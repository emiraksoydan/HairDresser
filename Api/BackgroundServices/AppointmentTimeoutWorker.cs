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
        IOptions<AppointmentSettings> appointmentSettings,
        ILogger<AppointmentTimeoutWorker> logger
    ) : BackgroundService
    {
        private readonly BackgroundServicesSettings _settings = backgroundServicesSettings.Value;
        private readonly AppointmentSettings _appointmentSettings = appointmentSettings.Value;
        private readonly ILogger<AppointmentTimeoutWorker> _logger = logger;

        // 3'lü sistem (StoreSelection) süreleri - appsettings.json'dan okunuyor
        private int StoreSelectionTotalMinutes => _appointmentSettings.StoreSelection.TotalMinutes;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                // Per-cycle timeout: yavaş DB sorgusu bir sonraki cycle'ı bloklamasın
                using var cycleCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cycleCts.CancelAfter(TimeSpan.FromMinutes(2));
                var cycleToken = cycleCts.Token;

                var now = DateTime.UtcNow;
                const int batchSize = 50; // Her seferde 50 appointment işle

                // Toplam expired appointment sayısını kontrol et
                var totalExpiredCount = await db.Appointments
                    .CountAsync(a => a.Status == AppointmentStatus.Pending
                                  && a.PendingExpiresAt != null
                                  && a.PendingExpiresAt <= now, cycleToken);


                // Batch'ler halinde işle
                int processedCount = 0;
                while (processedCount < totalExpiredCount)
                {
                    // Bir batch al
                    var expiredBatch = await db.Appointments
                        .Where(a => a.Status == AppointmentStatus.Pending
                                 && a.PendingExpiresAt != null
                                 && a.PendingExpiresAt <= now)
                        .OrderBy(a => a.PendingExpiresAt) // En eski olanları önce işle
                        .Take(batchSize)
                        .ToListAsync(cycleToken);

                    if (!expiredBatch.Any())
                        break; // Daha fazla expired appointment yok

                    foreach (var appt in expiredBatch)
                    {
                        try
                        {
                            await ProcessExpiredAppointmentAsync(appt, scope, cycleToken);
                            processedCount++;
                        }
                        catch (OperationCanceledException) when (cycleCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("Appointment timeout cycle exceeded 2 minute limit. Skipping remaining batch.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            processedCount++; // Sayacı artır ki sonsuz döngüye girmesin
                        }
                    }

                    // Cycle timeout kontrolü
                    if (cycleToken.IsCancellationRequested)
                        break;

                    // Batch işlendikten sonra kısa bir bekleme (database'e fazla yük bindirmemek için)
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
            var appointmentDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IAppointmentDal>();
            var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
            var threadDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IChatThreadDal>();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

            // Begin transaction to ensure atomicity of all operations
            await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                var trackedAppt = await db.Appointments
                    .FirstOrDefaultAsync(a => a.Id == appt.Id, stoppingToken);

                // ÖNEMLĐ: Status Pending değilse işleme (zaten işlenmiş demektir)
                if (trackedAppt == null || trackedAppt.Status != AppointmentStatus.Pending)
                {
                    if (trackedAppt != null && trackedAppt.Status == AppointmentStatus.Unanswered)
                    {
                        _logger.LogInformation("AppointmentTimeoutWorker: Appointment {AppointmentId} is already Unanswered, skipping processing",
                            trackedAppt.Id);
                    }
                    return;
                }

            var now = DateTime.UtcNow;
            var isStoreSelectionFlow = trackedAppt.StoreSelectionType == StoreSelectionType.StoreSelection &&
                trackedAppt.CustomerUserId.HasValue &&
                trackedAppt.FreeBarberUserId.HasValue;

            if (isStoreSelectionFlow)
            {
                var overallExpiresAt = trackedAppt.CreatedAt.AddMinutes(StoreSelectionTotalMinutes);
                if (now < overallExpiresAt)
                {
                    // Store 5dk cevap vermedi
                    if (trackedAppt.BarberStoreUserId.HasValue &&
                        trackedAppt.StoreDecision == DecisionStatus.Pending)
                    {
                        var storeOwnerUserId = trackedAppt.BarberStoreUserId;
                        var freeBarberUserId = trackedAppt.FreeBarberUserId;
                        trackedAppt.StoreDecision = DecisionStatus.NoAnswer;
                        trackedAppt.UpdatedAt = now;
                        trackedAppt.PendingExpiresAt = overallExpiresAt;
                        // Özel bildirim tipi: StoreSelectionTimeout
                        var recipients = new List<Guid>();
                        if (storeOwnerUserId.HasValue) recipients.Add(storeOwnerUserId.Value);
                        if (freeBarberUserId.HasValue) recipients.Add(freeBarberUserId.Value);
                        if (recipients.Count > 0)
                            await notifySvc.NotifyToRecipientsAsync(trackedAppt.Id, NotificationType.StoreSelectionTimeout, recipients, actorUserId: null);


                        ClearStoreSelectionSlot(trackedAppt);

                        await db.SaveChangesAsync(stoppingToken);
                        await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);
                        await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);

                        await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);

                        // Commit transaction for store timeout scenario
                        await transaction.CommitAsync(stoppingToken);
                        return;
                    }

                    // Müşteri 30dk içinde cevap vermedi (Store onayladıktan sonra)
                    if (trackedAppt.BarberStoreUserId.HasValue &&
                        trackedAppt.StoreDecision == DecisionStatus.Approved &&
                        trackedAppt.CustomerDecision == DecisionStatus.Pending)
                    {
                        var storeOwnerUserId = trackedAppt.BarberStoreUserId;
                        var freeBarberUserId = trackedAppt.FreeBarberUserId;
                        var customerUserId = trackedAppt.CustomerUserId;
                        trackedAppt.CustomerDecision = DecisionStatus.NoAnswer;
                        trackedAppt.UpdatedAt = now;
                        trackedAppt.StoreDecision = DecisionStatus.Pending;
                        trackedAppt.PendingExpiresAt = overallExpiresAt;
                        // Özel bildirim tipi: CustomerFinalTimeout
                        var recipients = new List<Guid>();
                        if (storeOwnerUserId.HasValue) recipients.Add(storeOwnerUserId.Value);
                        if (freeBarberUserId.HasValue) recipients.Add(freeBarberUserId.Value);
                        if (customerUserId.HasValue) recipients.Add(customerUserId.Value);
                        if (recipients.Count > 0)
                            await notifySvc.NotifyToRecipientsAsync(trackedAppt.Id, NotificationType.CustomerFinalTimeout, recipients, actorUserId: null);


                        ClearStoreSelectionSlot(trackedAppt);

                        await db.SaveChangesAsync(stoppingToken);
                        await UpdateThreadStoreOwnerAsync(threadDal, trackedAppt.Id, null);
                        await chatService.PushAppointmentThreadUpdatedAsync(trackedAppt.Id);

                        await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);

                        // Commit transaction for customer timeout scenario
                        await transaction.CommitAsync(stoppingToken);
                        return;
                    }
                }
            }

            UpdateAppointmentStatus(trackedAppt);

            // Katılımcılar (thread removal + appointment.updated + badge update için)
            var participantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Cevapsız olduğunda slot kilidini kaldır (availability + unique index için)
            // ÖNEMLİ: Store bilgisini (BarberStoreUserId) SAKLIYORUZ - iptal tabında görünmeli.
            // UYARI: Tarih ve saat bilgileri TEMİZLENMELİ - başka randevular alınabilsin!
            if (trackedAppt.ChairId.HasValue)
            {
                trackedAppt.ChairId = null;
                trackedAppt.ManuelBarberId = null;
                trackedAppt.AppointmentDate = null;
                trackedAppt.StartTime = null;
                trackedAppt.EndTime = null;
            }

                await db.SaveChangesAsync(stoppingToken);

                await ReleaseFreeBarberAsync(trackedAppt, freeBarberDal, stoppingToken);

                // Thread'i kaldır + unread count'ları sıfırla + badge update schedule et
                var thread = await threadDal.Get(t => t.AppointmentId == trackedAppt.Id);
                if (thread != null)
                {
                    thread.CustomerUnreadCount = 0;
                    thread.StoreUnreadCount = 0;
                    thread.FreeBarberUnreadCount = 0;
                    thread.UpdatedAt = DateTime.UtcNow;
                    await threadDal.Update(thread);

                    // Thread'i kaldır - cevapsız randevularda thread gösterilmemeli
                    foreach (var userId in participantUserIds)
                    {
                        try { await realtime.PushChatThreadRemovedAsync(userId, thread.Id); } catch { /* non-critical */ }
                    }
                }

                // Appointment listesini anlık güncelle (appointment.updated)
                foreach (var userId in participantUserIds)
                {
                    try
                    {
                        var cancelled = await appointmentDal.GetAllAppointmentByFilter(userId, AppointmentFilter.Cancelled);
                        var dto = cancelled.FirstOrDefault(a => a.Id == trackedAppt.Id);
                        if (dto != null)
                        {
                            await realtime.PushAppointmentUpdatedAsync(userId, dto);
                        }
                    }
                    catch
                    {
                        // Hata durumunda devam et, kritik değil
                    }
                }

                await UpdateAndSendNotificationsAsync(trackedAppt, db, notifySvc, realtime, scope, stoppingToken);

                // Commit transaction - all operations successful
                await transaction.CommitAsync(stoppingToken);

            }
            catch (Exception ex)
            {
                // Rollback transaction on any error
                await transaction.RollbackAsync(stoppingToken);
                _logger.LogError(ex, "Failed to process expired appointment {AppointmentId}. Transaction rolled back.", appt.Id);
                throw;
            }
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

            // CustomerDecision için de NoAnswer ekle (Customer -> FreeBarber + Store senaryosunda)
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
        /// FreeBarber'ı release eder (IsAvailable = true)
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
        /// Mevcut notification'ları günceller ve yeni notification'lar gönderir
        /// </summary>
        private async Task UpdateAndSendNotificationsAsync(
            Entities.Concrete.Entities.Appointment trackedAppt,
            DatabaseContext db,
            IAppointmentNotifyService notifySvc,
            IRealTimePublisher realtime,
            IServiceScope scope,
            CancellationToken stoppingToken)
        {
            // ÖNEMLİ: Notification Type değişmemeli - sadece payload güncellenmeli
            // Mevcut notification'ları bul (herhangi bir type olabilir - AppointmentCreated, AppointmentApproved, vb.)
            var existingNotifications = await db.Notifications
                .Where(n => n.AppointmentId == trackedAppt.Id)
                .ToListAsync(stoppingToken);

            // Mevcut notification'ları olan kullanıcılar (bunların notification'ları güncellenecek)
            var usersWithExistingNotifications = existingNotifications.Select(n => n.UserId).Distinct().ToList();

            // DÜZELTME: Sadece OKUNMAMIŞ (isRead: false) bildirimlerin payload'u güncellensin
            // Kullanıcı zaten aksiyon aldıysa (Onayla/Reddet) ve bildirim okundu işaretliyse,
            // payload'u güncellemeye gerek yok - zaten karar verilmiş durumda
            var unreadNotifications = existingNotifications.Where(n => !n.IsRead).ToList();

            // Mevcut notification'ları güncelle: Sadece okunmamış olanların payload'daki status'u güncelle, Type değiştirme
            foreach (var notif in unreadNotifications)
            {
                await UpdateNotificationPayloadAsync(notif, trackedAppt, db, realtime, stoppingToken);
            }

            var allParticipantUserIds = new[] { trackedAppt.CustomerUserId, trackedAppt.BarberStoreUserId, trackedAppt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // FIX: AppointmentUnanswered bildirimi zaten olan kullanıcıları bul (duplicate önleme)
            // Eski davranış: mevcut notification'ı olmayan kullanıcılara gönder
            // Yeni davranış: AppointmentUnanswered tipinde bildirimi olmayan TÜM katılımcılara gönder
            // Böylece müşteri AppointmentCreated bildirimi olsa bile yeni bir Unanswered bildirimi alır
            var usersWithUnansweredNotification = await db.Notifications
                .Where(n => n.AppointmentId == trackedAppt.Id && n.Type == NotificationType.AppointmentUnanswered)
                .Select(n => n.UserId)
                .Distinct()
                .ToListAsync(stoppingToken);

            var usersWithoutNotifications = allParticipantUserIds
                .Where(userId => !usersWithUnansweredNotification.Contains(userId))
                .ToList();

            if (usersWithoutNotifications.Any() && trackedAppt.Status == AppointmentStatus.Unanswered)
            {
                await SendNewUnansweredNotificationsAsync(trackedAppt, notifySvc, usersWithoutNotifications, stoppingToken);

                // NOT: MarkThreadReadByAppointmentSystemAsync çağrısı kaldırıldı
                // Çünkü thread unread count'ları zaten yukarıda sıfırlanıyor (satır 243-245)
                // Ve bu fonksiyon bildirimleri okunmuş yapabilir (istenmeyen davranış)
            }
        }

        /// <summary>
        /// Notification payload'ını günceller ve SignalR ile push eder
        /// </summary>
        private async Task UpdateNotificationPayloadAsync(
            Entities.Concrete.Entities.Notification notif,
            Entities.Concrete.Entities.Appointment trackedAppt,
            DatabaseContext db,
            IRealTimePublisher realtime,
            CancellationToken stoppingToken)
        {
            // Payload'daki status'u güncelle (veri tutarlılığı için)
            if (string.IsNullOrEmpty(notif.PayloadJson) || notif.PayloadJson.Trim() == "{}")
                return;

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
                        prop.Name.Equals("freeBarberDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("customerDecision", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("pendingExpiresAt", StringComparison.OrdinalIgnoreCase))
                        continue;

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

                // Update status and decisions
                payloadDict["status"] = (int)trackedAppt.Status;
                payloadDict["storeDecision"] = trackedAppt.StoreDecision.HasValue ? (int)trackedAppt.StoreDecision.Value : null;
                payloadDict["freeBarberDecision"] = trackedAppt.FreeBarberDecision.HasValue ? (int)trackedAppt.FreeBarberDecision.Value : null;
                payloadDict["customerDecision"] = trackedAppt.CustomerDecision.HasValue ? (int)trackedAppt.CustomerDecision.Value : null;
                payloadDict["pendingExpiresAt"] = trackedAppt.PendingExpiresAt;

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
                return;
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
                await realtime.PushNotificationSilentUpdateAsync(notif.UserId, updatedDto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push updated notification {NotificationId} to SignalR", notif.Id);
            }
        }

        /// <summary>
        /// Yeni AppointmentUnanswered notification'ları gönderir
        /// </summary>
        private async Task SendNewUnansweredNotificationsAsync(
            Entities.Concrete.Entities.Appointment trackedAppt,
            IAppointmentNotifyService notifySvc,
            List<Guid> usersWithoutNotifications,
            CancellationToken stoppingToken)
        {
            try
            {
                // ÖNEMLĐ: Eğer liste boşsa bildirim gönderme (duplicate engellemek için)
                if (usersWithoutNotifications == null || !usersWithoutNotifications.Any())
                {
                    _logger.LogInformation("AppointmentTimeoutWorker: No users without notifications for appointment {AppointmentId}, skipping notification send",
                        trackedAppt.Id);
                    return;
                }

                _logger.LogInformation("AppointmentTimeoutWorker: Sending new AppointmentUnanswered notifications to {Count} users without existing notifications for appointment {AppointmentId}",
                    usersWithoutNotifications.Count, trackedAppt.Id);

                // DÜZELTME: NotifyWithAppointmentToRecipientsAsync kullan - appointment entity'sini direkt al
                // NotifyToRecipientsAsync DB'den appointment'ı tekrar okur; açık transaction içinde
                // uncommitted Unanswered status yeni bağlantıda görünmeyebilir (READ COMMITTED isolation).
                // Entity direkt geçilerek hem doğru status hem de gereksiz DB okuma önlenir.
                await notifySvc.NotifyWithAppointmentToRecipientsAsync(
                    trackedAppt,
                    NotificationType.AppointmentUnanswered,
                    usersWithoutNotifications,
                    actorUserId: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AppointmentUnanswered notifications for appointment {AppointmentId}", trackedAppt.Id);
                // Notification gönderimi başarısız olsa bile appointment update'i commit edilmeli
            }
        }
    }
}
