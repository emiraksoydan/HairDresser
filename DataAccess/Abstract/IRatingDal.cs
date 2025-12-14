using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IRatingDal : IEntityRepository<Rating>
    {
        Task<Rating> GetByAppointmentAndTargetAsync(Guid appointmentId, Guid targetId, Guid ratedFromId);
        Task<bool> ExistsAsync(Guid appointmentId, Guid targetId, Guid ratedFromId);
    }
}
