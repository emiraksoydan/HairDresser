using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class FreeBarber : IEntity
    {
        public Guid Id { get; set; }
        public Guid FreeBarberUserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public BarberType Type { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsAvailable { get; set; }
    

    }
}
