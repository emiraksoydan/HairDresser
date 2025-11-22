using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class ManuelBarberUpdateDto  : IDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? ProfileImageUrl { get; set; }


    }
}
