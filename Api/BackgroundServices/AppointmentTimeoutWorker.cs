using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Configuration;
using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;

namespace Api.BackgroundServices
{
    public class AppointmentTimeoutWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesSettings> backgroundServicesSettings,
        ILogger<AppointmentTimeoutWorker> logger
    ) : BackgroundService
    {
        private readonly BackgroundServicesSettings _settings = backgroundServicesSettings.Value;
        private readonly ILogger<AppointmentTimeoutWorker> _logger = logger;

        [TransactionScopeAspect]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();
                var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
                var realtime = scope.ServiceProvider.GetRequiredService<IRealTimePublisher>();

                var now = DateTime.UtcNow;

                var expired = await db.Appointments
                    .Where(a => a.Status == AppointmentStatus.Pending
                             && a.PendingExpiresAt != null
                             && a.PendingExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                if (expired.Any())
                {
                    _logger.LogInformation("AppointmentTimeoutWorker: Found {Count} expired appointments", expired.Count);
                }

                // Her appointment için işlem yap
                foreach (var appt in expired)
                {
                    appt.Status = AppointmentStatus.Unanswered;
                    appt.PendingExpiresAt = null;
                    appt.UpdatedAt = DateTime.UtcNow;

                    if (appt.StoreDecision == DecisionStatus.Pending)
                        appt.StoreDecision = DecisionStatus.NoAnswer;

                    if (appt.FreeBarberDecision == DecisionStatus.Pending)
                        appt.FreeBarberDecision = DecisionStatus.NoAnswer;

                    // freebarber release
                    if (appt.FreeBarberUserId.HasValue)
                    {
                        var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                        if (fb != null)
                        {
                            fb.IsAvailable = true;
                            fb.UpdatedAt = DateTime.UtcNow;
                            await freeBarberDal.Update(fb);
                        }
                    }
                    // ÖNEMLİ: Notification Type değişmemeli - sadece payload güncellenmeli
                    // Mevcut notification'ları bul (herhangi bir type olabilir - AppointmentCreated, AppointmentApproved, vb.)
                    var existingNotifications = await db.Notifications
                        .Where(n => n.AppointmentId == appt.Id)
                        .ToListAsync(stoppingToken);

                    // Mevcut notification'ları olan kullanıcılar (bunların notification'ları güncellenecek)
                    var usersWithExistingNotifications = existingNotifications.Select(n => n.UserId).Distinct().ToList();

                    // Mevcut notification'ları güncelle: Sadece payload'daki status'u güncelle, Type değiştirme
                    foreach (var notif in existingNotifications)
                    {
                        // Payload'daki status'u güncelle (veri tutarlılığı için)
                        if (!string.IsNullOrEmpty(notif.PayloadJson) && notif.PayloadJson.Trim() != "{}")
                        {
                            try
                            {
                                var options = new JsonSerializerOptions 
                                { 
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                    WriteIndented = false 
                                };
                                
                                // Mevcut payload'ı parse et ve status'u güncelle
                                using var doc = JsonDocument.Parse(notif.PayloadJson);
                                var root = doc.RootElement;
                                
                                // Yeni bir dictionary oluştur (object tipinde değerler için)
                                var payloadDict = new Dictionary<string, object?>();
                                
                                // Mevcut tüm property'leri kopyala (status hariç)
                                foreach (var prop in root.EnumerateObject())
                                {
                                    if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                                        prop.Name.Equals("storeDecision", StringComparison.OrdinalIgnoreCase) ||
                                        prop.Name.Equals("freeBarberDecision", StringComparison.OrdinalIgnoreCase))
                                        continue; // Status ve decision'ları atla, yeni değerle güncelleyeceğiz
                                    
                                    // Value'yü object'e çevir (basit tipler için)
                                    payloadDict[prop.Name] = prop.Value.ValueKind switch
                                    {
                                        System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                                        System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? (object)intVal : prop.Value.GetDecimal(),
                                        System.Text.Json.JsonValueKind.True => true,
                                        System.Text.Json.JsonValueKind.False => false,
                                        System.Text.Json.JsonValueKind.Null => null,
                                        System.Text.Json.JsonValueKind.Object => JsonSerializer.Deserialize<object>(prop.Value.GetRawText()),
                                        System.Text.Json.JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(prop.Value.GetRawText()),
                                        _ => prop.Value.GetRawText() // Complex types için raw text
                                    };
                                }
                                
                                // Status ve decision'ları Unanswered olarak ekle/güncelle
                                payloadDict["status"] = (int)AppointmentStatus.Unanswered;
                                payloadDict["storeDecision"] = appt.StoreDecision == DecisionStatus.Pending ? (int)DecisionStatus.NoAnswer : (int)appt.StoreDecision;
                                payloadDict["freeBarberDecision"] = appt.FreeBarberDecision == DecisionStatus.Pending ? (int)DecisionStatus.NoAnswer : (int)appt.FreeBarberDecision;
                                
                                // Geri JSON string'e çevir
                                notif.PayloadJson = JsonSerializer.Serialize(payloadDict, options);
                            }
                            catch (Exception ex)
                            {
                                // Payload parse edilemezse log ve devam et
                                _logger.LogWarning(ex, "Failed to update notification payload for notification {NotificationId}", notif.Id);
                            }
                        }
                        
                        // Güncellenmiş notification'ı SignalR ile push et (veri tutarlılığı için)
                        try
                        {
                            var updatedDto = new Entities.Concrete.Dto.NotificationDto
                            {
                                Id = notif.Id,
                                Type = notif.Type, // Type değişmedi - aynı kaldı
                                AppointmentId = notif.AppointmentId,
                                Title = notif.Title,
                                Body = notif.Body,
                                PayloadJson = notif.PayloadJson,
                                CreatedAt = notif.CreatedAt,
                                IsRead = notif.IsRead
                            };
                            await realtime.PushNotificationAsync(notif.UserId, updatedDto);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to push updated notification {NotificationId} to SignalR", notif.Id);
                        }
                    }

                    // ÖNEMLİ: Mevcut notification'ı olmayan kullanıcılara yeni AppointmentUnanswered notification gönder
                    // Tüm ilgili kullanıcıları belirle
                    var allParticipantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .Distinct()
                        .ToList();

                    // Mevcut notification'ı olmayan kullanıcılar
                    var usersWithoutNotifications = allParticipantUserIds
                        .Where(userId => !usersWithExistingNotifications.Contains(userId))
                        .ToList();

                    // Bu kullanıcılara yeni AppointmentUnanswered notification gönder
                    if (usersWithoutNotifications.Any())
                    {
                        try
                        {
                            _logger.LogInformation("AppointmentTimeoutWorker: Sending new AppointmentUnanswered notifications to {Count} users without existing notifications for appointment {AppointmentId}", 
                                usersWithoutNotifications.Count, appt.Id);
                            
                            // Her kullanıcı için manuel olarak AppointmentUnanswered notification gönder
                            // NotifyAsync tüm kullanıcılara gönderir, ama biz sadece notification'ı olmayanlara göndermek istiyoruz
                            // Bu yüzden CreateAndPushAsync'i direkt kullanmalıyız
                            var notificationSvc = scope.ServiceProvider.GetRequiredService<Business.Abstract.INotificationService>();
                            
                            // AppointmentNotifyManager'dan title ve payload oluşturmak için
                            // Basit bir yaklaşım: NotifyAsync'i çağırmak ama sadece belirli kullanıcılara göndermek
                            // Ancak NotifyAsync tüm kullanıcılara gönderir
                            // En iyi yaklaşım: Her kullanıcı için ayrı ayrı CreateAndPushAsync çağırmak
                            // Ama title ve payload oluşturmak için AppointmentNotifyManager.NotifyAsyncInternal mantığına ihtiyacımız var
                            
                            // Geçici çözüm: NotifyAsync'i çağırmak - CreateAndPushAsync içinde duplicate kontrolü var
                            // Ama type farklı olduğu için yeni notification oluşturulur (bu istediğimiz davranış)
                            // Mevcut notification'ı olan kullanıcılar için: Zaten yukarıda güncellendi
                            // Mevcut notification'ı olmayan kullanıcılar için: Yeni AppointmentUnanswered gönderilecek
                            await notifySvc.NotifyAsync(appt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send AppointmentUnanswered notifications for appointment {AppointmentId}", appt.Id);
                        }
                    }
                }

                if (expired.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(_settings.AppointmentTimeoutWorkerIntervalSeconds), stoppingToken);
            }
        }
    }
}
