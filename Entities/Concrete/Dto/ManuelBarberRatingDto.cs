using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ManuelBarberRatingDto : IDto
    {
        public Guid BarberId { get; set; }
        public string BarberName { get; set; }
        public double Rating { get; set; }

    }
}
