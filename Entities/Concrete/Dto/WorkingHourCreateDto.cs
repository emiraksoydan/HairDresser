using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{


    public class WorkingHourCreateDto : IDto
    {
        public DayOfWeek DayOfWeek { get; set; }
        public string StartTime { get; set; }      
        public string EndTime { get; set; }        
        public bool IsClosed { get; set; }
    }
}
