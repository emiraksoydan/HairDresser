using Core.Utilities.Results;
using Entities.Concrete.Dto;


namespace Business.Abstract
{
    public interface IImageService
    {
        Task<IResult> AddAsync(CreateImageDto createImageDto);
        Task<IResult> AddRangeAsync(List<CreateImageDto> list);
        Task<IResult> UpdateAsync(UpdateImageDto updateImageDto);
        Task<IResult> DeleteAsync(Guid id);

        Task<IDataResult<ImageGetDto>> GetImage(Guid id);
   
    }
}
