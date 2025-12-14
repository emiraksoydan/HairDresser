using System;
using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete.Dto
{
    public class ToggleFavoriteDto
    {
        [Required]
        public Guid TargetId { get; set; }
        
        // Opsiyonel: Randevu sayfasından geliyorsa appointmentId gönderilir
        public Guid? AppointmentId { get; set; }
    }
}
