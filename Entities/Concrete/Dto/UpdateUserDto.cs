using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class UpdateUserDto : IDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
    }
}