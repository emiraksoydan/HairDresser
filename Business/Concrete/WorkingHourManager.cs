using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class WorkingHourManager(IWorkingHourDal workingHourDal, IMapper mapper) : IWorkingHourService
    {
        public async Task<IResult> AddAsync(WorkingHourCreateDto dto)
        {
            var entities = dto.Adapt<WorkingHour>();
            await workingHourDal.Add(entities);
            return new SuccessResult("Çalışma saati başarıyla oluşturuldu.");
        }
        public async Task<IResult> AddRangeAsync(List<WorkingHour> list)
        {
             await workingHourDal.AddRange(list);
            return new SuccessResult("Çalışma saatleri başarıyla oluşturuldu.");
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            var existing = await workingHourDal.Get(x => x.Id == id);
            if (existing == null)
                return new ErrorResult("Çalışma saati bulunamadı.");

            await workingHourDal.Remove(existing);
            return new SuccessResult("Silindi.");
        }

        public async Task<IDataResult<List<WorkingHourDto>>> GetByTargetAsync(Guid targetId)
        {
            var list = await workingHourDal.GetAll(x => x.OwnerId == targetId);
            var dto = list.Adapt<List<WorkingHourDto>>();
            return new SuccessDataResult<List<WorkingHourDto>>(dto);
        }

        public async Task<IResult> UpdateAsync(WorkingHourUpdateDto dto)
        {
            var existing = await workingHourDal.Get(x => x.Id == dto.Id);
            if (existing == null)
                return new ErrorResult("Çalışma saati bulunamadı.");

            dto.Adapt(existing);
            await workingHourDal.Update(existing);
            return new SuccessResult("Güncellendi.");
        }
    }
}
