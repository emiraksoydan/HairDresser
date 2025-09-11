using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class AppointmentServiceOffering : IEntity
    {
        public Guid Id { get; set; }
        public Guid AppointmentId { get; set; }
        public Guid ServiceOfferingId { get; set; }   
        public string ServiceName { get; set; } = string.Empty; 
        public decimal Price { get; set; }                     

        public Appointment Appointment { get; set; }
    }
}
