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

            // TargetId: Store ID, FreeBarber ID, Customer UserId veya ManuelBarber ID olabilir
            // TargetId belirleme:
            // Store için: Store ID (her dükkanın kendi rating'i)
            // FreeBarber için: FreeBarber User ID (FreeBarber'ın rating'i)
            // Customer için: Customer User ID (Customer'ın rating'i)
            // ManuelBarber için: Store Owner User ID
            var store = await _barberStoreDal.Get(x => x.Id == dto.TargetId);
            var freeBarber = await _freeBarberDal.Get(x => x.Id == dto.TargetId);
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == dto.TargetId);
            var customerUser = await _userDal.Get(x => x.Id == dto.TargetId);
            
            Guid targetIdForRating = Guid.Empty; // Rating TargetId
            Guid targetUserIdForValidation = Guid.Empty; // Appointment validation için User ID
            
            if (store != null)
            {
                targetIdForRating = store.Id; // Store ID
                targetUserIdForValidation = store.BarberStoreOwnerId; // Validation için owner User ID
            }
            else if (freeBarber != null)
            {
                targetIdForRating = freeBarber.FreeBarberUserId; // FreeBarber User ID
                targetUserIdForValidation = freeBarber.FreeBarberUserId;
            }
            else if (customerUser != null)
            {
                targetIdForRating = customerUser.Id; // Customer User ID
                targetUserIdForValidation = customerUser.Id;
            }
            else if (manuelBarber != null)
            {
                var manuelBarberStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                if (manuelBarberStore != null)
                {
                    targetIdForRating = manuelBarberStore.BarberStoreOwnerId; // ManuelBarber için store owner User ID
                    targetUserIdForValidation = manuelBarberStore.BarberStoreOwnerId;
                }
            }
            
            if (targetIdForRating == Guid.Empty)
                return new ErrorDataResult<RatingGetDto>(Messages.InvalidTargetForRating);
            
            // Appointment kontrolü - targetUserIdForValidation appointment'a katılımcı olmalı
            if (appointment.CustomerUserId != targetUserIdForValidation && 
                appointment.BarberStoreUserId != targetUserIdForValidation && 
                appointment.FreeBarberUserId != targetUserIdForValidation)
                return new ErrorDataResult<RatingGetDto>(Messages.InvalidTargetForRating);

            // Mevcut rating'i kontrol et
            // Store için: TargetId = Store ID
            // FreeBarber/Customer için: TargetId = User ID
            var existingRating = await _ratingDal.Get(x => x.AppointmentId == dto.AppointmentId && x.TargetId == targetIdForRating && x.RatedFromId == userId);
            if (existingRating != null)
                return new ErrorDataResult<RatingGetDto>("Bu randevu için bu hedefe zaten değerlendirme yaptınız. Değerlendirme güncellenemez.");

            // RatedFromId artık her zaman userId
            // RatedFrom bilgilerini kullanıcı tipine göre belirle
            UserType? ratedFromUserType = null;
            BarberType? ratedFromBarberType = null;
            string? ratedFromName = null;
            string? ratedFromImage = null;

            var currentUser = await _userDal.Get(x => x.Id == userId);
            if (currentUser != null)
            {
                ratedFromUserType = currentUser.UserType;
                ratedFromName = $"{currentUser.FirstName} {currentUser.LastName}";
                
                // Image URL'ini al
                if (currentUser.ImageId.HasValue)
                {
                    var image = await _imageDal.GetLatestImageAsync(currentUser.ImageId.Value, ImageOwnerType.User);
                    ratedFromImage = image?.ImageUrl;
                }
                
                if (currentUser.UserType == UserType.FreeBarber)
                {
                    var freeBarberFrom = await _freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                    if (freeBarberFrom != null)
                    {
                        ratedFromName = $"{freeBarberFrom.FirstName} {freeBarberFrom.LastName}";
                        ratedFromBarberType = freeBarberFrom.Type;
                        // Image URL'ini al - FreeBarber ID ile
                        var image = await _imageDal.GetLatestImageAsync(freeBarberFrom.Id, ImageOwnerType.FreeBarber);
                        ratedFromImage = image?.ImageUrl;
                    }
                }
                else if (currentUser.UserType == UserType.BarberStore)
                {
                    var storeFrom = await _barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                    if (storeFrom != null)
                    {
                        ratedFromName = storeFrom.StoreName;
                        ratedFromBarberType = storeFrom.Type;
                        // Image URL'ini al - Store ID ile
                        var image = await _imageDal.GetLatestImageAsync(storeFrom.Id, ImageOwnerType.Store);
                        ratedFromImage = image?.ImageUrl;
                    }
                }
            }

            // Yeni rating oluştur
            // Store için: TargetId = Store ID
            // FreeBarber/Customer için: TargetId = User ID
            var rating = new Rating
            {
                Id = Guid.NewGuid(),
                AppointmentId = dto.AppointmentId,
                TargetId = targetIdForRating, // Store ID veya User ID
                RatedFromId = userId, // Her zaman User ID
                Score = dto.Score,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _ratingDal.Add(rating);

            // DTO'ya map et
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
            // targetId Store ID, FreeBarber ID, Customer UserId veya ManuelBarber ID olabilir
            // TargetId belirleme:
            // Store için: Store ID (her dükkanın kendi rating'i)
            // FreeBarber için: FreeBarber User ID (FreeBarber'ın rating'i)
            // Customer için: Customer User ID (Customer'ın rating'i)
            // ManuelBarber için: Store Owner User ID
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            var customerUser = await _userDal.Get(x => x.Id == targetId);
            
            Guid targetIdForRating = Guid.Empty;
            
            if (store != null)
                targetIdForRating = store.Id; // Store ID
            else if (freeBarber != null)
                targetIdForRating = freeBarber.FreeBarberUserId; // FreeBarber User ID
            else if (customerUser != null)
                targetIdForRating = customerUser.Id; // Customer User ID
            else if (manuelBarber != null)
            {
                var manuelBarberStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                if (manuelBarberStore != null)
                    targetIdForRating = manuelBarberStore.BarberStoreOwnerId; // ManuelBarber için store owner User ID
            }
            
            if (targetIdForRating == Guid.Empty)
                return new SuccessDataResult<List<RatingGetDto>>(new List<RatingGetDto>());
            
            // Rating'leri getir
            // Store için: TargetId = Store ID
            // FreeBarber/Customer için: TargetId = User ID
            var ratings = await _ratingDal.GetAll(x => x.TargetId == targetIdForRating);
            
            // Eğer rating yoksa boş liste döndür
            if (ratings == null || !ratings.Any())
            {
                return new SuccessDataResult<List<RatingGetDto>>(new List<RatingGetDto>());
            }
            
            // RatedFromId artık her zaman User ID
            var ratedFromUserIds = ratings.Select(r => r.RatedFromId).Distinct().ToList();
            var users = await _userDal.GetAll(x => ratedFromUserIds.Contains(x.Id));
            var userDict = users.ToDictionary(u => u.Id, u => u);

            // FreeBarber ve Store bilgilerini de al (UserType'a göre)
            var freeBarberUsers = users.Where(u => u.UserType == UserType.FreeBarber).Select(u => u.Id).ToList();
            var storeUsers = users.Where(u => u.UserType == UserType.BarberStore).Select(u => u.Id).ToList();
            
            var freeBarbers = await _freeBarberDal.GetAll(x => freeBarberUsers.Contains(x.FreeBarberUserId));
            var stores = await _barberStoreDal.GetAll(x => storeUsers.Contains(x.BarberStoreOwnerId));
            
            // GroupBy kullanarak duplicate key'leri handle et
            var freeBarberDict = freeBarbers
                .GroupBy(fb => fb.FreeBarberUserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(fb => fb.CreatedAt).First());
            var storeDict = stores
                .GroupBy(s => s.BarberStoreOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

            // Image'leri çek
            // Customer için: User.ImageId -> Image.Id (ImageOwnerId = User ID, OwnerType = User olabilir ama genelde direkt Image.Id kullanılır)
            // Store için: ImageOwnerId = Store ID, OwnerType = Store
            // FreeBarber için: ImageOwnerId = FreeBarber ID, OwnerType = FreeBarber
            var storeIds = stores.Select(s => s.Id).ToList();
            var freeBarberIds = freeBarbers.Select(fb => fb.Id).ToList();
            var customerImageIds = users.Where(u => u.UserType == UserType.Customer && u.ImageId.HasValue).Select(u => u.ImageId!.Value).ToList();
            
            // Customer image'leri (User.ImageId -> Image.Id)
            var customerImages = customerImageIds.Any()
                ? await _imageDal.GetAll(x => customerImageIds.Contains(x.Id))
                : new List<Image>();
            var customerImageDict = customerImages.ToDictionary(img => img.Id, img => img.ImageUrl);
            
            // Store image'leri (ImageOwnerId = Store ID, OwnerType = Store) - En son eklenen image'i al
            var storeImages = storeIds.Any()
                ? await _imageDal.GetAll(x => storeIds.Contains(x.ImageOwnerId) && x.OwnerType == ImageOwnerType.Store)
                : new List<Image>();
            var storeImageDict = storeImages.GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);
            
            // FreeBarber image'leri (ImageOwnerId = FreeBarber ID, OwnerType = FreeBarber) - En son eklenen image'i al
            var freeBarberImages = freeBarberIds.Any()
                ? await _imageDal.GetAll(x => freeBarberIds.Contains(x.ImageOwnerId) && x.OwnerType == ImageOwnerType.FreeBarber)
                : new List<Image>();
            var freeBarberImageDict = freeBarberImages.GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);

            // TargetId frontend'e döndürülürken:
            // Store için: Store ID
            // FreeBarber için: FreeBarber ID (ama rating'te TargetId = FreeBarber User ID)
            // Customer için: Customer User ID
            // ManuelBarber için: ManuelBarber ID (ama rating'te TargetId = Store Owner User ID)
            Guid? targetIdForFrontend = null;
            if (store != null)
                targetIdForFrontend = store.Id; // Store ID
            else if (freeBarber != null)
                targetIdForFrontend = freeBarber.Id; // FreeBarber ID (frontend için)
            else if (customerUser != null)
                targetIdForFrontend = customerUser.Id; // Customer User ID
            else if (manuelBarber != null)
                targetIdForFrontend = manuelBarber.Id; // ManuelBarber ID (frontend için)
            
            var dtos = ratings.Select(r =>
            {
                RatingGetDto dto = new RatingGetDto
                {
                    Id = r.Id,
                    TargetId = targetIdForFrontend ?? targetId, // Frontend'e Store/FreeBarber/Customer/ManuelBarber ID döndür
                    RatedFromId = r.RatedFromId, // Her zaman User ID
                    Score = r.Score,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    AppointmentId = r.AppointmentId
                };

                // RatedFromId artık her zaman User ID, User tablosundan bilgileri al
                if (userDict.TryGetValue(r.RatedFromId, out var user))
                {
                    dto.RatedFromUserType = user.UserType;
                    
                    if (user.UserType == UserType.Customer)
                    {
                        dto.RatedFromName = $"{user.FirstName} {user.LastName}";
                        if (user.ImageId.HasValue && customerImageDict.TryGetValue(user.ImageId.Value, out var imageUrl))
                        {
                            dto.RatedFromImage = imageUrl;
                        }
                        dto.RatedFromBarberType = null;
                    }
                    else if (user.UserType == UserType.FreeBarber)
                    {
                        if (freeBarberDict.TryGetValue(r.RatedFromId, out var freeBarber))
                        {
                            dto.RatedFromName = $"{freeBarber.FirstName} {freeBarber.LastName}"; // FreeBarber'ın kendi ismi
                            dto.RatedFromBarberType = freeBarber.Type;
                            // FreeBarber image'ini FreeBarber ID'ye göre al (ImageOwnerId = FreeBarber ID)
                            if (freeBarberImageDict.TryGetValue(freeBarber.Id, out var imageUrl))
                            {
                                dto.RatedFromImage = imageUrl;
                            }
                        }
                        else
                        {
                            dto.RatedFromName = $"{user.FirstName} {user.LastName}";
                            if (user.ImageId.HasValue && customerImageDict.TryGetValue(user.ImageId.Value, out var imageUrl))
                            {
                                dto.RatedFromImage = imageUrl;
                            }
                        }
                    }
                    else if (user.UserType == UserType.BarberStore)
                    {
                        if (storeDict.TryGetValue(r.RatedFromId, out var store))
                        {
                            dto.RatedFromName = store.StoreName; // Store'un kendi ismi
                            dto.RatedFromBarberType = store.Type;
                            // Store image'ini Store ID'ye göre al (ImageOwnerId = Store ID)
                            if (storeImageDict.TryGetValue(store.Id, out var imageUrl))
                            {
                                dto.RatedFromImage = imageUrl;
                            }
                        }
                        else
                        {
                            dto.RatedFromName = $"{user.FirstName} {user.LastName}";
                            if (user.ImageId.HasValue && customerImageDict.TryGetValue(user.ImageId.Value, out var imageUrl))
                            {
                                dto.RatedFromImage = imageUrl;
                            }
                        }
                    }
                }

                return dto;
            }).ToList();

            return new SuccessDataResult<List<RatingGetDto>>(dtos);
        }

        public async Task<IDataResult<RatingGetDto>> GetMyRatingForAppointmentAsync(Guid userId, Guid appointmentId, Guid targetId)
        {
            // targetId Store ID, FreeBarber ID, Customer UserId veya ManuelBarber ID olabilir
            // TargetId belirleme:
            // Store için: Store ID (her dükkanın kendi rating'i)
            // FreeBarber için: FreeBarber User ID (FreeBarber'ın rating'i)
            // Customer için: Customer User ID (Customer'ın rating'i)
            // ManuelBarber için: Store Owner User ID
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            var customerUser = await _userDal.Get(x => x.Id == targetId);
            
            Guid targetIdForRating = Guid.Empty;
            
            if (store != null)
                targetIdForRating = store.Id; // Store ID
            else if (freeBarber != null)
                targetIdForRating = freeBarber.FreeBarberUserId; // FreeBarber User ID
            else if (customerUser != null)
                targetIdForRating = customerUser.Id; // Customer User ID
            else if (manuelBarber != null)
            {
                var manuelBarberStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                if (manuelBarberStore != null)
                    targetIdForRating = manuelBarberStore.BarberStoreOwnerId; // ManuelBarber için store owner User ID
            }
            
            if (targetIdForRating == Guid.Empty)
                return new ErrorDataResult<RatingGetDto>(null, "Hedef bulunamadı.");
            
            // Rating'i getir
            // Store için: TargetId = Store ID
            // FreeBarber/Customer için: TargetId = User ID
            var rating = await _ratingDal.Get(x => x.AppointmentId == appointmentId && x.TargetId == targetIdForRating && x.RatedFromId == userId);
            if (rating == null)
                return new ErrorDataResult<RatingGetDto>(null, "Değerlendirme bulunamadı.");

            // RatedFromId artık her zaman User ID
            var ratedFromUser = await _userDal.Get(x => x.Id == userId);
            var dto = new RatingGetDto
            {
                Id = rating.Id,
                TargetId = rating.TargetId, // Artık User ID, ama frontend'e Store/FreeBarber ID döndürmek için targetId'yi kullan
                RatedFromId = rating.RatedFromId, // Artık her zaman User ID
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
