using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class UpdateLocationDto : IDto
    {
        // Id artık kullanılmıyor - CurrentUserId ile bulunuyor
        public double Latitude { get; set; }
        public double Longitude { get; set; }

    }
}
