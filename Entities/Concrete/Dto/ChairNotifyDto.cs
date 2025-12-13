using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ChairNotifyDto : IDto
    {
        public Guid ChairId { get; set; }
        public string? ChairName { get; set; }
        public Guid? ManuelBarberId { get; set; }
        public string? ManuelBarberName { get; set; }
        public string? ManuelBarberImageUrl { get; set; }
    }
}
