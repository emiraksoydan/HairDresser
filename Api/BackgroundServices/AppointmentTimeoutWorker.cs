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
                    // Mevcut notification'ları güncelle: Type ve payload status'u güncelle
                    // ÖNEMLİ: Bu notification'ları SignalR ile de push etmeliyiz (veri tutarlılığı için)
                    var existingNotifications = await db.Notifications
                        .Where(n => n.AppointmentId == appt.Id
                                 && n.Type == NotificationType.AppointmentCreated)
                        .ToListAsync(stoppingToken);

                    foreach (var notif in existingNotifications)
                    {
                        notif.Type = NotificationType.AppointmentUnanswered;
                        
                        // Payload'daki status'u da güncelle (veri tutarlılığı için)
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
                                    if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase))
                                        continue; // Status'u atla, yeni değerle güncelleyeceğiz
                                    
                                    // Value'yü object'e çevir (basit tipler için)
                                    payloadDict[prop.Name] = prop.Value.ValueKind switch
                                    {
                                        System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                                        System.Text.Json.JsonValueKind.Number => prop.Value.GetDecimal(),
                                        System.Text.Json.JsonValueKind.True => true,
                                        System.Text.Json.JsonValueKind.False => false,
                                        System.Text.Json.JsonValueKind.Null => null,
                                        _ => prop.Value.GetRawText() // Complex types için raw text
                                    };
                                }
                                
                                // Status'u Unanswered olarak ekle/güncelle
                                payloadDict["status"] = "Unanswered";
                                
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
                                Type = notif.Type,
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

                    // ÖNEMLİ: Yeni AppointmentUnanswered notification göndermeye gerek yok
                    // Çünkü eski AppointmentCreated notification'ları zaten AppointmentUnanswered'e çevrildi
                    // ve SignalR ile push edildi. Yeni notification göndermek duplicate'a yol açar.
                    // Eğer hiç notification yoksa (nadir durum), o zaman gönderilebilir ama 
                    // genelde her participant için AppointmentCreated notification'ı zaten var.
                    // Bu yüzden yeni notification göndermiyoruz.
                }

                if (expired.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(_settings.AppointmentTimeoutWorkerIntervalSeconds), stoppingToken);
            }
        }
    }
}
