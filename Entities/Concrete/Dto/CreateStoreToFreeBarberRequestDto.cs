using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class CreateStoreToFreeBarberRequestDto : IDto
    {
        public Guid StoreId { get; set; }
        public Guid FreeBarberUserId { get; set; }
    }
}
