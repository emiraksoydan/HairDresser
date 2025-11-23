using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class BarberChairUpdateDto : IDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid? BarberId { get; set; }
    }
}
