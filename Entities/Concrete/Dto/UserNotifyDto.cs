using Entities.Abstract;
using System;
using Entities.Concrete.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class UserNotifyDto : IDto
    {
        public Guid UserId { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string RoleHint { get; set; }
        public BarberType? BarberType { get; set; } // FreeBarber tipi
        public bool? IsInFavorites { get; set; } // Bu kullanıcı favorilerde mi?
        public string CustomerNumber { get; set; } // Müşteri numarası
    }
}
