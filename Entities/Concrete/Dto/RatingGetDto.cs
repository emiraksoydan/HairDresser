using Entities.Concrete.Enums;
using System;

namespace Entities.Concrete.Dto
{
    public class RatingGetDto
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }
        public Guid RatedFromId { get; set; }
        public string? RatedFromName { get; set; }
        public string? RatedFromImage { get; set; }
        public double Score { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid AppointmentId { get; set; }
        public UserType? RatedFromUserType { get; set; }
        public BarberType? RatedFromBarberType { get; set; }
    }
}
