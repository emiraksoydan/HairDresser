using Business.Abstract;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;


namespace Business.Concrete
{
    public class ImageManager(IImageDal _imageDal) : IImageService
    {
        public async Task<IResult> AddAsync(CreateImageDto createImageDto)
        {
            var getImage = createImageDto.Adapt<Image>();
            await _imageDal.Add(getImage);
            return new SuccessResult();
        }

        public async Task<IResult> AddRangeAsync(List<CreateImageDto> list)
        {
            var imageEntities = list.Adapt<List<Image>>();

            await _imageDal.AddRange(imageEntities);
            return new SuccessResult();
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            var getImage = await _imageDal.Get(i=>i.Id == id);
            await _imageDal.Remove(getImage);
            return new SuccessResult();
        }

        public async Task<IDataResult<ImageGetDto>> GetImage(Guid id)
        {
            var image = await _imageDal.Get(x => x.ImageOwnerId == id);
            if (image == null)
                return new ErrorDataResult<ImageGetDto>("Resim bulunamadı.");

            var dto = image.Adapt<ImageGetDto>();

            return new SuccessDataResult<ImageGetDto>(dto);
        }

        public async Task<IResult> UpdateAsync(UpdateImageDto updateImageDto)
        {
            var entity = await _imageDal.Get(i => i.Id == updateImageDto.Id);
            if (entity == null)
                return new ErrorResult("Resim bulunamadı.");

            updateImageDto.Adapt(entity);
            await _imageDal.Update(entity);
            return new SuccessResult();
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateRangeAsync(List<UpdateImageDto> list)
        {
            if (list == null || list.Count == 0)
                return new SuccessResult();
            var updateDtos = list
                .Where(d => d.Id != Guid.Empty)
                .ToList();
            var newDtos = list
                .Where(d => d.Id == Guid.Empty)
                .ToList();

            List<Image> existingImages = new();
            if (updateDtos.Any())
            {
                var updateIds = updateDtos.Select(d => d.Id).ToList();
                existingImages = await _imageDal.GetAll(x => updateIds.Contains(x.Id));
            }
            var imageDict = existingImages.ToDictionary(x => x.Id);
            foreach (var dto in updateDtos)
            {
                if (!imageDict.TryGetValue(dto.Id, out var entity))
                    continue;

                dto.Adapt(entity);
            }
            if (existingImages.Any())
            {
                await _imageDal.UpdateRange(existingImages);
            }
            if (newDtos.Any())
            {
                var newEntities = newDtos.Adapt<List<Image>>();
                foreach (var entity in newEntities.Where(x=>x.Id == Guid.Empty))
                {
                    entity.Id = Guid.NewGuid();
                    entity.CreatedAt = DateTime.UtcNow;
                }

                await _imageDal.AddRange(newEntities);
            }
            return new SuccessResult();
        }
    }
}
