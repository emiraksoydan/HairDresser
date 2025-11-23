using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface IWorkingHourService
    {
        Task<IResult> AddAsync(WorkingHourCreateDto dto);
        Task<IResult> AddRangeAsync(List<WorkingHour> dto);

        Task<IResult> UpdateRangeAsync(List<WorkingHourUpdateDto> dto);
        Task<IResult> UpdateAsync(WorkingHourUpdateDto dto);
        Task<IResult> DeleteAsync(Guid id);
        Task<IDataResult<List<WorkingHourDto>>> GetByTargetAsync(Guid targetId);
    }

}
