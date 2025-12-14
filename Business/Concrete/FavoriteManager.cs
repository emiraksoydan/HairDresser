using Business.Abstract;
using Business.Resources;
using Core.Utilities.Helpers;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
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
    public class FavoriteManager : IFavoriteService
    {
        private readonly IFavoriteDal _favoriteDal;
        private readonly IUserDal _userDal;
        private readonly IBarberStoreDal _barberStoreDal;
        private readonly IFreeBarberDal _freeBarberDal;
        private readonly IAppointmentDal _appointmentDal;
        private readonly IManuelBarberDal _manuelBarberDal;
        private readonly DatabaseContext _context;

        public FavoriteManager(
            IFavoriteDal favoriteDal,
            IUserDal userDal,
            IBarberStoreDal barberStoreDal,
            IFreeBarberDal freeBarberDal,
            IAppointmentDal appointmentDal,
            IManuelBarberDal manuelBarberDal,
            DatabaseContext context)
        {
            _favoriteDal = favoriteDal;
            _userDal = userDal;
            _barberStoreDal = barberStoreDal;
            _freeBarberDal = freeBarberDal;
            _appointmentDal = appointmentDal;
            _manuelBarberDal = manuelBarberDal;
            _context = context;
        }

        public async Task<IDataResult<bool>> ToggleFavoriteAsync(Guid userId, ToggleFavoriteDto dto)
        {
            // TargetId: Store ID, FreeBarber ID, Customer UserId veya ManuelBarber ID olabilir
            // Önce Store ID kontrolü
            var store = await _barberStoreDal.Get(x => x.Id == dto.TargetId);
            var freeBarber = await _freeBarberDal.Get(x => x.Id == dto.TargetId);
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == dto.TargetId);
            var customerUser = await _userDal.Get(x => x.Id == dto.TargetId);
            
            // Eğer hiçbiri değilse hata
            if (store == null && freeBarber == null && manuelBarber == null && customerUser == null)
                return new ErrorDataResult<bool>(Messages.TargetUserNotFound);
            
            // Kendi kendine favori eklenemez
            if (dto.TargetId == userId)
                return new ErrorDataResult<bool>(Messages.CannotFavoriteYourself);

            // Eğer appointmentId varsa (randevu sayfasından geliyorsa), appointment kontrolü yap
            if (dto.AppointmentId.HasValue)
            {
                var appointment = await _appointmentDal.Get(x => x.Id == dto.AppointmentId.Value);
                if (appointment == null)
                    return new ErrorDataResult<bool>(Messages.AppointmentNotFound);

                // Randevu sayfasından geliyorsa, sadece Completed veya Cancelled olmalı
                if (appointment.Status != AppointmentStatus.Completed && appointment.Status != AppointmentStatus.Cancelled)
                    return new ErrorDataResult<bool>("Randevu sayfasından favorileme için randevunun tamamlanmış veya iptal edilmiş olması gerekir.");
            }

            // Mevcut favori kontrolü
            var existingFavorite = await _favoriteDal.GetByUsersAsync(userId, dto.TargetId);

            if (existingFavorite != null)
            {
                // Favori varsa sadece UpdatedAt'i güncelle (silme)
                existingFavorite.UpdatedAt = DateTime.UtcNow;
                await _favoriteDal.Update(existingFavorite);
                return new SuccessDataResult<bool>(true, Messages.FavoriteUpdatedSuccess);
            }
            else
            {
                // Favori yoksa ekle
                var favorite = new Favorite
                {
                    Id = Guid.NewGuid(),
                    FavoritedFromId = userId,
                    FavoritedToId = dto.TargetId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _favoriteDal.Add(favorite);
                return new SuccessDataResult<bool>(true, Messages.FavoriteAddedSuccess);
            }
        }

        public async Task<IDataResult<bool>> IsFavoriteAsync(Guid userId, Guid targetId)
        {
            var exists = await _favoriteDal.ExistsAsync(userId, targetId);
            return new SuccessDataResult<bool>(exists);
        }

        public async Task<IDataResult<List<FavoriteGetDto>>> GetMyFavoritesAsync(Guid userId)
        {
            var favorites = await _favoriteDal.GetAll(x => x.FavoritedFromId == userId);
            
            if (!favorites.Any())
                return new SuccessDataResult<List<FavoriteGetDto>>(new List<FavoriteGetDto>());

            var targetIds = favorites.Select(f => f.FavoritedToId).Distinct().ToList();
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            // Store'ları getir ve detaylarını çek
            var storeEntities = await _barberStoreDal.GetAll(x => targetIds.Contains(x.Id));
            var storeIds = storeEntities.Select(s => s.Id).ToList();
            var storeDetails = new Dictionary<Guid, BarberStoreGetDto>();
            
            if (storeIds.Any())
            {
                var stores = await _context.BarberStores
                    .AsNoTracking()
                    .Where(s => storeIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.StoreName, s.Type, s.AddressDescription, s.PricingValue, s.PricingType, s.Latitude, s.Longitude })
                    .ToListAsync();

                var storeIdsList = stores.Select(s => s.Id).ToList();

                // Rating & ReviewCount
                var ratingStats = await _context.Ratings
                    .AsNoTracking()
                    .Where(r => storeIdsList.Contains(r.TargetId))
                    .GroupBy(r => r.TargetId)
                    .Select(g => new { StoreId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                    .ToListAsync();
                var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });

                // Favorite count
                var favoriteStats = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => storeIdsList.Contains(f.FavoritedToId))
                    .GroupBy(f => f.FavoritedToId)
                    .Select(g => new { StoreId = g.Key, FavoriteCount = g.Count() })
                    .ToListAsync();
                var favoriteDict = favoriteStats.ToDictionary(x => x.StoreId, x => x.FavoriteCount);

                // Service Offerings
                var offeringGroups = await _context.ServiceOfferings
                    .AsNoTracking()
                    .Where(o => storeIdsList.Contains(o.OwnerId))
                    .GroupBy(o => o.OwnerId)
                    .Select(g => new { OwnerId = g.Key, Offerings = g.Select(o => new ServiceOfferingGetDto { Id = o.Id, ServiceName = o.ServiceName, Price = o.Price }).ToList() })
                    .ToListAsync();
                var offeringDict = offeringGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

                // Working Hours
                var hourGroups = await _context.WorkingHours
                    .AsNoTracking()
                    .Where(w => storeIdsList.Contains(w.OwnerId))
                    .GroupBy(w => w.OwnerId)
                    .Select(g => new { OwnerId = g.Key, Hours = g.ToList() })
                    .ToListAsync();
                var hoursDict = hourGroups.ToDictionary(x => x.OwnerId, x => x.Hours);

                // Images
                var imageGroups = await _context.Images
                    .AsNoTracking()
                    .Where(i => i.OwnerType == ImageOwnerType.Store && storeIdsList.Contains(i.ImageOwnerId))
                    .GroupBy(i => i.ImageOwnerId)
                    .Select(g => new { OwnerId = g.Key, Images = g.Select(i => new ImageGetDto { Id = i.Id, ImageUrl = i.ImageUrl }).ToList() })
                    .ToListAsync();
                var imageDict = imageGroups.ToDictionary(x => x.OwnerId, x => x.Images);

                foreach (var store in stores)
                {
                    ratingDict.TryGetValue(store.Id, out var ratingInfo);
                    favoriteDict.TryGetValue(store.Id, out var favCount);
                    offeringDict.TryGetValue(store.Id, out var offerings);
                    hoursDict.TryGetValue(store.Id, out var hours);
                    imageDict.TryGetValue(store.Id, out var images);

                    var isOpenNow = hours != null ? OpenControl.IsOpenNow(hours, nowLocal) : false;

                    storeDetails[store.Id] = new BarberStoreGetDto
                    {
                        Id = store.Id,
                        StoreName = store.StoreName,
                        Type = store.Type,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                        ReviewCount = ratingInfo?.ReviewCount ?? 0,
                        FavoriteCount = favCount,
                        IsOpenNow = isOpenNow,
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        ImageList = images ?? new List<ImageGetDto>(),
                        AddressDescription = store.AddressDescription,
                        PricingType = store.PricingType.ToString(),
                        PricingValue = store.PricingValue,
                        Latitude = store.Latitude,
                        Longitude = store.Longitude,
                        DistanceKm = 0 // Favoriler için distance hesaplanmıyor
                    };
                }
            }

            // FreeBarber'ları getir ve detaylarını çek
            var freeBarberEntities = await _freeBarberDal.GetAll(x => targetIds.Contains(x.Id));
            var freeBarberIds = freeBarberEntities.Select(fb => fb.Id).ToList();
            var freeBarberDetails = new Dictionary<Guid, FreeBarberGetDto>();

            if (freeBarberIds.Any())
            {
                var freeBarbers = await _context.FreeBarbers
                    .AsNoTracking()
                    .Where(fb => freeBarberIds.Contains(fb.Id))
                    .Select(fb => new { fb.Id, fb.FirstName, fb.LastName, fb.Type, fb.IsAvailable, fb.Latitude, fb.Longitude })
                    .ToListAsync();

                var fbIdsList = freeBarbers.Select(fb => fb.Id).ToList();

                // Rating & ReviewCount
                var fbRatingStats = await _context.Ratings
                    .AsNoTracking()
                    .Where(r => fbIdsList.Contains(r.TargetId))
                    .GroupBy(r => r.TargetId)
                    .Select(g => new { FreeBarberId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                    .ToListAsync();
                var fbRatingDict = fbRatingStats.ToDictionary(x => x.FreeBarberId, x => new { x.AvgRating, x.ReviewCount });

                // Favorite count
                var fbFavoriteStats = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => fbIdsList.Contains(f.FavoritedToId))
                    .GroupBy(f => f.FavoritedToId)
                    .Select(g => new { FreeBarberId = g.Key, FavoriteCount = g.Count() })
                    .ToListAsync();
                var fbFavoriteDict = fbFavoriteStats.ToDictionary(x => x.FreeBarberId, x => x.FavoriteCount);

                // Service Offerings
                var fbOfferingGroups = await _context.ServiceOfferings
                    .AsNoTracking()
                    .Where(o => fbIdsList.Contains(o.OwnerId))
                    .GroupBy(o => o.OwnerId)
                    .Select(g => new { OwnerId = g.Key, Offerings = g.Select(o => new ServiceOfferingGetDto { Id = o.Id, ServiceName = o.ServiceName, Price = o.Price }).ToList() })
                    .ToListAsync();
                var fbOfferingDict = fbOfferingGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

                // Images
                var fbImageGroups = await _context.Images
                    .AsNoTracking()
                    .Where(i => i.OwnerType == ImageOwnerType.FreeBarber && fbIdsList.Contains(i.ImageOwnerId))
                    .GroupBy(i => i.ImageOwnerId)
                    .Select(g => new { OwnerId = g.Key, Images = g.Select(i => new ImageGetDto { Id = i.Id, ImageUrl = i.ImageUrl }).ToList() })
                    .ToListAsync();
                var fbImageDict = fbImageGroups.ToDictionary(x => x.OwnerId, x => x.Images);

                foreach (var fb in freeBarbers)
                {
                    fbRatingDict.TryGetValue(fb.Id, out var ratingInfo);
                    fbFavoriteDict.TryGetValue(fb.Id, out var favCount);
                    fbOfferingDict.TryGetValue(fb.Id, out var offerings);
                    fbImageDict.TryGetValue(fb.Id, out var images);

                    freeBarberDetails[fb.Id] = new FreeBarberGetDto
                    {
                        Id = fb.Id,
                        FullName = $"{fb.FirstName} {fb.LastName}",
                        Type = fb.Type,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                        ReviewCount = ratingInfo?.ReviewCount ?? 0,
                        FavoriteCount = favCount,
                        IsAvailable = fb.IsAvailable,
                        Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                        ImageList = images ?? new List<ImageGetDto>(),
                        Latitude = fb.Latitude,
                        Longitude = fb.Longitude,
                        DistanceKm = 0 // Favoriler için distance hesaplanmıyor
                    };
                }
            }

            // ManuelBarber'ları getir
            var manuelBarbers = await _manuelBarberDal.GetAll(x => targetIds.Contains(x.Id));
            var manuelBarberDict = manuelBarbers.ToDictionary(mb => mb.Id, mb => mb);

            // Customer User'ları getir
            var customerUsers = await _userDal.GetAll(x => targetIds.Contains(x.Id));
            var customerUserDict = customerUsers.ToDictionary(u => u.Id, u => u);

            // Image'ları getir (Customer ve ManuelBarber için)
            var userImageIds = customerUsers.Where(u => u.ImageId.HasValue).Select(u => u.ImageId!.Value).ToList();
            var userImages = await _context.Images
                .AsNoTracking()
                .Where(i => userImageIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.ImageUrl);

            var dtos = favorites.Select(f =>
            {
                var dto = new FavoriteGetDto
                {
                    Id = f.Id,
                    FavoritedFromId = f.FavoritedFromId,
                    FavoritedToId = f.FavoritedToId,
                    CreatedAt = f.CreatedAt
                };

                // Store bilgisi
                if (storeDetails.TryGetValue(f.FavoritedToId, out var storeDetail))
                {
                    dto.TargetType = FavoriteTargetType.Store;
                    dto.TargetName = storeDetail.StoreName;
                    dto.Store = storeDetail;
                }
                // FreeBarber bilgisi
                else if (freeBarberDetails.TryGetValue(f.FavoritedToId, out var freeBarberDetail))
                {
                    dto.TargetType = FavoriteTargetType.FreeBarber;
                    dto.TargetName = freeBarberDetail.FullName;
                    dto.FreeBarber = freeBarberDetail;
                }
                // ManuelBarber bilgisi
                else if (manuelBarberDict.TryGetValue(f.FavoritedToId, out var manuelBarber))
                {
                    dto.TargetType = FavoriteTargetType.ManuelBarber;
                    dto.TargetName = manuelBarber.FullName;
                    dto.ManuelBarber = new ManuelBarberFavoriteDto
                    {
                        Id = manuelBarber.Id,
                        FullName = manuelBarber.FullName,
                        ImageUrl = null // ManuelBarber için image yok
                    };
                }
                // Customer User bilgisi
                else if (customerUserDict.TryGetValue(f.FavoritedToId, out var customerUser))
                {
                    dto.TargetType = FavoriteTargetType.Customer;
                    dto.TargetName = $"{customerUser.FirstName} {customerUser.LastName}";
                    var imageUrl = customerUser.ImageId.HasValue && userImages.TryGetValue(customerUser.ImageId.Value, out var url) ? url : null;
                    dto.Customer = new UserFavoriteDto
                    {
                        Id = customerUser.Id,
                        FirstName = customerUser.FirstName,
                        LastName = customerUser.LastName,
                        ImageUrl = imageUrl
                    };
                }

                return dto;
            }).ToList();

            return new SuccessDataResult<List<FavoriteGetDto>>(dtos);
        }

        public async Task<IDataResult<bool>> RemoveFavoriteAsync(Guid userId, Guid targetId)
        {
            var favorite = await _favoriteDal.GetByUsersAsync(userId, targetId);
            if (favorite == null)
                return new ErrorDataResult<bool>("Favori bulunamadı.");

            await _favoriteDal.Remove(favorite);
            return new SuccessDataResult<bool>(true, "Favoriden çıkarıldı.");
        }
    }
}
