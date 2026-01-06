using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class SettingGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public bool ShowImageAnimation { get; set; }
        // NotificationSoundUrl kaldırıldı - artık backend'deki varsayılan ses dosyası kullanılıyor
    }
}

