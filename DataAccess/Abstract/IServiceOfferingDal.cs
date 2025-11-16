using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IServiceOfferingDal : IEntityRepository<ServiceOffering>
    {
        Task<List<ServiceOfferingGetDto>> GetServiceOfferingsByIdAsync(Guid Id);

        Task<List<ServiceOffering>> GetServiceOfferingsByIdsAsync(IEnumerable<Guid> ids);



    }
}
