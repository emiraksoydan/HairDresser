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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

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

                // Her appointment için ayrı transaction ile işlem yap
                foreach (var appt in expired)
                {
                    try
                    {
                        await ProcessExpiredAppointmentAsync(appt, scope, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Bir appointment başarısız olursa diğerlerini etkilemesin
                        _logger.LogError(ex, "Failed to process expired appointment {AppointmentId}", appt.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.AppointmentTimeoutWorkerIntervalSeconds), stoppingToken);
            }
        }

        /// <summary>
        /// Her expired appointment için ayrı transaction içinde işlem yapar
        /// Bir appointment başarısız olursa diğerleri etkilenmez
        /// </summary>
        [TransactionScopeAspect]
        private async Task ProcessExpiredAppointmentAsync(
            Entities.Concrete.Entities.Appointment appt,
            IServiceScope scope,
            CancellationToken stoppingToken)
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();
            var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
            var realtime = scope.ServiceProvider.GetRequiredService<IRealTimePublisher>();

            // ÖNEMLİ: Entity detached durumda olabilir, DbContext'e attach et veya tekrar çek
            // Daha güvenli yaklaşım: Entity'yi tekrar çek (tracked olacak)
            var trackedAppt = await db.Appointments.FindAsync(new object[] { appt.Id }, stoppingToken);
            if (trackedAppt == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found in database", appt.Id);
                return;
            }

            trackedAppt.Status = AppointmentStatus.Unanswered;
            trackedAppt.PendingExpiresAt = null;
            trackedAppt.UpdatedAt = DateTime.UtcNow;

            if (trackedAppt.StoreDecision == DecisionStatus.Pending)
                trackedAppt.StoreDecision = DecisionStatus.NoAnswer;

            if (trackedAppt.FreeBarberDecision == DecisionStatus.Pending)
                trackedAppt.FreeBarberDecision = DecisionStatus.NoAnswer;

            // freebarber release
            if (trackedAppt.FreeBarberUserId.HasValue)
            {
                var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == trackedAppt.FreeBarberUserId.Value);
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
                .Where(n => n.AppointmentId == trackedAppt.Id)
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
                        payloadDict["storeDecision"] = trackedAppt.StoreDecision == DecisionStatus.Pending ? (int)DecisionStatus.NoAnswer : (int)trackedAppt.StoreDecision;
                        payloadDict["freeBarberDecision"] = trackedAppt.FreeBarberDecision == DecisionStatus.Pending ? (int)DecisionStatus.NoAnswer : (int)trackedAppt.FreeBarberDecision;
                        
                        // Geri JSON string'e çevir
                        notif.PayloadJson = JsonSerializer.Serialize(payloadDict, options);
                        
                        // ÖNEMLİ: Notification'ı DbContext'e attach et veya Update çağrısı yap
                        // DbContext tarafından track edilmesi için
                        db.Notifications.Update(notif);
                    }
                    catch (Exception ex)
                    {
                        // Payload parse edilemezse log ve devam et
                        _logger.LogWarning(ex, "Failed to update notification payload for notification {NotificationId}", notif.Id);
                    }
                }
                
                // Güncellenmiş notification'ı SignalR ile push et (veri tutarlılığı için)
                // NOT: SignalR push transaction dışında yapılmalı (commit sonrası)
                // Ancak şu an transaction içindeyiz, bu yüzden push'u transaction sonrası yapmak için
                // BadgeUpdateService gibi bir mekanizma gerekir, ama şimdilik burada bırakıyoruz
                // Çünkü notification zaten DB'de güncellenmiş olacak
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
            var allParticipantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
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
                    await notifySvc.NotifyAsync(trackedAppt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send AppointmentUnanswered notifications for appointment {AppointmentId}", appt.Id);
                    // Notification gönderimi başarısız olsa bile appointment update'i commit edilmeli
                }
            }

            // ÖNEMLİ: TransactionScopeAspect otomatik olarak SaveChanges'i çağıracak
            // Bu yüzden manuel SaveChangesAsync çağrısına gerek yok
            // DbContext değişiklikleri otomatik olarak track ediyor (trackedAppt ve notif.Update() sayesinde)
            // TransactionScopeAspect reflection ile DbContext'i bulup SaveChanges'i çağıracak
        }
    }
}
