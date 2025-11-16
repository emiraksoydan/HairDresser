using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{


    public class ManuelBarberCreateDto : IDto
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        public string? ProfileImageUrl { get; set; }

    }
}
