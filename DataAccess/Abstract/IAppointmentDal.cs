using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IAppointmentDal : IEntityRepository<Appointment>
    {
        Task<List<ChairSlotDto>> GetAvailibilitySlot(Guid storeId,DateOnly dateOnly,CancellationToken ct = default);

        Task<List<AppointmentGetDto>> GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter appointmentFilter);

    }
}
