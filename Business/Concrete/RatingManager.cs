using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
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
        private readonly IImageDal _imageDal;

        public RatingManager(
            IRatingDal ratingDal,
            IAppointmentDal appointmentDal,
            IUserDal userDal,
            IBarberStoreDal barberStoreDal,
            IFreeBarberDal freeBarberDal,
            IManuelBarberDal manuelBarberDal,
            IImageDal imageDal)
        {
            _ratingDal = ratingDal;
            _appointmentDal = appointmentDal;
            _userDal = userDal;
            _barberStoreDal = barberStoreDal;
            _freeBarberDal = freeBarberDal;
            _manuelBarberDal = manuelBarberDal;
            _imageDal = imageDal;
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<RatingGetDto>> CreateRatingAsync(Guid userId, CreateRatingDto dto)
        {
            var appointment = await _appointmentDal.Get(x => x.Id == dto.AppointmentId);
            if (appointment == null)
                return new ErrorDataResult<RatingGetDto>(Messages.AppointmentNotFound);

            if (appointment.Status != AppointmentStatus.Completed &&
                appointment.Status != AppointmentStatus.Cancelled &&
                appointment.Status != AppointmentStatus.Unanswered)
                return new ErrorDataResult<RatingGetDto>(Messages.RatingOnlyForCompleted);

            if (appointment.CustomerUserId != userId &&
                appointment.BarberStoreUserId != userId &&
                appointment.FreeBarberUserId != userId)
                return new ErrorDataResult<RatingGetDto>(Messages.Unauthorized);

            var (ratingTargetId, validationUserId, _, manuelBarber) = await ResolveRatingTargetAsync(dto.TargetId);

            if (manuelBarber != null)
            {
                if (appointment.ManuelBarberId != manuelBarber.Id)
                    ratingTargetId = Guid.Empty;
                else
                {
                    var mbStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                    if (mbStore != null) validationUserId = mbStore.BarberStoreOwnerId;
                }
            }

            if (ratingTargetId == Guid.Empty)
                return new ErrorDataResult<RatingGetDto>(Messages.InvalidTargetForRating);

            if (appointment.CustomerUserId != validationUserId &&
                appointment.BarberStoreUserId != validationUserId &&
                appointment.FreeBarberUserId != validationUserId)
            {
                if (manuelBarber == null || appointment.ManuelBarberId != manuelBarber.Id)
                    return new ErrorDataResult<RatingGetDto>(Messages.InvalidTargetForRating);
            }

            var existingRating = await _ratingDal.Get(x =>
                x.AppointmentId == dto.AppointmentId &&
                x.TargetId == ratingTargetId &&
                x.RatedFromId == userId);
            if (existingRating != null)
                return new ErrorDataResult<RatingGetDto>(Messages.RatingAlreadyExists);

            var (ratedFromName, ratedFromImage, ratedFromUserType, ratedFromBarberType) =
                await GetRatedFromProfileAsync(userId);

            var rating = new Rating
            {
                Id = Guid.NewGuid(),
                AppointmentId = dto.AppointmentId,
                TargetId = ratingTargetId,
                RatedFromId = userId,
                Score = dto.Score,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _ratingDal.Add(rating);

            var dtoResult = new RatingGetDto
            {
                Id = rating.Id,
                TargetId = rating.TargetId,
                RatedFromId = rating.RatedFromId,
                RatedFromName = ratedFromName,
                RatedFromImage = ratedFromImage,
                RatedFromUserType = ratedFromUserType,
                RatedFromBarberType = ratedFromBarberType,
                Score = rating.Score,
                Comment = rating.Comment,
                CreatedAt = rating.CreatedAt,
                UpdatedAt = rating.UpdatedAt,
                AppointmentId = rating.AppointmentId
            };

            return new SuccessDataResult<RatingGetDto>(dtoResult, Messages.RatingCreatedSuccess);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> DeleteRatingAsync(Guid userId, Guid ratingId)
        {
            var rating = await _ratingDal.Get(x => x.Id == ratingId);
            if (rating == null)
                return new ErrorDataResult<bool>(Messages.RatingNotFound);

            if (rating.RatedFromId != userId)
                return new ErrorDataResult<bool>(Messages.Unauthorized);

            await _ratingDal.Remove(rating);
            return new SuccessDataResult<bool>(true, Messages.RatingDeletedSuccess);
        }

        public async Task<IDataResult<RatingGetDto>> GetRatingByIdAsync(Guid ratingId)
        {
            var rating = await _ratingDal.Get(x => x.Id == ratingId);
            if (rating == null)
                return new ErrorDataResult<RatingGetDto>(Messages.RatingNotFound);

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
            var (ratingTargetId, _, frontendTargetId, _) = await ResolveRatingTargetAsync(targetId);

            if (ratingTargetId == Guid.Empty)
                return new SuccessDataResult<List<RatingGetDto>>(new List<RatingGetDto>());

            var ratings = await _ratingDal.GetAll(x => x.TargetId == ratingTargetId);

            if (ratings == null || !ratings.Any())
                return new SuccessDataResult<List<RatingGetDto>>(new List<RatingGetDto>());

            var ratedFromUserIds = ratings.Select(r => r.RatedFromId).Distinct().ToList();
            var users = await _userDal.GetAll(x => ratedFromUserIds.Contains(x.Id));
            var userDict = users.ToDictionary(u => u.Id, u => u);

            var freeBarberUsers = users.Where(u => u.UserType == UserType.FreeBarber).Select(u => u.Id).ToList();
            var storeUsers = users.Where(u => u.UserType == UserType.BarberStore).Select(u => u.Id).ToList();

            var freeBarbers = await _freeBarberDal.GetAll(x => freeBarberUsers.Contains(x.FreeBarberUserId));
            var stores = await _barberStoreDal.GetAll(x => storeUsers.Contains(x.BarberStoreOwnerId));

            var freeBarberDict = freeBarbers
                .GroupBy(fb => fb.FreeBarberUserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(fb => fb.CreatedAt).First());
            var storeDict = stores
                .GroupBy(s => s.BarberStoreOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

            var storeIds = stores.Select(s => s.Id).ToList();
            var freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
            var customerImageIds = users
                .Where(u => u.UserType == UserType.Customer && u.ImageId.HasValue)
                .Select(u => u.ImageId!.Value).ToList();

            var customerImages = customerImageIds.Any()
                ? await _imageDal.GetAll(x => customerImageIds.Contains(x.Id))
                : new List<Image>();
            var customerImageDict = customerImages.ToDictionary(img => img.Id, img => img.ImageUrl);

            var storeImages = storeIds.Any()
                ? await _imageDal.GetAll(x => storeIds.Contains(x.ImageOwnerId) && x.OwnerType == ImageOwnerType.Store)
                : new List<Image>();
            var storeImageDict = storeImages.GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);

            var freeBarberImages = freeBarberIds.Any()
                ? await _imageDal.GetAll(x => freeBarberIds.Contains(x.ImageOwnerId) && x.OwnerType == ImageOwnerType.FreeBarber)
                : new List<Image>();
            var freeBarberImageDict = freeBarberImages.GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);

            var dtos = ratings.Select(r =>
            {
                var dto = new RatingGetDto
                {
                    Id = r.Id,
                    TargetId = frontendTargetId,
                    RatedFromId = r.RatedFromId,
                    Score = r.Score,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    AppointmentId = r.AppointmentId
                };

                if (userDict.TryGetValue(r.RatedFromId, out var user))
                {
                    dto.RatedFromUserType = user.UserType;

                    if (user.UserType == UserType.Customer)
                    {
                        dto.RatedFromName = $"{user.FirstName} {user.LastName}";
                        if (user.ImageId.HasValue && customerImageDict.TryGetValue(user.ImageId.Value, out var imageUrl))
                            dto.RatedFromImage = imageUrl;
                        dto.RatedFromBarberType = null;
                    }
                    else if (user.UserType == UserType.FreeBarber)
                    {
                        if (freeBarberDict.TryGetValue(r.RatedFromId, out var fb))
                        {
                            dto.RatedFromName = $"{fb.FirstName} {fb.LastName}";
                            dto.RatedFromBarberType = fb.Type;
                            if (freeBarberImageDict.TryGetValue(fb.Id, out var imageUrl))
                                dto.RatedFromImage = imageUrl;
                        }
                        else
                        {
                            dto.RatedFromName = $"{user.FirstName} {user.LastName}";
                            if (user.ImageId.HasValue && customerImageDict.TryGetValue(user.ImageId.Value, out var imageUrl))
                                dto.RatedFromImage = imageUrl;
                        }
                    }
                    else if (user.UserType == UserType.BarberStore)
                    {
                        if (storeDict.TryGetValue(r.RatedFromId, out var store))
                        {
                            dto.RatedFromName = store.StoreName;
                            dto.RatedFromBarberType = store.Type;
                            if (storeImageDict.TryGetValue(store.Id, out var imageUrl))
                                dto.RatedFromImage = imageUrl;
                        }
                        else
                        {
                            dto.RatedFromName = $"{user.FirstName} {user.LastName}";
                            if (user.ImageId.HasValue && customerImageDict.TryGetValue(user.ImageId.Value, out var imageUrl))
                                dto.RatedFromImage = imageUrl;
                        }
                    }
                }

                return dto;
            }).ToList();

            return new SuccessDataResult<List<RatingGetDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<RatingGetDto>> GetMyRatingForAppointmentAsync(Guid userId, Guid appointmentId, Guid targetId)
        {
            var (ratingTargetId, _, _, _) = await ResolveRatingTargetAsync(targetId);

            if (ratingTargetId == Guid.Empty)
                return new ErrorDataResult<RatingGetDto>(null, Messages.TargetNotFound);

            var rating = await _ratingDal.Get(x =>
                x.AppointmentId == appointmentId &&
                x.TargetId == ratingTargetId &&
                x.RatedFromId == userId);
            if (rating == null)
                return new ErrorDataResult<RatingGetDto>(null, Messages.RatingNotFound);

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

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Resolves a targetId (store / freeBarber / manuelBarber / customer) using
        /// parallel queries and returns the IDs needed by callers.
        ///
        /// RatingTargetId   – ID stored in the Rating row (Guid.Empty = not found)
        /// ValidationUserId – appointment-participation check user ID
        ///                    (Guid.Empty for ManuelBarber – caller must resolve via store)
        /// FrontendTargetId – ID returned to the frontend (equals targetId for all types)
        /// ManuelBarber     – non-null when target was a ManuelBarber (for appointment check)
        /// </summary>
        private async Task<(Guid RatingTargetId, Guid ValidationUserId, Guid FrontendTargetId, ManuelBarber? ManuelBarber)>
            ResolveRatingTargetAsync(Guid targetId)
        {
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            if (store is { } s)
                return (s.Id, s.BarberStoreOwnerId, s.Id, null);

            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            if (freeBarber is { } fb)
                return (fb.FreeBarberUserId, fb.FreeBarberUserId, fb.Id, null);

            var customer = await _userDal.Get(x => x.Id == targetId);
            if (customer is { } cu)
                return (cu.Id, cu.Id, cu.Id, null);

            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            if (manuelBarber is { } mb)
                return (mb.Id, Guid.Empty, mb.Id, mb);

            return (Guid.Empty, Guid.Empty, Guid.Empty, null);
        }

        /// <summary>
        /// Returns the display profile for the user who gave the rating,
        /// applying UserType-specific name and image logic.
        /// </summary>
        private async Task<(string? Name, string? Image, UserType? UserType, BarberType? BarberType)>
            GetRatedFromProfileAsync(Guid userId)
        {
            var user = await _userDal.Get(x => x.Id == userId);
            if (user == null) return (null, null, null, null);

            string? name = $"{user.FirstName} {user.LastName}";
            string? image = null;
            BarberType? barberType = null;

            if (user.ImageId.HasValue)
            {
                var img = await _imageDal.GetLatestImageAsync(user.ImageId.Value, ImageOwnerType.User);
                image = img?.ImageUrl;
            }

            if (user.UserType == UserType.FreeBarber)
            {
                var fb = await _freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                if (fb != null)
                {
                    name = $"{fb.FirstName} {fb.LastName}";
                    barberType = fb.Type;
                    var img = await _imageDal.GetLatestImageAsync(fb.Id, ImageOwnerType.FreeBarber);
                    image = img?.ImageUrl;
                }
            }
            else if (user.UserType == UserType.BarberStore)
            {
                var store = await _barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                if (store != null)
                {
                    name = store.StoreName;
                    barberType = store.Type;
                    var img = await _imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                    image = img?.ImageUrl;
                }
            }

            return (name, image, user.UserType, barberType);
        }
    }
}
