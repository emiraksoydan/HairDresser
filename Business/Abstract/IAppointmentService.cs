using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface IAppointmentService
    {
        Task<IDataResult<Guid>> CustomerCreatesForFreeBarberAsync(Guid customerId, Guid freeBarberUserId, DateTime startUtc, DateTime endUtc, IEnumerable<Guid> serviceOfferingIds);
        Task<IDataResult<Guid>> CustomerCreatesForStoreAsync(Guid customerId, Guid chairId, Guid performerUserId, DateTime startUtc, DateTime endUtc, IEnumerable<Guid> serviceOfferingIds);
        Task<IDataResult<Guid>> FreeBarberToStoreAsync(Guid customerId, Guid chairId,DateTime startUtc, DateTime endUtc, IEnumerable<Guid> serviceOfferingIds);
        Task<IDataResult<Guid>> StoreInvitesBarberAsync(Guid storeOwnerUserId, Guid freeBarberUserId, Guid storeId, DateTime startUtc, DateTime endUtc);
        Task<IResult> ApproveAsync(Guid appointmentId, Guid userId, bool approve);
    }
}
