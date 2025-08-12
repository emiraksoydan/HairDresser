using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class ManuelBarber : IEntity
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProfileImageUrl { get; set; }
        public double Rating { get; set; } 
        public bool IsActive { get; set; } = true;
        public Guid StoreId { get; set; }

        [NotMapped]                 
        public Guid TempId { get; set; }

    }
}
