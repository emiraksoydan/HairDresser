using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    public class Rating : IEntity
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }     
        public RatingTargetType TargetType { get; set; }
        public Guid RatedFromId { get; set; }
        public User RatedFrom { get; set; }
        public double Score { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
