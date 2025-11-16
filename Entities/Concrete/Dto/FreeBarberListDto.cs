using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class FreeBarberListDto : IDto
    {
        public Guid Id { get; set; }
        public string FreeBarberImageUrl { get; set; }
        public string FullName { get; set; }
        public BarberType Type { get; set; }
        public double Rating { get; set; }
        public int FavoriteCount { get; set; }
        [NotMapped]
        public bool IsAvailable { get; set; }
        public double DistanceKm { get; set; }
        public int ReviewCount { get; set; }
        public List<ServiceOfferingGetDto> ServiceOfferings { get; set; }
    }
}
