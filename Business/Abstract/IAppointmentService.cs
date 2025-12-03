using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IAppointmentService
    {
        Task<IDataResult<bool>> AnyControl(Guid id);
        Task<IDataResult<bool>> AnyChairControl(Guid id);
        Task<IDataResult<bool>> AnyStoreControl(Guid id);
        Task<IDataResult<bool>> AnyManuelBarberControl(Guid id);
        Task<IDataResult<List<ChairSlotDto>>> GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken ct = default);


        Task<IDataResult<Guid>> CreateCustomerToStoreAndFreeBarberControlAsync(Guid customerUserId, CreateAppointmentRequestDto req);
        Task<IDataResult<Guid>> CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req);
        Task<IDataResult<Guid>> CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateAppointmentRequestDto req);
        Task<IDataResult<bool>> StoreDecisionAsync(Guid storeOwnerUserId, Guid appointmentId, bool approve);
        Task<IDataResult<bool>> FreeBarberDecisionAsync(Guid freeBarberUserId, Guid appointmentId, bool approve);
        Task<IDataResult<bool>> CancelAsync(Guid userId, Guid appointmentId);
        Task<IDataResult<bool>> CompleteAsync(Guid storeOwnerUserId, Guid appointmentId);

    }
}
