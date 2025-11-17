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
    public interface IImageService
    {
        Task<IResult> AddAsync(CreateImageDto createImageDto);
        Task<IResult> AddRangeAsync(List<CreateImageDto> list);
        Task<IResult> UpdateAsync(UpdateImageDto updateImageDto);
        Task<IResult> DeleteAsync(Guid id);
   
    }
}
