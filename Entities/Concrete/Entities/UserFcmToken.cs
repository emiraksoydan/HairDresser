using Entities.Abstract;
using System;

namespace Entities.Concrete.Entities
{
    /// <summary>
    /// User FCM token entity for push notifications
    /// Supports multiple devices per user
    /// </summary>
    public class UserFcmToken : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }
        public string FcmToken { get; set; } = string.Empty;
        public string? DeviceId { get; set; } // Optional: Device identifier
        public string? Platform { get; set; } // "ios" or "android"
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true; // Token is active/invalid
    }
}

