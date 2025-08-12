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
        public ChairMode Type { get; set; }                

        public Guid? ManualBarberId { get; set; }    
        public Guid? ManualBarberTempId { get; set; }   
        public string? Name { get; set; }


    }
}
