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
    public class FreeBarberMinePanelDto : IDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public BarberType Type { get; set; }
        public double Rating { get; set; }
        public int FavoriteCount { get; set; }
        public bool IsAvailable { get; set; }
        public int ReviewCount { get; set; }
        public List<ImageGetDto> ImageList { get; set; }
        public List<ServiceOfferingGetDto> Offerings { get; set; }
    }
}
