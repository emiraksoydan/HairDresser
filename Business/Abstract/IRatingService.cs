using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRatingService
    {
        Task<IDataResult<RatingGetDto>> CreateRatingAsync(Guid userId, CreateRatingDto dto);
        Task<IDataResult<bool>> DeleteRatingAsync(Guid userId, Guid ratingId);
        Task<IDataResult<RatingGetDto>> GetRatingByIdAsync(Guid ratingId);
        Task<IDataResult<List<RatingGetDto>>> GetRatingsByTargetAsync(Guid targetId);
        Task<IDataResult<RatingGetDto>> GetMyRatingForAppointmentAsync(Guid userId, Guid appointmentId, Guid targetId);
    }
}
