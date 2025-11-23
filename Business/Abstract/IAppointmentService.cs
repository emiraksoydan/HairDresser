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

    }
}
