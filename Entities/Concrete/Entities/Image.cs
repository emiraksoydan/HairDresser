using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    public class Image : IEntity
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; }
        public ImageOwnerType OwnerType { get; set; }
        public Guid ImageOwnerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }
}
