using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IManuelBarberService
    {
        Task<IResult> AddAsync(ManuelBarberCreateDto dto, Guid storeOwnerId);
        Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto, Guid storeOwnerId);
        Task<IResult> DeleteAsync(Guid id);
        Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeOwnerId);
    }

}
