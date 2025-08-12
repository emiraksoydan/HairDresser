using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class Favorite : IEntity
    {
        public Guid Id { get; set; }
        public Guid FavoritedFromId { get; set; } 
        public User FavoritedFrom { get; set; }
        public Guid FavoritedToId { get; set; }
        public User FavoritedTo { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
