using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{


    public class BarberChairCreateDto : IDto
    {
        public string? BarberId { get; set; }
        public string Id { get; set; }
        public string? Name { get; set; }
    }
}
