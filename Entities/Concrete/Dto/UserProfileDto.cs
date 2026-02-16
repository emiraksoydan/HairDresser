using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class UserProfileDto : IDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public UserType UserType { get; set; }
        public string CustomerNumber { get; set; } // Müşteri numarası
        public Guid? ImageId { get; set; }
        public ImageGetDto Image { get; set; }
        public bool IsActive { get; set; }
        public bool IsKvkkApproved { get; set; }
    }
}
