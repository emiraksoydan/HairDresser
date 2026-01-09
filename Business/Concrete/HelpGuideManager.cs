using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;

namespace Business.Concrete
{
    public class HelpGuideManager : IHelpGuideService
    {
        private readonly IHelpGuideDal _helpGuideDal;

        public HelpGuideManager(IHelpGuideDal helpGuideDal)
        {
            _helpGuideDal = helpGuideDal;
        }

        public async Task<IDataResult<List<HelpGuideGetDto>>> GetByUserTypeAsync(int userType)
        {
            var guides = await _helpGuideDal.GetByUserTypeAsync(userType);
            
            var dtos = guides.Select(g => new HelpGuideGetDto
            {
                Id = g.Id,
                UserType = g.UserType,
                Title = g.Title,
                Description = g.Description,
                Order = g.Order,
                IsActive = g.IsActive
            }).ToList();

            return new SuccessDataResult<List<HelpGuideGetDto>>(dtos);
        }

        public async Task<IDataResult<List<HelpGuideGetDto>>> GetActiveByUserTypeAsync(int userType)
        {
            var guides = await _helpGuideDal.GetActiveByUserTypeAsync(userType);
            
            var dtos = guides.Select(g => new HelpGuideGetDto
            {
                Id = g.Id,
                UserType = g.UserType,
                Title = g.Title,
                Description = g.Description,
                Order = g.Order,
                IsActive = g.IsActive
            }).ToList();

            return new SuccessDataResult<List<HelpGuideGetDto>>(dtos);
        }
    }
}
