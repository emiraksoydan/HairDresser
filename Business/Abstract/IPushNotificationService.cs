using Entities.Concrete.Dto;
using System;
using System.Threading.Tasks;

namespace Business.Abstract
{
    /// <summary>
    /// Push notification service interface for Firebase Cloud Messaging (FCM)
    /// Handles background notifications when app is closed
    /// </summary>
    public interface IPushNotificationService
    {
        /// <summary>
        /// Sends push notification to a user's device via FCM
        /// </summary>
        /// <param name="userId">Target user ID</param>
        /// <param name="notification">Notification data</param>
        /// <returns>True if sent successfully, false otherwise</returns>
        Task<bool> SendPushNotificationAsync(Guid userId, NotificationDto notification);

        /// <summary>
        /// Registers or updates FCM token for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="fcmToken">FCM device token</param>
        /// <param name="deviceId">Optional device identifier</param>
        /// <param name="platform">Optional platform (ios/android)</param>
        /// <returns>True if registered successfully</returns>
        Task<bool> RegisterFcmTokenAsync(Guid userId, string fcmToken, string? deviceId = null, string? platform = null);

        /// <summary>
        /// Unregisters FCM token for a user (logout, token refresh, etc.)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="fcmToken">FCM device token to remove</param>
        /// <returns>True if unregistered successfully</returns>
        Task<bool> UnregisterFcmTokenAsync(Guid userId, string fcmToken);
    }
}

