using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface ISlotService
    {
 
        Task<IDataResult<List<WeeklySlotDto>>> GetWeeklySlotsAsync(Guid storeId);

    }
}
