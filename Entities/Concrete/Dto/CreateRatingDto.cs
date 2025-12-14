using System;
using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete.Dto
{
    public class CreateRatingDto
    {
        [Required]
        public Guid AppointmentId { get; set; }

        [Required]
        public Guid TargetId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public double Score { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }
    }
}
