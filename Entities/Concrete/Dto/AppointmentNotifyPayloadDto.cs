using Entities.Abstract;
using Entities.Concrete.Enums;
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

        // Status bilgileri - Frontend'de filtreleme için gerekli
        public AppointmentStatus? Status { get; set; }
        public DecisionStatus? StoreDecision { get; set; }
        public DecisionStatus? FreeBarberDecision { get; set; }

        // Service offerings - Frontend'de hizmet butonlarını göstermek için
        public List<ServiceOfferingGetDto>? ServiceOfferings { get; set; }

        public object? Extra { get; set; }              
    }
}
