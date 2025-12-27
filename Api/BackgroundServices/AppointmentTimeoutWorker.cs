using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Configuration;
using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        private const int StoreSelectionTotalMinutes = 30;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                var now = DateTime.UtcNow;
                const int batchSize = 50; // Her seferde 50 appointment iÅŸle

                // Toplam expired appointment sayÄ±sÄ±nÄ± kontrol et
                var totalExpiredCount = await db.Appointments
                    .CountAsync(a => a.Status == AppointmentStatus.Pending
                                  && a.PendingExpiresAt != null
                                  && a.PendingExpiresAt <= now, stoppingToken);


                // Batch'ler halinde iÅŸle
                int processedCount = 0;
                while (processedCount < totalExpiredCount)
                {
                    // Bir batch al
                    var expiredBatch = await db.Appointments
                        .Where(a => a.Status == AppointmentStatus.Pending
                                 && a.PendingExpiresAt != null
                                 && a.PendingExpiresAt <= now)
                        .OrderBy(a => a.PendingExpiresAt) // En eski olanlarÄ± Ã¶nce iÅŸle
                        .Take(batchSize)
                        .ToListAsync(stoppingToken);

                    if (!expiredBatch.Any())
                        break; // Daha fazla expired appointment yok

                    foreach (var appt in expiredBatch)
                    {
                        try
                        {
                            await ProcessExpiredAppointmentAsync(appt, scope, stoppingToken);
                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            processedCount++; // SayacÄ± artÄ±r ki sonsuz dÃ¶ngÃ¼ye girmesin
                        }
                    }

                    // Batch iÅŸlendikten sonra kÄ±sa bir bekleme (database'e fazla yÃ¼k bindirmemek iÃ§in)
                    if (processedCount < totalExpiredCount)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.AppointmentTimeoutWorkerIntervalSeconds), stoppingToken);
            }
        }

        private async Task ProcessExpiredAppointmentAsync(
            Entities.Concrete.Entities.Appointment appt,
            IServiceScope scope,
            CancellationToken stoppingToken)
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();
            var realtime = scope.ServiceProvider.GetRequiredService<IRealTimePublisher>();
            var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
            var threadDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IChatThreadDal>();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

            var trackedAppt = await db.Appointments
                .FirstOrDefaultAsync(a => a.Id == appt.Id, stoppingToken);
            if (trackedAppt == null || trackedAppt.Status != AppointmentStatus.Pending)
                return;

            var now = DateTime.UtcNow;
            var isStoreSelectionFlow = trackedAppt.StoreSelectionType == StoreSelectionType.StoreSelection &&
                trackedAppt.CustomerUserId.HasValue &&
                trackedAppt.FreeBarberUserId.HasValue;

            if (isStoreSelectionFlow)
            {
                var overallExpiresAt = trackedAppt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                if (now < overallExpiresAt)
                {
                    if (trackedAppt.BarberStoreUserId.HasValue &&
                        trackedAppt.StoreDecision == DecisionStatus.Pending)
                    {
                        trackedAppt.StoreDecision = DecisionStatus.NoAnswer;
                        trackedAppt.UpdatedAt = now;

                        ClearStoreSelectionSlot(trackedAppt);
                        trackedAppt.PendingExpiresAt = overallExpiresAt;

                        await db.SaveChangesAsync(stoppingToken);
                        await ReleaseFreeBarberAsync(trackedAppt, freeBarberDal, stoppingToken);
                        await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);
                        await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);
                        await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);
                        return;
                    }

                    if (trackedAppt.BarberStoreUserId.HasValue &&
                        trackedAppt.StoreDecision == DecisionStatus.Approved &&
                        trackedAppt.CustomerDecision == DecisionStatus.Pending)
                    {
                        trackedAppt.CustomerDecision = DecisionStatus.NoAnswer;
                        trackedAppt.UpdatedAt = now;

                        ClearStoreSelectionSlot(trackedAppt);
                        trackedAppt.StoreDecision = DecisionStatus.Pending;
                        trackedAppt.PendingExpiresAt = overallExpiresAt;

                        await db.SaveChangesAsync(stoppingToken);
                        await ReleaseFreeBarberAsync(trackedAppt, freeBarberDal, stoppingToken);
                        await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);
                        await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);
                        await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);
                        return;
                    }
                }
            }

            UpdateAppointmentStatus(trackedAppt);

            if (isStoreSelectionFlow && trackedAppt.BarberStoreUserId.HasValue)
            {
                ClearStoreSelectionSlot(trackedAppt);
                await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);
                await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);
            }

            await db.SaveChangesAsync(stoppingToken);
            await ReleaseFreeBarberAsync(trackedAppt, freeBarberDal, stoppingToken);
            await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);
        }

        /// <summary>
        /// Marks appointment as unanswered.
        /// </summary>
        private static void UpdateAppointmentStatus(Entities.Concrete.Entities.Appointment appt)
        {
            appt.Status = AppointmentStatus.Unanswered;
            appt.PendingExpiresAt = null;
            appt.UpdatedAt = DateTime.UtcNow;

            if (appt.StoreDecision == DecisionStatus.Pending)
                appt.StoreDecision = DecisionStatus.NoAnswer;

            if (appt.FreeBarberDecision == DecisionStatus.Pending)
                appt.FreeBarberDecision = DecisionStatus.NoAnswer;
            
            // CustomerDecision iÃ§in de NoAnswer ekle (Customer -> FreeBarber + Store senaryosunda)
            if (appt.CustomerDecision == DecisionStatus.Pending)
                appt.CustomerDecision = DecisionStatus.NoAnswer;
        }

        private static void ClearStoreSelectionSlot(Entities.Concrete.Entities.Appointment appt)
        {
            appt.BarberStoreUserId = null;
            appt.ChairId = null;
            appt.AppointmentDate = null;
            appt.StartTime = null;
            appt.EndTime = null;
            appt.ManuelBarberId = null;
        }

        private static async Task UpdateThreadStoreOwnerAsync(DataAccess.Abstract.IChatThreadDal threadDal, Guid appointmentId, Guid? storeOwnerUserId)
        {
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            thread.StoreOwnerUserId = storeOwnerUserId;
            thread.UpdatedAt = DateTime.UtcNow;
            await threadDal.Update(thread);
        }

        /// <summary>
        /// FreeBarber'Ä± release eder (IsAvailable = true)
        /// </summary>
        private async Task ReleaseFreeBarberAsync(
            Entities.Concrete.Entities.Appointment appt,
            DataAccess.Abstract.IFreeBarberDal freeBarberDal,
            CancellationToken stoppingToken)
        {
            if (!appt.FreeBarberUserId.HasValue)
                return;

            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
            if (fb != null)
            {
                fb.IsAvailable = true;
                fb.UpdatedAt = DateTime.UtcNow;
                await freeBarberDal.Update(fb);
            }
        }

        /// <summary>
        /// Mevcut notification'larÄ± gÃ¼nceller ve yeni notification'lar gÃ¶nderir
        /// </summary>
        private async Task UpdateAndSendNotificationsAsync(
            Entities.Concrete.Entities.Appointment trackedAppt,
            DatabaseContext db,
            IAppointmentNotifyService notifySvc,
            IRealTimePublisher realtime,
            IServiceScope scope,
            CancellationToken stoppingToken)
        {
            // Ã–NEMLÄ°: Notification Type deÄŸiÅŸmemeli - sadece payload gÃ¼ncellenmeli
            // Mevcut notification'larÄ± bul (herhangi bir type olabilir - AppointmentCreated, AppointmentApproved, vb.)
            var existingNotifications = await db.Notifications
                .Where(n => n.AppointmentId == trackedAppt.Id)
                .ToListAsync(stoppingToken);

            // Mevcut notification'larÄ± olan kullanÄ±cÄ±lar (bunlarÄ±n notification'larÄ± gÃ¼ncellenecek)
            var usersWithExistingNotifications = existingNotifications.Select(n => n.UserId).Distinct().ToList();

            // Mevcut notification'larÄ± gÃ¼ncelle: Sadece payload'daki status'u gÃ¼ncelle, Type deÄŸiÅŸtirme
            foreach (var notif in existingNotifications)
            {
                await UpdateNotificationPayloadAsync(notif, trackedAppt, db, realtime, stoppingToken);
            }

            // Ã–NEMLÄ°: Mevcut notification'Ä± olmayan kullanÄ±cÄ±lara yeni AppointmentUnanswered notification gÃ¶nder
            var allParticipantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var usersWithoutNotifications = allParticipantUserIds
                .Where(userId => !usersWithExistingNotifications.Contains(userId))
                .ToList();

            if (usersWithoutNotifications.Any())
            {
                await SendNewUnansweredNotificationsAsync(trackedAppt, notifySvc, usersWithoutNotifications, stoppingToken);
            }
        }

        /// <summary>
        /// Notification payload'Ä±nÄ± gÃ¼nceller ve SignalR ile push eder
        /// </summary>
        private async Task UpdateNotificationPayloadAsync(
            Entities.Concrete.Entities.Notification notif,
            Entities.Concrete.Entities.Appointment trackedAppt,
            DatabaseContext db,
            IRealTimePublisher realtime,
            CancellationToken stoppingToken)
        {
            // Payload'daki status'u gÃ¼ncelle (veri tutarlÄ±lÄ±ÄŸÄ± iÃ§in)
            if (string.IsNullOrEmpty(notif.PayloadJson) || notif.PayloadJson.Trim() == "{}")
                return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                // Mevcut payload'Ä± parse et ve status'u gÃ¼ncelle
                using var doc = JsonDocument.Parse(notif.PayloadJson);
                var root = doc.RootElement;

                // Yeni bir dictionary oluÅŸtur (object tipinde deÄŸerler iÃ§in)
                var payloadDict = new Dictionary<string, object?>();

                // Mevcut tÃ¼m property'leri kopyala (status hariÃ§)
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("storeDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("freeBarberDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("customerDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("pendingExpiresAt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Value'yÃ¼ object'e Ã§evir (basit tipler iÃ§in)
                    payloadDict[prop.Name] = prop.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                        System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? (object)intVal : prop.Value.GetDecimal(),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.Null => null,
                        System.Text.Json.JsonValueKind.Object => JsonSerializer.Deserialize<object>(prop.Value.GetRawText()),
                        System.Text.Json.JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(prop.Value.GetRawText()),
                        _ => prop.Value.GetRawText() // Complex types iÃ§in raw text
                    };
                }

                // Update status and decisions
                payloadDict["status"] = (int)trackedAppt.Status;
                payloadDict["storeDecision"] = (int)trackedAppt.StoreDecision;
                payloadDict["freeBarberDecision"] = (int)trackedAppt.FreeBarberDecision;
                payloadDict["customerDecision"] = (int)trackedAppt.CustomerDecision;
                payloadDict["pendingExpiresAt"] = trackedAppt.PendingExpiresAt;

                // Geri JSON string'e Ã§evir
                notif.PayloadJson = JsonSerializer.Serialize(payloadDict, options);

                // Ã–NEMLÄ°: Notification'Ä± DbContext'e attach et veya Update Ã§aÄŸrÄ±sÄ± yap
                // DbContext tarafÄ±ndan track edilmesi iÃ§in
                db.Notifications.Update(notif);
            }
            catch (Exception ex)
            {
                // Payload parse edilemezse log ve devam et
                _logger.LogWarning(ex, "Failed to update notification payload for notification {NotificationId}", notif.Id);
                return;
            }

            // GÃ¼ncellenmiÅŸ notification'Ä± SignalR ile push et (veri tutarlÄ±lÄ±ÄŸÄ± iÃ§in)
            try
            {
                var updatedDto = new Entities.Concrete.Dto.NotificationDto
                {
                    Id = notif.Id,
                    Type = notif.Type, // Type deÄŸiÅŸmedi - aynÄ± kaldÄ±
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

        /// <summary>
        /// Yeni AppointmentUnanswered notification'larÄ± gÃ¶nderir
        /// </summary>
        private async Task SendNewUnansweredNotificationsAsync(
            Entities.Concrete.Entities.Appointment trackedAppt,
            IAppointmentNotifyService notifySvc,
            List<Guid> usersWithoutNotifications,
            CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("AppointmentTimeoutWorker: Sending new AppointmentUnanswered notifications to {Count} users without existing notifications for appointment {AppointmentId}",
                    usersWithoutNotifications.Count, trackedAppt.Id);

                // NotifyAsync tÃ¼m kullanÄ±cÄ±lara gÃ¶nderir, ama CreateAndPushAsync iÃ§inde duplicate kontrolÃ¼ var
                // Mevcut notification'Ä± olan kullanÄ±cÄ±lar iÃ§in: Zaten yukarÄ±da gÃ¼ncellendi
                // Mevcut notification'Ä± olmayan kullanÄ±cÄ±lar iÃ§in: Yeni AppointmentUnanswered gÃ¶nderilecek
                await notifySvc.NotifyAsync(trackedAppt.Id, NotificationType.AppointmentUnanswered, actorUserId: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AppointmentUnanswered notifications for appointment {AppointmentId}", trackedAppt.Id);
                // Notification gÃ¶nderimi baÅŸarÄ±sÄ±z olsa bile appointment update'i commit edilmeli
            }
        }
    }
}






