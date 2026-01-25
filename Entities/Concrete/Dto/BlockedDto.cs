using System;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Engelleme oluşturma DTO
    /// </summary>
    public class CreateBlockedDto
    {
        public Guid BlockedToUserId { get; set; }
        public string BlockReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Engelleme kaldırma DTO
    /// </summary>
    public class UnblockDto
    {
        public Guid BlockedToUserId { get; set; }
    }

    /// <summary>
    /// Engelleme görüntüleme DTO
    /// </summary>
    public class BlockedGetDto
    {
        public Guid Id { get; set; }
        public Guid BlockedFromUserId { get; set; }
        public Guid BlockedToUserId { get; set; }
        public string BlockReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Engellenen kullanıcı bilgisi
        public string? TargetUserName { get; set; }
        public string? TargetUserImage { get; set; }
        public UserType? TargetUserType { get; set; }
    }

    /// <summary>
    /// Engelleme durumu kontrol DTO
    /// </summary>
    public class BlockStatusDto
    {
        public bool IsBlocked { get; set; }
        public bool IsBlockedBy { get; set; }  // Karşı taraf bizi engellemiş mi
    }
}
