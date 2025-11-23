using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class WorkingHourUpdateDto : IDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public bool IsClosed { get; set; }
    }
}
