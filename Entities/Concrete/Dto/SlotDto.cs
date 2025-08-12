using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class SlotDto : IDto
    {
        public Guid SlotId { get; set; }
        public string Start { get; set; } 
        public string End { get; set; }
        public bool IsBooked { get; set; }

        public bool IsPast { get; set; }

    }
}
