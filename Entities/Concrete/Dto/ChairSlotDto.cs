using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ChairSlotDto : IDto
    {
        public Guid ChairId { get; set; }
        public string? ChairName { get; set; }
        public string? BarberName { get; set; }
        public double? BarberRating { get; set; }
        public List<SlotDto> Slots { get; set; } = new();
    }
}
