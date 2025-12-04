using Entities.Abstract;
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
    }
}
