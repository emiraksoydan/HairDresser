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
        private readonly IChatService _chatService;
        private readonly DatabaseContext _context;

        public FavoriteManager(
            IFavoriteDal favoriteDal,
            IUserDal userDal,
            IBarberStoreDal barberStoreDal,
            IFreeBarberDal freeBarberDal,
            IAppointmentDal appointmentDal,
            IManuelBarberDal manuelBarberDal,
            IChatService chatService,
            DatabaseContext context)
        {
            _favoriteDal = favoriteDal;
            _userDal = userDal;
            _barberStoreDal = barberStoreDal;
            _freeBarberDal = freeBarberDal;
            _appointmentDal = appointmentDal;
            _manuelBarberDal = manuelBarberDal;
            _chatService = chatService;
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

            // Eğer appointmentId varsa (randevu sayfasından geliyorsa), appointment kontrolü yap
            if (dto.AppointmentId.HasValue)
            {
                var appointment = await _appointmentDal.Get(x => x.Id == dto.AppointmentId.Value);
                if (appointment == null)
                    return new ErrorDataResult<bool>(Messages.AppointmentNotFound);

                // Randevu sayfasından geliyorsa, sadece Completed veya Cancelled olmalı
                if (appointment.Status != AppointmentStatus.Completed && appointment.Status != AppointmentStatus.Cancelled && appointment.Status != AppointmentStatus.Rejected && appointment.Status != AppointmentStatus.Unanswered)
                    return new ErrorDataResult<bool>("Randevu sayfasından favorileme için randevunuzun sonuçlanması gerekir.");
            }

            // FavoritedToId belirleme:
            // Store için: Store ID (her dükkanın kendi favori sayısı)
            // FreeBarber için: FreeBarber User ID (FreeBarber'ın favori sayısı)
            // Customer için: Customer User ID (Customer'ın favori sayısı)
            // ManuelBarber için: Store Owner User ID (thread için)
            Guid favoritedToId = Guid.Empty;
            Guid targetUserIdForThread = Guid.Empty; // Thread oluşturmak için
            
            if (store != null)
            {
                favoritedToId = store.Id; // Store ID
                targetUserIdForThread = store.BarberStoreOwnerId; // Thread için owner User ID
            }
            else if (freeBarber != null)
            {
                favoritedToId = freeBarber.FreeBarberUserId; // FreeBarber User ID
                targetUserIdForThread = freeBarber.FreeBarberUserId;
            }
            else if (customerUser != null)
            {
                favoritedToId = customerUser.Id; // Customer User ID
                targetUserIdForThread = customerUser.Id;
            }
            else if (manuelBarber != null)
            {
                // ManuelBarber için store'dan owner'ı bul
                var manuelBarberStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                if (manuelBarberStore != null)
                {
                    favoritedToId = manuelBarberStore.BarberStoreOwnerId; // ManuelBarber için store owner User ID
                    targetUserIdForThread = manuelBarberStore.BarberStoreOwnerId;
                }
            }

            if (favoritedToId == Guid.Empty)
                return new ErrorDataResult<bool>(Messages.TargetUserNotFound);
            
            // Kendi kendine favori eklenebilir (Store sahibi kendi dükkanını, FreeBarber kendi panelini favoriye ekleyebilir)
            // Ancak thread oluşturulmaz (kendi kendine mesajlaşma mantıklı değil)
            bool isSelfFavorite = targetUserIdForThread == userId;

            // Mevcut favori kontrolü
            // Store için: FavoritedToId = Store ID
            // FreeBarber/Customer için: FavoritedToId = User ID
            var existingFavorite = await _favoriteDal.Get(x => x.FavoritedFromId == userId && x.FavoritedToId == favoritedToId);

            if (existingFavorite != null)
            {
                // Favori varsa IsActive durumunu toggle et
                existingFavorite.IsActive = !existingFavorite.IsActive;
                existingFavorite.UpdatedAt = DateTime.UtcNow;
                await _favoriteDal.Update(existingFavorite);
                
                // Favori aktif edildiyse thread oluştur veya güncelle (thread'ler User ID'ler arasında)
                // Ancak kendi kendine favori ise thread oluşturma
                if (existingFavorite.IsActive && !isSelfFavorite && targetUserIdForThread != Guid.Empty)
                {
                    // Thread oluştur veya kontrol et (EnsureFavoriteThreadAsync zaten mevcut thread'i döndürür)
                    await _chatService.EnsureFavoriteThreadAsync(userId, targetUserIdForThread);
                }
                // Favori pasif edildiyse thread görünmez olacak (GetThreadsAsync'te aktif favori kontrolü var)
                
                var message = existingFavorite.IsActive 
                    ? Messages.FavoriteAddedSuccess 
                    : Messages.FavoriteRemovedSuccess;
                    
                return new SuccessDataResult<bool>(existingFavorite.IsActive, message);
            }
            else
            {
                // Favori yoksa ekle (IsActive = true ile)
                // Store için: FavoritedToId = Store ID
                // FreeBarber/Customer için: FavoritedToId = User ID
                var favorite = new Favorite
                {
                    Id = Guid.NewGuid(),
                    FavoritedFromId = userId,
                    FavoritedToId = favoritedToId, // Store ID veya User ID
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _favoriteDal.Add(favorite);
                
                // Favori eklendiğinde thread oluştur (eğer yoksa) - thread'ler User ID'ler arasında
                // Ancak kendi kendine favori ise thread oluşturma
                if (!isSelfFavorite && targetUserIdForThread != Guid.Empty)
                {
                    await _chatService.EnsureFavoriteThreadAsync(userId, targetUserIdForThread);
                }
                
                return new SuccessDataResult<bool>(true, Messages.FavoriteAddedSuccess);
            }
        }

        public async Task<IDataResult<bool>> IsFavoriteAsync(Guid userId, Guid targetId)
        {
            // targetId Store ID, FreeBarber ID, Customer User ID veya ManuelBarber ID olabilir
            // FavoritedToId belirleme:
            // Store için: Store ID (her dükkanın kendi favori sayısı)
            // FreeBarber için: FreeBarber User ID
            // Customer için: Customer User ID
            // ManuelBarber için: Store Owner User ID
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            var customerUser = await _userDal.Get(x => x.Id == targetId);
            
            Guid favoritedToId = Guid.Empty;
            if (store != null)
                favoritedToId = store.Id; // Store ID
            else if (freeBarber != null)
                favoritedToId = freeBarber.FreeBarberUserId; // FreeBarber User ID
            else if (customerUser != null)
                favoritedToId = customerUser.Id; // Customer User ID
            else if (manuelBarber != null)
            {
                var manuelBarberStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                if (manuelBarberStore != null)
                    favoritedToId = manuelBarberStore.BarberStoreOwnerId; // ManuelBarber için store owner User ID
            }
            
            if (favoritedToId == Guid.Empty)
                return new SuccessDataResult<bool>(false);
            
            // FavoritedToId'ye göre kontrol et (aktif favoriler)
            // Store için: FavoritedToId = Store ID
            // FreeBarber/Customer için: FavoritedToId = User ID
            var favorite = await _favoriteDal.Get(x => x.FavoritedFromId == userId && x.FavoritedToId == favoritedToId && x.IsActive);
            return new SuccessDataResult<bool>(favorite != null);
        }

        public async Task<IDataResult<List<FavoriteGetDto>>> GetMyFavoritesAsync(Guid userId)
        {
            // Sadece aktif favorileri getir
            // FavoritedToId: Store ID (Store için), FreeBarber User ID (FreeBarber için), Customer User ID (Customer için)
            var favorites = await _favoriteDal.GetAll(x => x.FavoritedFromId == userId && x.IsActive);
            
            if (!favorites.Any())
                return new SuccessDataResult<List<FavoriteGetDto>>(new List<FavoriteGetDto>());

            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            // FavoritedToId'leri ayır: Store ID'ler ve User ID'ler
            var favoriteToIds = favorites.Select(f => f.FavoritedToId).Distinct().ToList();
            
            // Store ID'leri bul (BarberStores tablosunda var mı?)
            var storeEntities = await _barberStoreDal.GetAll(x => favoriteToIds.Contains(x.Id));
            var storeIds = storeEntities.Select(s => s.Id).ToList();
            
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

                var storeIdsList = stores.Select(s => s.Id).ToList();

                // Rating & ReviewCount - Artık TargetId Store ID (her dükkanın kendi rating'i)
                var ratingStats = await _context.Ratings
                    .AsNoTracking()
                    .Where(r => storeIdsList.Contains(r.TargetId))
                    .GroupBy(r => r.TargetId)
                    .Select(g => new { StoreId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                    .ToListAsync();
                var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });

                // Favorite count (sadece aktif favoriler) - Artık Store ID'ye göre (her dükkanın kendi favori sayısı)
                var favoriteStats = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => storeIdsList.Contains(f.FavoritedToId) && f.IsActive)
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
                    ratingDict.TryGetValue(store.Id, out var ratingInfo); // Artık Store ID'ye göre
                    favoriteDict.TryGetValue(store.Id, out var favCount); // Artık Store ID'ye göre
                    offeringDict.TryGetValue(store.Id, out var offerings);
                    hoursDict.TryGetValue(store.Id, out var hours);
                    imageDict.TryGetValue(store.Id, out var images); // Her store'un kendi fotoğrafları

                    var isOpenNow = hours != null ? OpenControl.IsOpenNow(hours, nowLocal) : false;

                    // Her store için ayrı DTO oluştur (Key: Store ID)
                    storeDetails[store.Id] = new BarberStoreGetDto
                    {
                        Id = store.Id, // Her store'un kendi ID'si
                        StoreName = store.StoreName, // Her store'un kendi ismi
                        Type = store.Type,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2), // Her store'un kendi rating'i
                        ReviewCount = ratingInfo?.ReviewCount ?? 0, // Her store'un kendi review count'u
                        FavoriteCount = favCount, // Her store'un kendi favori sayısı
                        IsOpenNow = isOpenNow,
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        ImageList = images ?? new List<ImageGetDto>(), // Her store'un kendi fotoğrafları
                        AddressDescription = store.AddressDescription,
                        PricingType = store.PricingType.ToString(),
                        PricingValue = store.PricingValue,
                        Latitude = store.Latitude,
                        Longitude = store.Longitude,
                        DistanceKm = 0 // Favoriler için distance hesaplanmıyor
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
                    .Select(fb => new { fb.Id, fb.FirstName, fb.LastName, fb.Type, fb.IsAvailable, fb.Latitude, fb.Longitude, fb.FreeBarberUserId })
                    .ToListAsync();

                var fbIdsList = freeBarbers.Select(fb => fb.Id).ToList();

                // Rating & ReviewCount - TargetId = FreeBarber User ID (FreeBarber'ın rating'i)
                var freeBarberOwnerIds = freeBarbers.Select(fb => fb.FreeBarberUserId).Distinct().ToList();
                var fbRatingStats = await _context.Ratings
                    .AsNoTracking()
                    .Where(r => freeBarberOwnerIds.Contains(r.TargetId))
                    .GroupBy(r => r.TargetId)
                    .Select(g => new { OwnerUserId = g.Key, AvgRating = g.Average(x => (double)x.Score), ReviewCount = g.Count() })
                    .ToListAsync();
                var fbRatingDict = fbRatingStats.ToDictionary(x => x.OwnerUserId, x => new { x.AvgRating, x.ReviewCount });

                // Favorite count (sadece aktif favoriler) - FavoritedToId = FreeBarber User ID (FreeBarber'ın favori sayısı)
                var fbFavoriteStats = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => freeBarberOwnerIds.Contains(f.FavoritedToId) && f.IsActive)
                    .GroupBy(f => f.FavoritedToId)
                    .Select(g => new { OwnerUserId = g.Key, FavoriteCount = g.Count() })
                    .ToListAsync();
                var fbFavoriteDict = fbFavoriteStats.ToDictionary(x => x.OwnerUserId, x => x.FavoriteCount);

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
                    var freeBarberOwnerId = fb.FreeBarberUserId;
                    fbRatingDict.TryGetValue(freeBarberOwnerId, out var ratingInfo); // Artık owner User ID'ye göre
                    fbFavoriteDict.TryGetValue(freeBarberOwnerId, out var favCount);
                    fbOfferingDict.TryGetValue(fb.Id, out var offerings);
                    fbImageDict.TryGetValue(fb.Id, out var images); // Her freeBarber'ın kendi fotoğrafları

                    freeBarberDetails[freeBarberOwnerId] = new FreeBarberGetDto // Key olarak owner User ID kullan
                    {
                        Id = fb.Id, // Her freeBarber'ın kendi ID'si
                        FullName = $"{fb.FirstName} {fb.LastName}", // Her freeBarber'ın kendi ismi
                        Type = fb.Type,
                        Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                        ReviewCount = ratingInfo?.ReviewCount ?? 0,
                        FavoriteCount = favCount,
                        IsAvailable = fb.IsAvailable,
                        Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                        ImageList = images ?? new List<ImageGetDto>(), // Her freeBarber'ın kendi fotoğrafları
                        Latitude = fb.Latitude,
                        Longitude = fb.Longitude,
                        DistanceKm = 0 // Favoriler için distance hesaplanmıyor
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
                {
                    manuelBarberDict[store.BarberStoreOwnerId] = mb; // Key olarak store owner User ID kullan
                }
            }

            // Customer User'ları getir - direkt User ID'ler
            var customerUsers = await _userDal.GetAll(x => targetUserIds.Contains(x.Id));
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
                    FavoritedToId = f.FavoritedToId, // Artık User ID
                    CreatedAt = f.CreatedAt
                };

                // Store bilgisi - Artık User ID'ye göre kontrol et
                if (storeDetails.TryGetValue(f.FavoritedToId, out var storeDetail))
                {
                    dto.TargetType = FavoriteTargetType.Store;
                    dto.TargetName = storeDetail.StoreName;
                    dto.Store = storeDetail;
                }
                // FreeBarber bilgisi - Artık User ID'ye göre kontrol et
                else if (freeBarberDetails.TryGetValue(f.FavoritedToId, out var freeBarberDetail))
                {
                    dto.TargetType = FavoriteTargetType.FreeBarber;
                    dto.TargetName = freeBarberDetail.FullName;
                    dto.FreeBarber = freeBarberDetail;
                }
                // ManuelBarber bilgisi - Artık User ID'ye göre kontrol et
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
                // Customer User bilgisi - Artık direkt User ID
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
            // targetId Store ID, FreeBarber ID, Customer User ID veya ManuelBarber ID olabilir
            // Önce targetId'nin UserId'sini bul
            var store = await _barberStoreDal.Get(x => x.Id == targetId);
            var freeBarber = await _freeBarberDal.Get(x => x.Id == targetId);
            var manuelBarber = await _manuelBarberDal.Get(x => x.Id == targetId);
            var customerUser = await _userDal.Get(x => x.Id == targetId);
            
            Guid targetUserId = Guid.Empty;
            if (store != null)
                targetUserId = store.BarberStoreOwnerId;
            else if (freeBarber != null)
                targetUserId = freeBarber.FreeBarberUserId;
            else if (customerUser != null)
                targetUserId = customerUser.Id;
            else if (manuelBarber != null)
            {
                var manuelBarberStore = await _barberStoreDal.Get(x => x.Id == manuelBarber.StoreId);
                if (manuelBarberStore != null)
                    targetUserId = manuelBarberStore.BarberStoreOwnerId;
            }
            
            if (targetUserId == Guid.Empty)
                return new ErrorDataResult<bool>("Favori bulunamadı.");
            
            // Artık her zaman User ID'ler arasında kontrol et
            var favorite = await _favoriteDal.GetByUsersAsync(userId, targetUserId);
            if (favorite == null)
                return new ErrorDataResult<bool>("Favori bulunamadı.");

            await _favoriteDal.Remove(favorite);
            return new SuccessDataResult<bool>(true, "Favoriden çıkarıldı.");
        }
    }
}
