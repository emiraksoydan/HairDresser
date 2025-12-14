using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class RatingManager : IRatingService
    {
        private readonly IRatingDal _ratingDal;
        private readonly IAppointmentDal _appointmentDal;
        private readonly IUserDal _userDal;
        private readonly IBarberStoreDal _barberStoreDal;
        private readonly IFreeBarberDal _freeBarberDal;
        private readonly IManuelBarberDal _manuelBarberDal;

        public RatingManager(
            IRatingDal ratingDal,
            IAppointmentDal appointmentDal,
            IUserDal userDal,
            IBarberStoreDal barberStoreDal,
            IFreeBarberDal freeBarberDal,
            IManuelBarberDal manuelBarberDal)
        {
            _ratingDal = ratingDal;
            _appointmentDal = appointmentDal;
            _userDal = userDal;
            _barberStoreDal = barberStoreDal;
            _freeBarberDal = freeBarberDal;
            _manuelBarberDal = manuelBarberDal;
        }

        public async Task<IDataResult<RatingGetDto>> CreateRatingAsync(Guid userId, CreateRatingDto dto)
        {
            // Appointment kontrolü - sadece Completed veya Cancelled appointment'ler için rating yapılabilir
            var appointment = await _appointmentDal.Get(x => x.Id == dto.AppointmentId);
            if (appointment == null)
                return new ErrorDataResult<RatingGetDto>(Messages.AppointmentNotFound);

            if (appointment.Status != AppointmentStatus.Completed && appointment.Status != AppointmentStatus.Cancelled)
                return new ErrorDataResult<RatingGetDto>(Messages.RatingOnlyForCompleted);

            // Kullanıcının bu randevuya katılımcı olup olmadığını kontrol et
            if (appointment.CustomerUserId != userId && 
                appointment.BarberStoreUserId != userId && 
                appointment.FreeBarberUserId != userId)
                return new ErrorDataResult<RatingGetDto>(Messages.Unauthorized);

            // TargetId: Store ID, FreeBarber ID veya Customer UserId olabilir (ManuelBarber hariç)
            var isValidTarget = await ValidateTargetIdForRatingAsync(dto.TargetId, appointment);
            if (!isValidTarget)
                return new ErrorDataResult<RatingGetDto>(Messages.InvalidTargetForRating);

            // Mevcut rating'i kontrol et - eğer varsa bir daha eklenemez/güncellenemez
            var existingRating = await _ratingDal.GetByAppointmentAndTargetAsync(dto.AppointmentId, dto.TargetId, userId);
            if (existingRating != null)
                return new ErrorDataResult<RatingGetDto>("Bu randevu için bu hedefe zaten değerlendirme yaptınız. Değerlendirme güncellenemez.");

            // Yeni rating oluştur
            var rating = new Rating
            {
                Id = Guid.NewGuid(),
                AppointmentId = dto.AppointmentId,
                TargetId = dto.TargetId,
                RatedFromId = userId,
                Score = dto.Score,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _ratingDal.Add(rating);

            // DTO'ya map et
            var ratedFromUser = await _userDal.Get(x => x.Id == userId);
            var dtoResult = new RatingGetDto
            {
                Id = rating.Id,
                TargetId = rating.TargetId,
                RatedFromId = rating.RatedFromId,
                RatedFromName = ratedFromUser != null ? $"{ratedFromUser.FirstName} {ratedFromUser.LastName}" : null,
                RatedFromImage = ratedFromUser?.ImageId != null ? ratedFromUser.ImageId.ToString() : null,
                Score = rating.Score,
                Comment = rating.Comment,
                CreatedAt = rating.CreatedAt,
                UpdatedAt = rating.UpdatedAt,
                AppointmentId = rating.AppointmentId
            };

            return new SuccessDataResult<RatingGetDto>(dtoResult, Messages.RatingCreatedSuccess);
        }

        public async Task<IDataResult<bool>> DeleteRatingAsync(Guid userId, Guid ratingId)
        {
            var rating = await _ratingDal.Get(x => x.Id == ratingId);
            if (rating == null)
                return new ErrorDataResult<bool>("Değerlendirme bulunamadı.");

            if (rating.RatedFromId != userId)
                return new ErrorDataResult<bool>(Messages.Unauthorized);

            await _ratingDal.Remove(rating);
            return new SuccessDataResult<bool>(true, "Değerlendirme silindi.");
        }

        public async Task<IDataResult<RatingGetDto>> GetRatingByIdAsync(Guid ratingId)
        {
            var rating = await _ratingDal.Get(x => x.Id == ratingId);
            if (rating == null)
                return new ErrorDataResult<RatingGetDto>("Değerlendirme bulunamadı.");

            var ratedFromUser = await _userDal.Get(x => x.Id == rating.RatedFromId);
            var dto = new RatingGetDto
            {
                Id = rating.Id,
                TargetId = rating.TargetId,
                RatedFromId = rating.RatedFromId,
                RatedFromName = ratedFromUser != null ? $"{ratedFromUser.FirstName} {ratedFromUser.LastName}" : null,
                RatedFromImage = ratedFromUser?.ImageId != null ? ratedFromUser.ImageId.ToString() : null,
                Score = rating.Score,
                Comment = rating.Comment,
                CreatedAt = rating.CreatedAt,
                UpdatedAt = rating.UpdatedAt,
                AppointmentId = rating.AppointmentId
            };

            return new SuccessDataResult<RatingGetDto>(dto);
        }

        public async Task<IDataResult<List<RatingGetDto>>> GetRatingsByTargetAsync(Guid targetId)
        {
            var ratings = await _ratingDal.GetAll(x => x.TargetId == targetId);
            
            var userIds = ratings.Select(r => r.RatedFromId).Distinct().ToList();
            var users = await _userDal.GetAll(x => userIds.Contains(x.Id));

            var userDict = users.ToDictionary(u => u.Id, u => u);

            var dtos = ratings.Select(r =>
            {
                var user = userDict.GetValueOrDefault(r.RatedFromId);
                return new RatingGetDto
                {
                    Id = r.Id,
                    TargetId = r.TargetId,
                    RatedFromId = r.RatedFromId,
                    RatedFromName = user != null ? $"{user.FirstName} {user.LastName}" : null,
                    RatedFromImage = user?.ImageId != null ? user.ImageId.ToString() : null,
                    Score = r.Score,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    AppointmentId = r.AppointmentId
                };
            }).ToList();

            return new SuccessDataResult<List<RatingGetDto>>(dtos);
        }

        public async Task<IDataResult<RatingGetDto>> GetMyRatingForAppointmentAsync(Guid userId, Guid appointmentId, Guid targetId)
        {
            var rating = await _ratingDal.GetByAppointmentAndTargetAsync(appointmentId, targetId, userId);
            if (rating == null)
                return new ErrorDataResult<RatingGetDto>(null, "Değerlendirme bulunamadı.");

            var ratedFromUser = await _userDal.Get(x => x.Id == userId);
            var dto = new RatingGetDto
            {
                Id = rating.Id,
                TargetId = rating.TargetId,
                RatedFromId = rating.RatedFromId,
                RatedFromName = ratedFromUser != null ? $"{ratedFromUser.FirstName} {ratedFromUser.LastName}" : null,
                RatedFromImage = ratedFromUser?.ImageId != null ? ratedFromUser.ImageId.ToString() : null,
                Score = rating.Score,
                Comment = rating.Comment,
                CreatedAt = rating.CreatedAt,
                UpdatedAt = rating.UpdatedAt,
                AppointmentId = rating.AppointmentId
            };

            return new SuccessDataResult<RatingGetDto>(dto);
        }

        private async Task<bool> ValidateTargetIdForRatingAsync(Guid targetId, Appointment appointment)
        {
            // TargetId: Store ID, FreeBarber ID veya Customer UserId olabilir (ManuelBarber hariç)
            
            // Store ID kontrolü - targetId bir store ID olmalı
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            if (store != null && appointment.BarberStoreUserId.HasValue)
            {
                // Store'un bu appointment'a ait olduğunu kontrol et
                if (store.BarberStoreOwnerId == appointment.BarberStoreUserId.Value)
                    return true;
            }

            // FreeBarber ID kontrolü - targetId bir free barber ID olmalı
            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            if (freeBarber != null && appointment.FreeBarberUserId.HasValue)
            {
                // FreeBarber'ın bu appointment'a ait olduğunu kontrol et
                if (freeBarber.FreeBarberUserId == appointment.FreeBarberUserId.Value)
                    return true;
            }

            // Customer UserId kontrolü - targetId bir customer user ID olabilir
            if (appointment.CustomerUserId == targetId)
                return true;

            // ManuelBarber ID kontrolü - ManuelBarber'a rating yapılamaz
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            if (manuelBarber != null)
                return false; // ManuelBarber'a rating yapılamaz

            return false;
        }
    }
}
