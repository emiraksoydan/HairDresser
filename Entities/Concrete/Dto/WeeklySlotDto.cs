using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class WeeklySlotDto : IDto
    {
        public DateTime Date { get; set; }            
        public string DayName { get; set; }   
        public List<ChairSlotDto> Chairs { get; set; }
    }

}
