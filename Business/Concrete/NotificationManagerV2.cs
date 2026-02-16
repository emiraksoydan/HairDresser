using Business.Abstract;

using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// V2 Notification Manager - Refactored for better performance and real-time sync
    ///
    /// Key Improvements:
    /// 1. Unified notification creation pipeline
    /// 2. Batch processing for multiple recipients
    /// 3. Atomic payload updates with proper SignalR sync
    /// 4. Idempotent operations (duplicate handling)
    /// 5. Proper badge count management
    /// </summary>
    public class NotificationManagerV2(
        INotificationDal notificationDal,
        IRealTimePublisher realtime,
        IAppointmentDal appointmentDal,
        IPushNotificationService? pushNotificationService = null) : INotificationService
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        #region Core API Methods

        /// <summary>
        /// Creates notification for a single user and pushes via SignalR + FCM
        /// </summary>
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [LogAspect]
        public async Task<IDataResult<Guid>> CreateAndPushAsync(
            Guid userId,
            NotificationType type,
            Guid? appointmentId,
            string title,
            object payload,
            string? body = null)
        {
            // Duplicate check & update logic
            // ÖNEMLİ: Sadece AppointmentCreated için duplicate kontrolü yap
            // Geri dönüş bildirimleri (Approved, Rejected, vb.) için her zaman yeni bildirim oluştur
            // Çünkü kullanıcılar geri dönüş bildirimlerini görmeli (sound, badge count artmalı)
            if (appointmentId.HasValue && type == NotificationType.AppointmentCreated)
            {
                var existingNotif = await GetExistingNotificationAsync(userId, appointmentId.Value, type);
                if (existingNotif != null)
                {
                    // Update existing notification instead of creating duplicate
                    await UpdateNotificationPayloadAsync(existingNotif, payload);
                    var updatedDto = MapToDto(existingNotif);
                    await PushNotificationSilentAsync(userId, updatedDto);
                    return new SuccessDataResult<Guid>(existingNotif.Id);
                }
            }

            // Create new notification
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AppointmentId = appointmentId,
                Type = type,
                Title = title,
                Body = body,
                PayloadJson = JsonSerializer.Serialize(payload, _jsonOptions),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await notificationDal.Add(notification);

            // CRITICAL FIX: Count badge AFTER notification is added (transaction içinde)
            // Bu race condition'ı önler - aynı anda birden fazla bildirim geldiğinde doğru count hesaplanır
            int unreadCount = 0;
            if (!notification.IsRead)
            {
                unreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            }

            var notificationDto = MapToDto(notification);

            // Real-time push via SignalR
            await realtime.PushNotificationAsync(userId, notificationDto);

            // CRITICAL FIX: Send badge.updated event immediately after notification is pushed
            // This updates the badge count on the frontend without requiring an API refetch
            // Frontend expects this event for real-time badge count updates
            // Transaction commit sonrası badge count gönderilecek (TransactionScopeAspect sayesinde)
            if (!notification.IsRead && unreadCount > 0)
            {
                await realtime.PushBadgeUpdateAsync(userId, notificationUnreadCount: unreadCount);
            }

            // Push notification via FCM (background/closed app)
            if (pushNotificationService != null)
            {
                try
                {
                    await pushNotificationService.SendPushNotificationAsync(userId, notificationDto);
                }
                catch
                {
                    // FCM failure should not break notification creation
                }
            }

            return new SuccessDataResult<Guid>(notification.Id);
        }

        [LogAspect]
        public async Task<IDataResult<List<NotificationDto>>> GetAllNotify(Guid userId)
        {
            var notifications = await notificationDal.GetAll(x => x.UserId == userId);

            var dtos = notifications
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapToDto)
                .ToList();

            return new SuccessDataResult<List<NotificationDto>>(dtos);
        }

        [LogAspect]
        public async Task<IDataResult<int>> GetUnreadCountAsync(Guid userId)
        {
            var count = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            return new SuccessDataResult<int>(count);
        }

        [LogAspect]
        public async Task<IDataResult<bool>> MarkReadAsync(Guid userId, Guid notificationId)
        {
            var notification = await notificationDal.Get(x => x.Id == notificationId && x.UserId == userId);
            if (notification == null)
                return new ErrorDataResult<bool>(false, "Bildirim bulunamadı");

            if (notification.IsRead)
                return new SuccessDataResult<bool>(true); // Already read

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await notificationDal.Update(notification);

            // Badge count changed - notify frontend with count
            var unreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            await realtime.PushBadgeUpdateAsync(userId, unreadCount);

            return new SuccessDataResult<bool>(true);
        }

        [LogAspect]
        public async Task<IDataResult<bool>> MarkReadByAppointmentIdAsync(Guid userId, Guid appointmentId)
        {
            var notifications = await notificationDal.GetAll(x =>
                x.UserId == userId &&
                x.AppointmentId == appointmentId &&
                !x.IsRead);

            if (notifications == null || !notifications.Any())
                return new SuccessDataResult<bool>(true);

            var now = DateTime.UtcNow;
            foreach (var n in notifications)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            await notificationDal.UpdateRange(notifications);

            // Badge count changed - notify frontend with count
            var unreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            await realtime.PushBadgeUpdateAsync(userId, unreadCount);

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [LogAspect]
        public async Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid notificationId)
        {
            var notification = await notificationDal.Get(x => x.Id == notificationId && x.UserId == userId);
            if (notification == null)
                return new ErrorDataResult<bool>(false, "Bildirim bulunamadı");

            // Check if appointment is still active (Pending/Approved)
            if (notification.AppointmentId.HasValue)
            {
                var appointment = await appointmentDal.Get(x => x.Id == notification.AppointmentId.Value);
                if (appointment != null &&
                    (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Approved))
                {
                    return new ErrorDataResult<bool>(false, "Pending veya Approved durumundaki randevuların bildirimleri silinemez");
                }
            }

            // Mark as read if unread
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await notificationDal.Update(notification);
            }

            // Delete notification
            await notificationDal.Remove(notification);

            // Badge count changed - notify frontend with count
            var unreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            await realtime.PushBadgeUpdateAsync(userId, unreadCount);

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        [LogAspect]
        public async Task<IDataResult<bool>> DeleteAllAsync(Guid userId)
        {
            var notifications = await notificationDal.GetAll(x => x.UserId == userId);

            if (notifications == null || !notifications.Any())
                return new ErrorDataResult<bool>(false, "Silinecek bildirim bulunamadı.");

            // Get appointment IDs
            var appointmentIds = notifications
                .Where(n => n.AppointmentId.HasValue)
                .Select(n => n.AppointmentId!.Value)
                .Distinct()
                .ToList();

            // Batch fetch appointments
            var appointments = appointmentIds.Any()
                ? await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id))
                : new List<Appointment>();

            var appointmentStatusMap = appointments.ToDictionary(a => a.Id, a => a.Status);

            var notificationsToDelete = new List<Notification>();
            var notificationsToMarkRead = new List<Notification>();

            foreach (var n in notifications)
            {
                // Check appointment status
                if (n.AppointmentId.HasValue && appointmentStatusMap.TryGetValue(n.AppointmentId.Value, out var status))
                {
                    if (status == AppointmentStatus.Pending || status == AppointmentStatus.Approved)
                    {
                        continue; // Skip active appointments
                    }
                }

                // Mark as read if unread
                if (!n.IsRead)
                {
                    n.IsRead = true;
                    n.ReadAt = DateTime.UtcNow;
                    notificationsToMarkRead.Add(n);
                }

                notificationsToDelete.Add(n);
            }

            // Batch update
            if (notificationsToMarkRead.Any())
            {
                await notificationDal.UpdateRange(notificationsToMarkRead);
            }

            // Batch delete
            if (!notificationsToDelete.Any())
            {
                return new ErrorDataResult<bool>(false, "Silinecek bildirim bulunamadı. Tüm bildirimler Pending veya Approved durumundaki randevulara ait.");
            }

            foreach (var n in notificationsToDelete)
            {
                await notificationDal.Remove(n);
            }

            // Badge count changed - notify frontend with count
            var unreadCount = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            await realtime.PushBadgeUpdateAsync(userId, unreadCount);

            return new SuccessDataResult<bool>(true);
        }

        #endregion

        #region Appointment Notification Updates

        /// <summary>
        /// Updates notification payload for an appointment when status/decision changes
        /// This method is called from AppointmentManager after status updates
        /// </summary>
        [LogAspect]
        public async Task<IDataResult<bool>> UpdateNotificationPayloadByAppointmentAsync(
            Guid appointmentId,
            AppointmentStatus status,
            DecisionStatus? storeDecision = null,
            DecisionStatus? freeBarberDecision = null,
            DecisionStatus? customerDecision = null,
            DateTime? pendingExpiresAt = null)
        {
            // Get all notifications for this appointment
            var notifications = await notificationDal.GetAll(x => x.AppointmentId == appointmentId);

            if (notifications == null || !notifications.Any())
                return new SuccessDataResult<bool>(true);

            // NOT: Rejected durumunda da payload güncellenmeli ki frontend'de butonlar gizlensin
            // Eski yorum: "Skip if status is Rejected (avoid updating old notifications)" - Bu yanlıştı

            var updatedNotifications = new List<NotificationDto>();

            foreach (var notification in notifications)
            {
                // Only update action notifications (AppointmentCreated, StoreApprovedSelection)
                if (notification.Type != NotificationType.AppointmentCreated &&
                    notification.Type != NotificationType.StoreApprovedSelection)
                    continue;

                // Parse and update payload
                if (string.IsNullOrEmpty(notification.PayloadJson) || notification.PayloadJson.Trim() == "{}")
                    continue;

                try
                {
                    var updated = await UpdatePayloadFieldsAsync(
                        notification,
                        status,
                        storeDecision,
                        freeBarberDecision,
                        customerDecision,
                        pendingExpiresAt);

                    if (updated)
                    {
                        await notificationDal.Update(notification);
                        updatedNotifications.Add(MapToDto(notification));
                    }
                }
                catch
                {
                    // Skip if payload cannot be parsed
                    continue;
                }
            }

            // Push updated notifications to all users via SignalR
            var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
            foreach (var userId in userIds)
            {
                var userNotifications = updatedNotifications
                    .Where(dto => notifications.Any(n => n.Id == dto.Id && n.UserId == userId))
                    .ToList();

                foreach (var dto in userNotifications)
                {
                    try
                    {
                        await realtime.PushNotificationSilentUpdateAsync(userId, dto);

                        // FCM push for background/closed apps
                        if (pushNotificationService != null)
                        {
                            try
                            {
                                await pushNotificationService.SendPushNotificationAsync(userId, dto);
                            }
                            catch
                            {
                                // FCM failure should not break the update
                            }
                        }
                    }
                    catch
                    {
                        // SignalR failure should not break the update
                    }
                }
            }

            return new SuccessDataResult<bool>(true);
        }

        #endregion

        #region Private Helper Methods

        private async Task<Notification?> GetExistingNotificationAsync(Guid userId, Guid appointmentId, NotificationType type)
        {
            // Special handling for AppointmentUnanswered - check any type
            if (type == NotificationType.AppointmentUnanswered)
            {
                return await notificationDal.Get(x =>
                    x.UserId == userId &&
                    x.AppointmentId == appointmentId &&
                    x.Type == NotificationType.AppointmentUnanswered);
            }

            // AppointmentDecisionUpdated allows duplicates
            if (type == NotificationType.AppointmentDecisionUpdated)
            {
                return null;
            }

            // Other types - check for unread duplicates
            return await notificationDal.Get(x =>
                x.UserId == userId &&
                x.AppointmentId == appointmentId &&
                x.Type == type &&
                !x.IsRead);
        }

        private async Task UpdateNotificationPayloadAsync(Notification notification, object newPayload)
        {
            notification.PayloadJson = JsonSerializer.Serialize(newPayload, _jsonOptions);
            notification.CreatedAt = DateTime.UtcNow; // Update timestamp
            await notificationDal.Update(notification);
        }

        private async Task<bool> UpdatePayloadFieldsAsync(
            Notification notification,
            AppointmentStatus status,
            DecisionStatus? storeDecision,
            DecisionStatus? freeBarberDecision,
            DecisionStatus? customerDecision,
            DateTime? pendingExpiresAt)
        {
            using var doc = JsonDocument.Parse(notification.PayloadJson);
            var root = doc.RootElement;

            // Check if pendingExpiresAt matches (if specified)
            if (pendingExpiresAt.HasValue)
            {
                if (!root.TryGetProperty("pendingExpiresAt", out var pendingProp))
                    return false;

                if (pendingProp.ValueKind == JsonValueKind.String)
                {
                    var pendingString = pendingProp.GetString();
                    if (!string.IsNullOrWhiteSpace(pendingString) &&
                        DateTime.TryParse(pendingString, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedPending))
                    {
                        var targetPending = pendingExpiresAt.Value.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(pendingExpiresAt.Value, DateTimeKind.Utc)
                            : pendingExpiresAt.Value.ToUniversalTime();

                        if (parsedPending != targetPending)
                            return false;
                    }
                }
            }

            // Build updated payload
            var payloadDict = new Dictionary<string, object?>();

            // Copy existing properties (except status/decisions/pendingExpiresAt)
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("storeDecision", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("freeBarberDecision", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("customerDecision", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("pendingExpiresAt", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                payloadDict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? (object)intVal : prop.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Object => JsonSerializer.Deserialize<object>(prop.Value.GetRawText()),
                    JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(prop.Value.GetRawText()),
                    _ => prop.Value.GetRawText()
                };
            }

            // Update status and decisions (as int for consistency)
            payloadDict["status"] = (int)status;
            if (storeDecision.HasValue)
                payloadDict["storeDecision"] = (int)storeDecision.Value;
            if (freeBarberDecision.HasValue)
                payloadDict["freeBarberDecision"] = (int)freeBarberDecision.Value;
            if (customerDecision.HasValue)
                payloadDict["customerDecision"] = (int)customerDecision.Value;
            else
                payloadDict["customerDecision"] = null;
            payloadDict["pendingExpiresAt"] = pendingExpiresAt;

            notification.PayloadJson = JsonSerializer.Serialize(payloadDict, _jsonOptions);
            return true;
        }

        private async Task PushNotificationSilentAsync(Guid userId, NotificationDto dto)
        {
            await realtime.PushNotificationSilentUpdateAsync(userId, dto);

            if (pushNotificationService != null)
            {
                try
                {
                    await pushNotificationService.SendPushNotificationAsync(userId, dto);
                }
                catch
                {
                    // FCM failure should not break notification update
                }
            }
        }

        private static NotificationDto MapToDto(Notification notification)
        {
            return new NotificationDto
            {
                Id = notification.Id,
                Type = notification.Type,
                AppointmentId = notification.AppointmentId,
                Title = notification.Title,
                Body = notification.Body,
                PayloadJson = notification.PayloadJson,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.IsRead
            };
        }

        #endregion
    }
}
