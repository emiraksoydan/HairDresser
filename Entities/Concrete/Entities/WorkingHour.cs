using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class WorkingHour : IEntity
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }       
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsClosed { get; set; }
    }
}
