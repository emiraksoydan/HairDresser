using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class AppointmentNotifyPayloadDto
    {
        public Guid AppointmentId { get; set; }
        public string EventKey { get; set; } = null!;     
        public string RecipientRole { get; set; } = null!; 
        public DateOnly Date { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public Guid? ActorUserId { get; set; }           
        public UserNotifyDto? Customer { get; set; }
        public UserNotifyDto? FreeBarber { get; set; }
        public StoreNotifyDto? Store { get; set; }
        public ChairNotifyDto? Chair { get; set; }

        public object? Extra { get; set; }              
    }
}
