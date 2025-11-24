using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class UpdateImageDto : IDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; }
        public Guid ImageOwnerId { get; set; }
        public ImageOwnerType? OwnerType { get; set; }
    }
}
