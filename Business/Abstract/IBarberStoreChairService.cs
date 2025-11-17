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
    public interface IBarberStoreChairService
    {
        Task<IResult> AddAsync(BarberChairCreateDto dto, Guid storeOwnerId);
        Task<IResult> AddRangeAsync(List<BarberChair> list);

        Task<IResult> UpdateAsync(BarberChairUpdateDto dto);
        Task<IResult> DeleteAsync(Guid chairId);
        Task<IDataResult<List<BarberChairDto>>> GetAllByStoreAsync(Guid storeId);

        Task<IDataResult<BarberChairDto>> GetChairById(Guid chairId);
    }
}
