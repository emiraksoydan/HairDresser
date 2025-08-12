using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class AddressInfo : IEntity
    {
        public Guid Id { get; set; }
        public string AddressLine { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
