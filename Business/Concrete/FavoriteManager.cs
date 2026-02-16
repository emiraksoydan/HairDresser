using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
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
        private readonly IChatService _chatService;
        private readonly IChatThreadDal _threadDal;
        private readonly IRealTimePublisher _realtime;
        private readonly DatabaseContext _context;

        public FavoriteManager(
            IFavoriteDal favoriteDal,
            IUserDal userDal,
            IBarberStoreDal barberStoreDal,
            IFreeBarberDal freeBarberDal,
            IAppointmentDal appointmentDal,
            IManuelBarberDal manuelBarberDal,
            IChatService chatService,
            IChatThreadDal threadDal,
            IRealTimePublisher realtime,
            DatabaseContext context)
        {
            _favoriteDal = favoriteDal;
            _userDal = userDal;
            _barberStoreDal = barberStoreDal;
            _freeBarberDal = freeBarberDal;
            _appointmentDal = appointmentDal;
            _manuelBarberDal = manuelBarberDal;
            _chatService = chatService;
            _threadDal = threadDal;
            _realtime = realtime;
            _context = context;
        }

        // ─────────────────────────────────────────────────────────────────────
        // TOGGLE
        // ─────────────────────────────────────────────────────────────────────

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<ToggleFavoriteResponseDto>> ToggleFavoriteAsync(Guid userId, ToggleFavoriteDto dto)
        {
            var (favoritedToId, targetUserIdForThread, isStore) = await ResolveFavoriteTargetAsync(dto.TargetId);
            if (favoritedToId == Guid.Empty)
                return new ErrorDataResult<ToggleFavoriteResponseDto>(Messages.TargetUserNotFound);

            if (dto.AppointmentId.HasValue)
            {
                var appointment = await _appointmentDal.Get(x => x.Id == dto.AppointmentId.Value);
                if (appointment == null)
                    return new ErrorDataResult<ToggleFavoriteResponseDto>(Messages.AppointmentNotFound);

                if (appointment.Status != AppointmentStatus.Completed &&
                    appointment.Status != AppointmentStatus.Cancelled &&
                    appointment.Status != AppointmentStatus.Rejected &&
                    appointment.Status != AppointmentStatus.Unanswered)
                    return new ErrorDataResult<ToggleFavoriteResponseDto>(Messages.AppointmentMustBeCompletedForFavorite);
            }

            bool isSelfFavorite = targetUserIdForThread == userId;
            var existingFavorite = await _favoriteDal.Get(x => x.FavoritedFromId == userId && x.FavoritedToId == favoritedToId);

            if (existingFavorite != null)
            {
                existingFavorite.IsActive = !existingFavorite.IsActive;
                existingFavorite.UpdatedAt = DateTime.UtcNow;
                await _favoriteDal.Update(existingFavorite);

                if (existingFavorite.IsActive && !isSelfFavorite && targetUserIdForThread != Guid.Empty)
                {
                    await _context.SaveChangesAsync();
                    // Yeni kural: thread ancak karşı taraf da aktif favorisindeyse açılır
                    var reverseFavorite = await _favoriteDal.GetByUsersAsync(targetUserIdForThread, userId);
                    bool reverseActive = reverseFavorite?.IsActive == true;
                    if (!reverseActive && isStore)
                    {
                        var storeFavorite = await _favoriteDal.Get(x =>
                            x.FavoritedFromId == targetUserIdForThread &&
                            x.FavoritedToId == favoritedToId &&
                            x.IsActive);
                        reverseActive = storeFavorite != null;
                    }
                    if (reverseActive)
                        await _chatService.EnsureFavoriteThreadAsync(userId, targetUserIdForThread, storeId: null);
                }
                else if (!existingFavorite.IsActive && !isSelfFavorite && targetUserIdForThread != Guid.Empty)
                {
                    // Yeni kural: bir taraf bile favoriden çıkarırsa thread ikisi için de anında gizlenir
                    await _context.SaveChangesAsync();
                    var thread = await _threadDal.GetFavoriteThreadAsync(userId, targetUserIdForThread, storeId: null);
                    if (thread != null)
                    {
                        await _realtime.PushChatThreadRemovedAsync(userId, thread.Id);
                        await _realtime.PushChatThreadRemovedAsync(targetUserIdForThread, thread.Id);
                    }
                }

                int favoriteCount = await CountFavoritesAsync(favoritedToId);
                var message = existingFavorite.IsActive ? Messages.FavoriteAddedSuccess : Messages.FavoriteRemovedSuccess;
                return new SuccessDataResult<ToggleFavoriteResponseDto>(
                    new ToggleFavoriteResponseDto { IsFavorite = existingFavorite.IsActive, FavoriteCount = favoriteCount },
                    message);
            }
            else
            {
                var favorite = new Favorite
                {
                    Id = Guid.NewGuid(),
                    FavoritedFromId = userId,
                    FavoritedToId = favoritedToId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _favoriteDal.Add(favorite);

                if (!isSelfFavorite && targetUserIdForThread != Guid.Empty)
                {
                    await _context.SaveChangesAsync();
                    // Yeni kural: thread ancak karşı taraf da aktif favorisindeyse oluşturulur
                    var reverseFavorite = await _favoriteDal.GetByUsersAsync(targetUserIdForThread, userId);
                    bool reverseActive = reverseFavorite?.IsActive == true;
                    if (!reverseActive && isStore)
                    {
                        var storeFavorite = await _favoriteDal.Get(x =>
                            x.FavoritedFromId == targetUserIdForThread &&
                            x.FavoritedToId == favoritedToId &&
                            x.IsActive);
                        reverseActive = storeFavorite != null;
                    }
                    if (reverseActive)
                        await _chatService.EnsureFavoriteThreadAsync(userId, targetUserIdForThread, storeId: null);
                }

                int favoriteCount = await CountFavoritesAsync(favoritedToId);
                return new SuccessDataResult<ToggleFavoriteResponseDto>(
                    new ToggleFavoriteResponseDto { IsFavorite = true, FavoriteCount = favoriteCount },
                    Messages.FavoriteAddedSuccess);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // IS FAVORITE
        // ─────────────────────────────────────────────────────────────────────

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<bool>> IsFavoriteAsync(Guid userId, Guid targetId)
        {
            var (favoritedToId, _, _) = await ResolveFavoriteTargetAsync(targetId);
            if (favoritedToId == Guid.Empty)
                return new SuccessDataResult<bool>(false);

            var favorite = await _favoriteDal.Get(x =>
                x.FavoritedFromId == userId &&
                x.FavoritedToId == favoritedToId &&
                x.IsActive);
            return new SuccessDataResult<bool>(favorite != null);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET MY FAVORITES
        // ─────────────────────────────────────────────────────────────────────

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<FavoriteGetDto>>> GetMyFavoritesAsync(Guid userId)
        {
            // Sadece aktif favorileri getir
            // FavoritedToId: Store ID (Store için), FreeBarber User ID (FreeBarber için), Customer User ID (Customer için)
            var favorites = await _favoriteDal.GetAll(x => x.FavoritedFromId == userId && x.IsActive);

            if (!favorites.Any())
                return new SuccessDataResult<List<FavoriteGetDto>>(new List<FavoriteGetDto>());

            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            // Performance: HashSet kullanarak daha hızlı Contains kontrolü
            var favoriteToIds = favorites.Select(f => f.FavoritedToId).Distinct().ToHashSet();

            // Store ID'leri bul (BarberStores tablosunda var mı?)
            var storeEntities = await _barberStoreDal.GetAll(x => favoriteToIds.Contains(x.Id));
            var storeIds = storeEntities.Select(s => s.Id).ToHashSet();

            // User ID'leri bul (Store ID'leri hariç - FreeBarber ve Customer User ID'leri)
            var targetUserIds = favoriteToIds.Where(id => !storeIds.Contains(id)).ToList();
            var storeDetails = new Dictionary<Guid, BarberStoreGetDto>(); // Key: Store ID

            if (storeIds.Any())
            {
                var stores = await _context.BarberStores
                    .AsNoTracking()
                    .Where(s => storeIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.StoreName, s.Type, s.AddressDescription, s.PricingValue, s.PricingType, s.Latitude, s.Longitude, s.BarberStoreOwnerId })
                    .ToListAsync();

                // Performance: HashSet kullanarak daha hızlı Contains kontrolü
                var storeIdsSet = stores.Select(s => s.Id).ToHashSet();

                // Rating & ReviewCount - Artık TargetId Store ID (her dükkanın kendi rating'i)
                var ratingStats = await _context.Ratings
                    .AsNoTracking()
                    .Where(r => storeIdsSet.Contains(r.TargetId))
                    .GroupBy(r => r.TargetId)
                    .Select(g => new { StoreId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                    .ToListAsync();
                var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });

                // Favorite count (sadece aktif favoriler) - Artık Store ID'ye göre (her dükkanın kendi favori sayısı)
                var favoriteStats = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => storeIdsSet.Contains(f.FavoritedToId) && f.IsActive)
                    .GroupBy(f => f.FavoritedToId)
                    .Select(g => new { StoreId = g.Key, FavoriteCount = g.Count() })
                    .ToListAsync();
                var favoriteDict = favoriteStats.ToDictionary(x => x.StoreId, x => x.FavoriteCount);

                // Service Offerings
                var offeringGroups = await _context.ServiceOfferings
                    .AsNoTracking()
                    .Where(o => storeIdsSet.Contains(o.OwnerId))
                    .GroupBy(o => o.OwnerId)
                    .Select(g => new { OwnerId = g.Key, Offerings = g.Select(o => new ServiceOfferingGetDto { Id = o.Id, ServiceName = o.ServiceName, Price = o.Price }).ToList() })
                    .ToListAsync();
                var offeringDict = offeringGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

                // Working Hours
                var hourGroups = await _context.WorkingHours
                    .AsNoTracking()
                    .Where(w => storeIdsSet.Contains(w.OwnerId))
                    .GroupBy(w => w.OwnerId)
                    .Select(g => new { OwnerId = g.Key, Hours = g.ToList() })
                    .ToListAsync();
                var hoursDict = hourGroups.ToDictionary(x => x.OwnerId, x => x.Hours);

                // Images
                var imageGroups = await _context.Images
                    .AsNoTracking()
                    .Where(i => i.OwnerType == ImageOwnerType.Store && storeIdsSet.Contains(i.ImageOwnerId))
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
                        BarberStoreOwnerId = store.BarberStoreOwnerId,
                        StoreName = store.StoreName,
                        Type = store.Type,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                        ReviewCount = ratingInfo?.ReviewCount ?? 0,
                        FavoriteCount = favCount,
                        IsFavorited = true,
                        IsOpenNow = isOpenNow,
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        ImageList = images ?? new List<ImageGetDto>(),
                        AddressDescription = store.AddressDescription,
                        PricingType = store.PricingType.ToString(),
                        PricingValue = store.PricingValue,
                        Latitude = store.Latitude,
                        Longitude = store.Longitude,
                        DistanceKm = 0
                    };
                }
            }

            // FreeBarber'ları getir - owner user ID'lerine göre
            var freeBarberEntities = await _freeBarberDal.GetAll(x => targetUserIds.Contains(x.FreeBarberUserId));
            var freeBarberIds = freeBarberEntities.Select(fb => fb.Id).ToList();
            var freeBarberDetails = new Dictionary<Guid, FreeBarberGetDto>();

            if (freeBarberIds.Any())
            {
                var freeBarbers = await _context.FreeBarbers
                    .AsNoTracking()
                    .Where(fb => freeBarberIds.Contains(fb.Id))
                    .Select(fb => new { fb.Id, fb.FirstName, fb.LastName, fb.Type, fb.IsAvailable, fb.Latitude, fb.Longitude, fb.FreeBarberUserId, fb.BeautySalonCertificateImageId })
                    .ToListAsync();

                var fbIdsList = freeBarbers.Select(fb => fb.Id).ToList();

                var freeBarberOwnerIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();
                var fbRatingStats = await _context.Ratings
                    .AsNoTracking()
                    .Where(r => freeBarberOwnerIds.Contains(r.TargetId))
                    .GroupBy(r => r.TargetId)
                    .Select(g => new { OwnerUserId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                    .ToListAsync();
                var fbRatingDict = fbRatingStats.ToDictionary(x => x.OwnerUserId, x => new { x.AvgRating, x.ReviewCount });

                var fbFavoriteStats = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => freeBarberOwnerIds.Contains(f.FavoritedToId) && f.IsActive)
                    .GroupBy(f => f.FavoritedToId)
                    .Select(g => new { OwnerUserId = g.Key, FavoriteCount = g.Count() })
                    .ToListAsync();
                var fbFavoriteDict = fbFavoriteStats.ToDictionary(x => x.OwnerUserId, x => x.FavoriteCount);

                var fbOfferingGroups = await _context.ServiceOfferings
                    .AsNoTracking()
                    .Where(o => fbIdsList.Contains(o.OwnerId))
                    .GroupBy(o => o.OwnerId)
                    .Select(g => new { OwnerId = g.Key, Offerings = g.Select(o => new ServiceOfferingGetDto { Id = o.Id, ServiceName = o.ServiceName, Price = o.Price }).ToList() })
                    .ToListAsync();
                var fbOfferingDict = fbOfferingGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

                var fbImageGroups = await _context.Images
                    .AsNoTracking()
                    .Where(i => i.OwnerType == ImageOwnerType.FreeBarber && fbIdsList.Contains(i.ImageOwnerId))
                    .GroupBy(i => i.ImageOwnerId)
                    .Select(g => new { OwnerId = g.Key, Images = g.Select(i => new ImageGetDto { Id = i.Id, ImageUrl = i.ImageUrl }).ToList() })
                    .ToListAsync();
                var fbImageDict = fbImageGroups.ToDictionary(x => x.OwnerId, x => x.Images);

                foreach (var fb in freeBarbers)
                {
                    var freeBarberOwnerId = fb.FreeBarberUserId;
                    fbRatingDict.TryGetValue(freeBarberOwnerId, out var ratingInfo);
                    fbFavoriteDict.TryGetValue(freeBarberOwnerId, out var favCount);
                    fbOfferingDict.TryGetValue(fb.Id, out var offerings);
                    fbImageDict.TryGetValue(fb.Id, out var images);

                    freeBarberDetails[freeBarberOwnerId] = new FreeBarberGetDto
                    {
                        Id = fb.Id,
                        FreeBarberUserId = fb.FreeBarberUserId,
                        FullName = $"{fb.FirstName} {fb.LastName}",
                        Type = fb.Type,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                        ReviewCount = ratingInfo?.ReviewCount ?? 0,
                        FavoriteCount = favCount,
                        IsFavorited = true,
                        IsAvailable = fb.IsAvailable,
                        Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                        ImageList = images ?? new List<ImageGetDto>(),
                        Latitude = fb.Latitude,
                        Longitude = fb.Longitude,
                        DistanceKm = 0,
                        BeautySalonCertificateImageId = fb.BeautySalonCertificateImageId
                    };
                }
            }

            // ManuelBarber'ları getir - store owner user ID'lerine göre
            var allStores = await _barberStoreDal.GetAll(x => targetUserIds.Contains(x.BarberStoreOwnerId));
            var storeIdsForManuelBarbers = allStores.Select(s => s.Id).ToList();
            var manuelBarbers = await _manuelBarberDal.GetAll(x => storeIdsForManuelBarbers.Contains(x.StoreId));
            var manuelBarberDict = new Dictionary<Guid, Entities.Concrete.Entities.ManuelBarber>();
            foreach (var mb in manuelBarbers)
            {
                var store = allStores.FirstOrDefault(s => s.Id == mb.StoreId);
                if (store != null)
                    manuelBarberDict[store.BarberStoreOwnerId] = mb;
            }

            // Customer User'ları getir
            var customerUsers = await _userDal.GetAll(x => targetUserIds.Contains(x.Id));
            var customerUserDict = customerUsers.ToDictionary(u => u.Id, u => u);

            var customerUserIds = customerUsers.Select(u => u.Id).ToList();
            var customerRatingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => customerUserIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new { UserId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                .ToListAsync();
            var customerRatingDict = customerRatingStats.ToDictionary(x => x.UserId, x => new { x.AvgRating, x.ReviewCount });

            var customerFavoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => customerUserIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new { UserId = g.Key, FavoriteCount = g.Count() })
                .ToListAsync();
            var customerFavoriteDict = customerFavoriteStats.ToDictionary(x => x.UserId, x => x.FavoriteCount);

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

                if (storeDetails.TryGetValue(f.FavoritedToId, out var storeDetail))
                {
                    dto.TargetType = FavoriteTargetType.Store;
                    dto.TargetName = storeDetail.StoreName;
                    dto.Store = storeDetail;
                }
                else if (freeBarberDetails.TryGetValue(f.FavoritedToId, out var freeBarberDetail))
                {
                    dto.TargetType = FavoriteTargetType.FreeBarber;
                    dto.TargetName = freeBarberDetail.FullName;
                    dto.FreeBarber = freeBarberDetail;
                }
                else if (manuelBarberDict.TryGetValue(f.FavoritedToId, out var manuelBarber))
                {
                    dto.TargetType = FavoriteTargetType.ManuelBarber;
                    dto.TargetName = manuelBarber.FullName;
                    dto.ManuelBarber = new ManuelBarberFavoriteDto
                    {
                        Id = manuelBarber.Id,
                        FullName = manuelBarber.FullName,
                        ImageUrl = null
                    };
                }
                else if (customerUserDict.TryGetValue(f.FavoritedToId, out var customerUser))
                {
                    dto.TargetType = FavoriteTargetType.Customer;
                    dto.TargetName = $"{customerUser.FirstName} {customerUser.LastName}";
                    var imageUrl = customerUser.ImageId.HasValue && userImages.TryGetValue(customerUser.ImageId.Value, out var url) ? url : null;

                    customerRatingDict.TryGetValue(customerUser.Id, out var ratingInfo);
                    customerFavoriteDict.TryGetValue(customerUser.Id, out var favCount);

                    dto.Customer = new UserFavoriteDto
                    {
                        Id = customerUser.Id,
                        FirstName = customerUser.FirstName,
                        LastName = customerUser.LastName,
                        ImageUrl = imageUrl,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                        ReviewCount = ratingInfo?.ReviewCount ?? 0,
                        FavoriteCount = favCount
                    };
                }

                return dto;
            }).ToList();

            return new SuccessDataResult<List<FavoriteGetDto>>(dtos);
        }

        // ─────────────────────────────────────────────────────────────────────
        // REMOVE
        // ─────────────────────────────────────────────────────────────────────

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> RemoveFavoriteAsync(Guid userId, Guid targetId)
        {
            var (favoritedToId, _, _) = await ResolveFavoriteTargetAsync(targetId);
            if (favoritedToId == Guid.Empty)
                return new ErrorDataResult<bool>(Messages.FavoriteNotFound);

            var favorite = await _favoriteDal.Get(x => x.FavoritedFromId == userId && x.FavoritedToId == favoritedToId);
            if (favorite == null)
                return new ErrorDataResult<bool>(Messages.FavoriteNotFound);

            await _favoriteDal.Remove(favorite);
            return new SuccessDataResult<bool>(true, Messages.FavoriteRemovedSuccess);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 4 tabloyu sıralı sorgulayarak favori hedefini çözer.
        /// Döndürür: (FavoritedToId, TargetUserIdForThread, IsStore)
        /// Bulunamazsa (Guid.Empty, Guid.Empty, false) döner.
        /// </summary>
        private async Task<(Guid FavoritedToId, Guid TargetUserIdForThread, bool IsStore)> ResolveFavoriteTargetAsync(Guid targetId)
        {
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            if (store is { } s)
                return (s.Id, s.BarberStoreOwnerId, true);

            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            if (freeBarber is { } fb)
                return (fb.FreeBarberUserId, fb.FreeBarberUserId, false);

            var customer = await _userDal.Get(x => x.Id == targetId);
            if (customer is { } cu)
                return (cu.Id, cu.Id, false);

            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            if (manuelBarber is { } mb)
            {
                var mbStore = await _barberStoreDal.Get(x => x.Id == mb.StoreId);
                if (mbStore != null)
                    return (mbStore.BarberStoreOwnerId, mbStore.BarberStoreOwnerId, false);
            }

            return (Guid.Empty, Guid.Empty, false);
        }

        /// <summary>
        /// Belirli bir favoritedToId için aktif favori sayısını döner.
        /// </summary>
        private Task<int> CountFavoritesAsync(Guid favoritedToId)
            => _context.Favorites.CountAsync(f => f.FavoritedToId == favoritedToId && f.IsActive);
    }
}
