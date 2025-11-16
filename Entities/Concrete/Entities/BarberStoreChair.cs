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

        public Guid? ManuelBarberId { get; set; }
        public string? Name { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
