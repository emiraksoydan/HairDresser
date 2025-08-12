using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class BarberChair : IEntity
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public BarberStore Store { get; set; }
        public string? Name { get; set; }                
        public bool IsActive { get; set; } = true;
        public Guid? AssignedBarberUserId { get; set; }
        public User? AssignedBarberUser { get; set; }
        public Guid? ManualBarberId { get; set; }
        public ManuelBarber? ManualBarber { get; set; }
        public bool IsInternalEmployee { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
