using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IManuelBarberService
    {
        Task<IResult> AddAsync(ManuelBarberCreateDto dto);

        Task<IResult> AddRangeAsync(List<ManuelBarberCreateDto> list,Guid storeId);
        Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto);
        Task<IResult> DeleteAsync(Guid id);
        Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeOwnerId);
    }

}
