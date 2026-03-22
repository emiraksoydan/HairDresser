using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class StoreNotifyDto : IDto
    {
        public Guid StoreId { get; set; }
        public Guid StoreOwnerUserId { get; set; }
        public string? StoreName { get; set; }
        public string? ImageUrl { get; set; }
        public BarberType? Type { get; set; }
        public bool? IsInFavorites { get; set; } // Bu dükkan favorilerde mi?
        public string? AddressDescription { get; set; } // Dükkan adres açıklaması
        public string? StoreOwnerNumber { get; set; } // Dükkan sahibi numarası (kullanıcı no)
        public string? StoreNo { get; set; } // Dükkan özelinde benzersiz numara
    }
}
