using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IComplaintDal : IEntityRepository<Complaint>
    {
        Task<bool> ExistsAsync(Guid complaintFromUserId, Guid complaintToUserId, Guid? appointmentId);
        Task<List<Complaint>> GetByUserAsync(Guid userId);
    }
}
