using Business.Abstract;
using Business.Resources;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class ChatManager(
             IAppointmentDal appointmentDal,
             IChatThreadDal threadDal,
             IChatMessageDal messageDal,
             IBadgeService badgeSvc,
             IBarberStoreDal barberStoreDal,
             IUserDal userDal,
             IFreeBarberDal freeBarberDal,
             IImageDal imageDal,
             IFavoriteDal favoriteDal,
             IRealTimePublisher realtime
     ) : IChatService
    {

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return new ErrorDataResult<ChatMessageDto>(Messages.EmptyMessage);

            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<ChatMessageDto>(Messages.AppointmentNotFound);

            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<ChatMessageDto>(Messages.ChatOnlyForActiveAppointments);

            // yetki: sender katılımcı mı?
            var isParticipant =
                appt.CustomerUserId == senderUserId ||
                appt.FreeBarberUserId == senderUserId ||
                appt.BarberStoreUserId == senderUserId;

            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>(Messages.NotAParticipant);

            // Performance: Use Get instead of GetAll().FirstOrDefault()
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            var barberStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId);
            if (barberStore is null)
                return new ErrorDataResult<ChatMessageDto>(Messages.StoreNotFound);
            
            if (thread is null)
            {
                thread = new ChatThread
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = appointmentId,
                    CustomerUserId = appt.CustomerUserId,
                    StoreOwnerUserId = barberStore.BarberStoreOwnerId,     
                    FreeBarberUserId = appt.FreeBarberUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await threadDal.Add(thread);
            }

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AppointmentId = appointmentId,
                SenderUserId = senderUserId,
                Text = text,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            };
            await messageDal.Add(msg);

            thread.LastMessageAt = msg.CreatedAt;
            thread.LastMessagePreview = text.Length > 60 ? text[..60] : text;
            thread.UpdatedAt = DateTime.UtcNow;

            // unread arttır (sender dışındaki katılımcılara)
            if (thread.CustomerUserId.HasValue && thread.CustomerUserId != senderUserId) thread.CustomerUnreadCount++;
            if (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId != senderUserId) thread.StoreUnreadCount++;
            if (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId != senderUserId) thread.FreeBarberUnreadCount++;

            await threadDal.Update(thread);

            var dto = new ChatMessageDto
            {
                ThreadId = thread.Id,
                AppointmentId = appointmentId,
                MessageId = msg.Id,
                SenderUserId = senderUserId,
                Text = msg.Text,
                CreatedAt = msg.CreatedAt
            };

            // push -> tüm katılımcılara (sender dahil - kendi mesajını görmesi için)
            var recipients = new[] { thread.CustomerUserId, thread.StoreOwnerUserId, thread.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            foreach (var u in recipients)
            {
                await realtime.PushChatMessageAsync(u, dto);
                // Badge count güncellemesi - tüm katılımcılar için (sender dahil)
                var badges = await badgeSvc.GetCountsAsync(u);
                if (badges.Success) await realtime.PushBadgeAsync(u, badges.Data);
            }

            // Thread güncellemesini tüm katılımcılara push et (LastMessagePreview, LastMessageAt, UnreadCount değişti)
            await PushAppointmentThreadUpdatedAsync(appointmentId);

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IDataResult<int>> GetUnreadTotalAsync(Guid userId)
        {
            var threads = await threadDal.GetAll(t =>
                t.CustomerUserId == userId || t.StoreOwnerUserId == userId || t.FreeBarberUserId == userId);

            var total = threads.Sum(t =>
                t.CustomerUserId == userId ? t.CustomerUnreadCount :
                t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
                t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);

            return new SuccessDataResult<int>(total);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> MarkThreadReadByAppointmentAsync(Guid userId, Guid appointmentId)
        {
            // Randevu thread'i için okundu işaretleme (geriye dönük uyumluluk)
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread is null) return new ErrorDataResult<bool>(false, Messages.ChatNotFound);

            if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
            else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
            else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
            else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);

            await threadDal.Update(thread);

            // Badge count güncellemesi - okundu işaretleyen kullanıcı için
            var badges = await badgeSvc.GetCountsAsync(userId);
            if (badges.Success) await realtime.PushBadgeAsync(userId, badges.Data);

            return new SuccessDataResult<bool>(true);
        }


        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IDataResult<List<ChatThreadListItemDto>>> GetThreadsAsync(Guid userId)
        {
            // sadece Pending + Approved randevular için
            var allowed = new[] { AppointmentStatus.Pending, AppointmentStatus.Approved };

            var threads = await threadDal.GetThreadsForUserAsync(userId, allowed);

            if (threads.Count == 0)
                return new SuccessDataResult<List<ChatThreadListItemDto>>(threads);

            // Randevu thread'leri ve favori thread'lerini ayır
            var appointmentThreads = threads.Where(t => !t.IsFavoriteThread && t.AppointmentId.HasValue).ToList();
            var favoriteThreads = threads.Where(t => t.IsFavoriteThread).ToList();

            var result = new List<ChatThreadListItemDto>();

            // Randevu thread'leri için işlem
            if (appointmentThreads.Any())
            {
                var appointmentIds = appointmentThreads.Select(t => t.AppointmentId!.Value).ToList();
                var appointments = await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id));
                var apptDict = appointments.ToDictionary(a => a.Id);

                // Thread entity'lerini getir (participant bilgileri için)
                var threadIds = appointmentThreads.Select(t => t.AppointmentId!.Value).ToList();
                var threadEntities = await threadDal.GetAll(t => t.AppointmentId.HasValue && appointmentIds.Contains(t.AppointmentId.Value));
                // GroupBy kullanarak duplicate AppointmentId'leri handle et (her AppointmentId için en son thread'i al)
                var threadDict = threadEntities
                    .GroupBy(t => t.AppointmentId!.Value)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.CreatedAt).First());

                // Tüm katılımcı ID'leri topla
                var participantIds = new HashSet<Guid>();
                foreach (var appt in appointments)
                {
                    if (appt.CustomerUserId.HasValue) participantIds.Add(appt.CustomerUserId.Value);
                    if (appt.BarberStoreUserId.HasValue) participantIds.Add(appt.BarberStoreUserId.Value);
                    if (appt.FreeBarberUserId.HasValue) participantIds.Add(appt.FreeBarberUserId.Value);
                }

                // Kullanıcı bilgilerini batch olarak çek
                var users = await userDal.GetAll(u => participantIds.Contains(u.Id));
                var userDict = users.ToDictionary(u => u.Id);

                var storeOwnerIds = appointments.Where(a => a.BarberStoreUserId.HasValue)
                    .Select(a => a.BarberStoreUserId!.Value).Distinct().ToList();
                var stores = storeOwnerIds.Any()
                    ? await barberStoreDal.GetAll(x => storeOwnerIds.Contains(x.BarberStoreOwnerId))
                    : new List<BarberStore>();
                // GroupBy kullanarak duplicate BarberStoreOwnerId'leri handle et (her owner için en son store'u al)
                var storeDict = stores
                    .GroupBy(s => s.BarberStoreOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

                var freeBarberIds = appointments.Where(a => a.FreeBarberUserId.HasValue)
                    .Select(a => a.FreeBarberUserId!.Value).Distinct().ToList();
                var freeBarbers = freeBarberIds.Any()
                    ? await freeBarberDal.GetAll(x => freeBarberIds.Contains(x.FreeBarberUserId))
                    : new List<FreeBarber>();
                // GroupBy kullanarak duplicate FreeBarberUserId'leri handle et (her user için en son freeBarber'ı al)
                var freeBarberDict = freeBarbers
                    .GroupBy(fb => fb.FreeBarberUserId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(fb => fb.CreatedAt).First());

                // Image'ları batch olarak çek
                var userImageIds = users.Where(u => u.ImageId.HasValue).Select(u => u.ImageId!.Value).Distinct().ToList();
                var storeImageOwnerIds = stores.Select(s => s.Id).ToList();
                var freeBarberImageOwnerIds = freeBarbers.Select(fb => fb.Id).ToList();

                var userImages = userImageIds.Any()
                    ? await imageDal.GetAll(i => userImageIds.Contains(i.Id) && i.OwnerType == ImageOwnerType.User)
                    : new List<Image>();
                var storeImages = storeImageOwnerIds.Any()
                    ? await imageDal.GetAll(i => storeImageOwnerIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.Store)
                    : new List<Image>();
                var freeBarberImages = freeBarberImageOwnerIds.Any()
                    ? await imageDal.GetAll(i => freeBarberImageOwnerIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.FreeBarber)
                    : new List<Image>();

                // GroupBy kullanarak duplicate ImageId'leri handle et (her image için en son olanı al)
                var userImageDict = userImages
                    .GroupBy(i => i.Id)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First());
                var storeImageDict = storeImages.GroupBy(i => i.ImageOwnerId).ToDictionary(g => g.Key, g => g.First().ImageUrl);
                var freeBarberImageDict = freeBarberImages.GroupBy(i => i.ImageOwnerId).ToDictionary(g => g.Key, g => g.First().ImageUrl);

                // Her randevu thread'i için işlem
                foreach (var threadDto in appointmentThreads)
                {
                    if (!apptDict.TryGetValue(threadDto.AppointmentId!.Value, out var appt)) continue;
                    if (!threadDict.TryGetValue(threadDto.AppointmentId.Value, out var threadEntity)) continue;

                    storeDict.TryGetValue(appt.BarberStoreUserId ?? Guid.Empty, out var store);
                    threadDto.Title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Participants listesini doldur
                    threadDto.Participants = new List<ChatThreadParticipantDto>();

                    // Customer
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customer))
                        {
                            var imageUrl = customer.ImageId.HasValue && userImageDict.TryGetValue(customer.ImageId.Value, out var img) ? img.ImageUrl : null;
                            threadDto.Participants.Add(new ChatThreadParticipantDto
                            {
                                UserId = customer.Id,
                                DisplayName = $"{customer.FirstName} {customer.LastName}",
                                ImageUrl = imageUrl,
                                UserType = customer.UserType,
                                BarberType = null
                            });
                        }
                    }

                    // Store
                    if (appt.BarberStoreUserId.HasValue && appt.BarberStoreUserId != userId && store != null)
                    {
                        var imageUrl = storeImageDict.TryGetValue(store.Id, out var imgUrl) ? imgUrl : null;
                        if (userDict.TryGetValue(store.BarberStoreOwnerId, out var storeOwner))
                        {
                            threadDto.Participants.Add(new ChatThreadParticipantDto
                            {
                                UserId = storeOwner.Id,
                                DisplayName = store.StoreName,
                                ImageUrl = imageUrl,
                                UserType = UserType.BarberStore,
                                BarberType = store.Type
                            });
                        }
                    }

                    // FreeBarber
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId != userId)
                    {
                        var freeBarber = freeBarbers.FirstOrDefault(fb => fb.FreeBarberUserId == appt.FreeBarberUserId.Value);
                        if (freeBarber != null)
                        {
                            var imageUrl = freeBarberImageDict.TryGetValue(freeBarber.Id, out var imgUrl) ? imgUrl : null;
                            if (userDict.TryGetValue(freeBarber.FreeBarberUserId, out var fbUser))
                            {
                                threadDto.Participants.Add(new ChatThreadParticipantDto
                                {
                                    UserId = fbUser.Id,
                                    DisplayName = $"{freeBarber.FirstName} {freeBarber.LastName}",
                                    ImageUrl = imageUrl,
                                    UserType = UserType.FreeBarber,
                                    BarberType = freeBarber.Type
                                });
                            }
                        }
                    }

                    result.Add(threadDto);
                }
            }

            // Favori thread'leri için işlem
            if (favoriteThreads.Any())
            {
                var favoriteThreadEntities = await threadDal.GetFavoriteThreadsForUserAsync(userId);
                var favoriteDict = favoriteThreadEntities.ToDictionary(t => t.Id);

                // Aktif favorileri kontrol et
                var activeFavoriteThreads = new List<ChatThreadListItemDto>();
                foreach (var threadDto in favoriteThreads)
                {
                    // Thread entity'sini bul (ThreadId ile)
                    if (!favoriteDict.TryGetValue(threadDto.ThreadId, out var threadEntity))
                        continue;

                    // Favori aktif mi kontrol et - en az bir tarafın favori olması yeterli
                    // ÖNEMLİ: Thread User ID'ler arasında, ama favoriler Store ID veya User ID ile kaydediliyor
                    // Store için: Store ID ile kaydediliyor, ama thread User ID'ler arasında
                    // Bu yüzden Store ID'leri User ID'lere çevirip favori kontrolü yapıyoruz
                    
                    bool isFavoriteActive = false;
                    var fromUserId = threadEntity.FavoriteFromUserId!.Value;
                    var toUserId = threadEntity.FavoriteToUserId!.Value;
                    
                    // 1. fromUserId -> toUserId yönünde favori kontrolü
                    var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                    if (favorite1 != null && favorite1.IsActive)
                    {
                        isFavoriteActive = true;
                    }
                    else
                    {
                        // Store ID kontrolü: toUserId bir Store Owner ID olabilir
                        var store1 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == toUserId);
                        if (store1 != null)
                        {
                            var favorite1Store = await favoriteDal.GetByUsersAsync(fromUserId, store1.Id);
                            if (favorite1Store != null && favorite1Store.IsActive)
                                isFavoriteActive = true;
                        }
                    }
                    
                    // 2. toUserId -> fromUserId yönünde favori kontrolü
                    if (!isFavoriteActive)
                    {
                        var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favorite2 != null && favorite2.IsActive)
                        {
                            isFavoriteActive = true;
                        }
                        else
                        {
                            // Store ID kontrolü: fromUserId bir Store Owner ID olabilir
                            var store2 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == fromUserId);
                            if (store2 != null)
                            {
                                var favorite2Store = await favoriteDal.GetByUsersAsync(toUserId, store2.Id);
                                if (favorite2Store != null && favorite2Store.IsActive)
                                    isFavoriteActive = true;
                            }
                        }
                    }
                    
                    // En az bir tarafın favori olması yeterli (aktif olmalı)
                    if (!isFavoriteActive) continue; // Hiçbiri aktif değilse thread'i atla

                    var otherUserId = threadEntity.FavoriteFromUserId == userId 
                        ? threadEntity.FavoriteToUserId!.Value 
                        : threadEntity.FavoriteFromUserId!.Value;

                    // Diğer kullanıcının bilgilerini çek
                    var otherUser = await userDal.Get(u => u.Id == otherUserId);
                    if (otherUser == null) continue;

                    string displayName = "";
                    string? imageUrl = null;
                    BarberType? barberType = null;

                    if (otherUser.UserType == UserType.Customer)
                    {
                        displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                        if (otherUser.ImageId.HasValue)
                        {
                            var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else if (otherUser.UserType == UserType.BarberStore)
                    {
                        var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                        if (store != null)
                        {
                            displayName = store.StoreName;
                            barberType = store.Type;
                            var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else if (otherUser.UserType == UserType.FreeBarber)
                    {
                        var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                        if (freeBarber != null)
                        {
                            displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                            barberType = freeBarber.Type;
                            var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                            imageUrl = img?.ImageUrl;
                        }
                    }

                    threadDto.Title = displayName;
                    threadDto.Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = otherUser.Id,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = otherUser.UserType,
                            BarberType = barberType
                        }
                    };

                    activeFavoriteThreads.Add(threadDto);
                }

                result.AddRange(activeFavoriteThreads);
            }

            // Son mesaj zamanına göre sırala
            result = result.OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue).ToList();

            return new SuccessDataResult<List<ChatThreadListItemDto>>(result);
        }

        private static string BuildThreadTitleForUser(Guid userId, Appointment appt, string? storeName)
        {
            if (appt.BarberStoreUserId == userId)
            {
                // store owner kendi listesinde karşı taraf
                return appt.CustomerUserId.HasValue ? Messages.ChatThreadTitleCustomer : Messages.ChatThreadTitleFreeBarber;
            }

            // customer/freebarber tarafı store'u görsün
            return string.IsNullOrWhiteSpace(storeName) ? Messages.ChatThreadTitleBarberStore : storeName!;
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesAsync(
            Guid userId, Guid appointmentId, DateTime? beforeUtc)
        {

            // Performance: Use repository instead of direct DbContext access
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.AppointmentNotFound);

            // sadece Pending + Approved sohbet gösterimi
            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.ChatOnlyForActiveAppointments);

            // katılımcı mı?
            // Performance: Use repository instead of direct DbContext access
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread is null) return new SuccessDataResult<List<ChatMessageItemDto>>();

            var isParticipant =
                thread.CustomerUserId == userId || thread.StoreOwnerUserId == userId || thread.FreeBarberUserId == userId;

            if (!isParticipant) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.NotAParticipant);

            var msgs = await messageDal.GetMessagesForAppointmentAsync(appointmentId, beforeUtc);

            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<ChatMessageDto>> SendFavoriteMessageAsync(Guid senderUserId, Guid threadId, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return new ErrorDataResult<ChatMessageDto>(Messages.EmptyMessage);

            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<ChatMessageDto>(Messages.ChatNotFound);

            // Favori thread kontrolü
            if (thread.AppointmentId.HasValue) return new ErrorDataResult<ChatMessageDto>("Bu metod sadece favori thread'ler için kullanılabilir");

            // Katılımcı kontrolü
            var isParticipant = (thread.FavoriteFromUserId == senderUserId || thread.FavoriteToUserId == senderUserId);
            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>(Messages.NotAParticipant);

            // Favori aktif mi kontrolü - en az bir tarafın favori olması yeterli
            // ÖNEMLİ: Thread User ID'ler arasında, ama favoriler Store ID ile kaydediliyor
            var fromUserId = thread.FavoriteFromUserId!.Value;
            var toUserId = thread.FavoriteToUserId!.Value;
            
            bool isFavoriteActive = false;
            
            // 1. fromUserId -> toUserId yönünde
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }
            else
            {
                // Store ID kontrolü: toUserId bir Store Owner ID olabilir
                var store1 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == toUserId);
                if (store1 != null)
                {
                    var favorite1Store = await favoriteDal.GetByUsersAsync(fromUserId, store1.Id);
                    if (favorite1Store != null && favorite1Store.IsActive)
                        isFavoriteActive = true;
                }
            }
            
            // 2. toUserId -> fromUserId yönünde
            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
                else
                {
                    // Store ID kontrolü: fromUserId bir Store Owner ID olabilir
                    var store2 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == fromUserId);
                    if (store2 != null)
                    {
                        var favorite2Store = await favoriteDal.GetByUsersAsync(toUserId, store2.Id);
                        if (favorite2Store != null && favorite2Store.IsActive)
                            isFavoriteActive = true;
                    }
                }
            }
            
            if (!isFavoriteActive)
                return new ErrorDataResult<ChatMessageDto>("Favori aktif değil, mesaj gönderilemez");

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AppointmentId = null, // Favori thread'de AppointmentId null
                SenderUserId = senderUserId,
                Text = text,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            };
            await messageDal.Add(msg);

            thread.LastMessageAt = msg.CreatedAt;
            thread.LastMessagePreview = text.Length > 60 ? text[..60] : text;
            thread.UpdatedAt = DateTime.UtcNow;

            // Unread count artır (sender dışındaki katılımcıya)
            var otherUserId = thread.FavoriteFromUserId == senderUserId ? thread.FavoriteToUserId : thread.FavoriteFromUserId;
            
            if (otherUserId.HasValue)
            {
                // Thread'deki user mapping'leri kullan (EnsureFavoriteThreadAsync'te set edilmiş)
                if (thread.CustomerUserId == otherUserId) thread.CustomerUnreadCount++;
                else if (thread.StoreOwnerUserId == otherUserId) thread.StoreUnreadCount++;
                else if (thread.FreeBarberUserId == otherUserId) thread.FreeBarberUnreadCount++;
            }

            await threadDal.Update(thread);

            var dto = new ChatMessageDto
            {
                ThreadId = thread.Id,
                AppointmentId = null,
                MessageId = msg.Id,
                SenderUserId = senderUserId,
                Text = msg.Text,
                CreatedAt = msg.CreatedAt
            };

            // Push -> tüm katılımcılara (sender ve other user)
            var favoriteRecipients = new List<Guid> { senderUserId };
            if (otherUserId.HasValue)
            {
                favoriteRecipients.Add(otherUserId.Value);
            }

            foreach (var recipientId in favoriteRecipients.Distinct())
            {
                await realtime.PushChatMessageAsync(recipientId, dto);
                // Badge count güncellemesi - tüm katılımcılar için
                var badges = await badgeSvc.GetCountsAsync(recipientId);
                if (badges.Success) await realtime.PushBadgeAsync(recipientId, badges.Data);
            }

            // Thread güncellemesini her iki kullanıcıya da push et (LastMessagePreview, LastMessageAt, UnreadCount değişti)
            // EnsureFavoriteThreadAsync mantığını kullanarak thread detaylarını oluştur ve push et
            await PushFavoriteThreadUpdatedAsync(fromUserId, toUserId, thread.Id);

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> MarkThreadReadAsync(Guid userId, Guid threadId)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<bool>(false, Messages.ChatNotFound);

            // Randevu thread'i için
            if (thread.AppointmentId.HasValue)
            {
                if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
                else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
                else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
                else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);
            }
            // Favori thread için
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                if (thread.FavoriteFromUserId == userId)
                {
                    if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
                    else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
                    else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
                }
                else if (thread.FavoriteToUserId == userId)
                {
                    if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
                    else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
                    else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
                }
                else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);
            }

            await threadDal.Update(thread);

            // Badge count güncellemesi - okundu işaretleyen kullanıcı için
            var badges = await badgeSvc.GetCountsAsync(userId);
            if (badges.Success) await realtime.PushBadgeAsync(userId, badges.Data);

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesByThreadAsync(Guid userId, Guid threadId, DateTime? beforeUtc)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.ChatNotFound);

            // Katılımcı kontrolü
            bool isParticipant = false;
            if (thread.AppointmentId.HasValue)
            {
                // Randevu thread'i
                var appt = await appointmentDal.Get(x => x.Id == thread.AppointmentId.Value);
                if (appt is null) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.AppointmentNotFound);

                if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                    return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.ChatOnlyForActiveAppointments);

                isParticipant = thread.CustomerUserId == userId || thread.StoreOwnerUserId == userId || thread.FreeBarberUserId == userId;
            }
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                // Favori thread
                isParticipant = thread.FavoriteFromUserId == userId || thread.FavoriteToUserId == userId;
                
                // Favori aktif mi kontrolü - en az bir tarafın favori olması yeterli
                // ÖNEMLİ: Thread User ID'ler arasında, ama favoriler Store ID ile kaydediliyor
                var fromUserId = thread.FavoriteFromUserId.Value;
                var toUserId = thread.FavoriteToUserId.Value;
                
                bool isFavoriteActive = false;
                
                // 1. fromUserId -> toUserId yönünde
                var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                if (favorite1 != null && favorite1.IsActive)
                {
                    isFavoriteActive = true;
                }
                else
                {
                    // Store ID kontrolü: toUserId bir Store Owner ID olabilir
                    var store1 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == toUserId);
                    if (store1 != null)
                    {
                        var favorite1Store = await favoriteDal.GetByUsersAsync(fromUserId, store1.Id);
                        if (favorite1Store != null && favorite1Store.IsActive)
                            isFavoriteActive = true;
                    }
                }
                
                // 2. toUserId -> fromUserId yönünde
                if (!isFavoriteActive)
                {
                    var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                    if (favorite2 != null && favorite2.IsActive)
                    {
                        isFavoriteActive = true;
                    }
                    else
                    {
                        // Store ID kontrolü: fromUserId bir Store Owner ID olabilir
                        var store2 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == fromUserId);
                        if (store2 != null)
                        {
                            var favorite2Store = await favoriteDal.GetByUsersAsync(toUserId, store2.Id);
                            if (favorite2Store != null && favorite2Store.IsActive)
                                isFavoriteActive = true;
                        }
                    }
                }
                
                if (!isFavoriteActive)
                    return new ErrorDataResult<List<ChatMessageItemDto>>("Favori aktif değil");
            }

            if (!isParticipant) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.NotAParticipant);

            var msgs = await messageDal.GetMessagesByThreadIdAsync(threadId, beforeUtc);
            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> EnsureFavoriteThreadAsync(Guid fromUserId, Guid toUserId)
        {
            // Mevcut thread'i kontrol et (her iki yönde)
            var existingThread = await threadDal.GetFavoriteThreadAsync(fromUserId, toUserId);
            
            ChatThread thread;
            bool isNewThread = false;

            if (existingThread != null)
            {
                thread = existingThread;
            }
            else
            {
                // Yeni thread oluştur
                var fromUser = await userDal.Get(u => u.Id == fromUserId);
                var toUser = await userDal.Get(u => u.Id == toUserId);
                
                if (fromUser == null || toUser == null)
                    return new ErrorDataResult<Guid>("Kullanıcı bulunamadı");

                thread = new ChatThread
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = null,
                    FavoriteFromUserId = fromUserId,
                    FavoriteToUserId = toUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Her iki kullanıcının UserType'ına göre CustomerUserId, StoreOwnerUserId veya FreeBarberUserId'yi set et
                if (fromUser.UserType == UserType.Customer)
                    thread.CustomerUserId = fromUserId;
                else if (fromUser.UserType == UserType.BarberStore)
                    thread.StoreOwnerUserId = fromUserId;
                else if (fromUser.UserType == UserType.FreeBarber)
                    thread.FreeBarberUserId = fromUserId;

                if (toUser.UserType == UserType.Customer && thread.CustomerUserId != toUserId)
                    thread.CustomerUserId = toUserId;
                else if (toUser.UserType == UserType.BarberStore && thread.StoreOwnerUserId != toUserId)
                    thread.StoreOwnerUserId = toUserId;
                else if (toUser.UserType == UserType.FreeBarber && thread.FreeBarberUserId != toUserId)
                    thread.FreeBarberUserId = toUserId;

                await threadDal.Add(thread);
                isNewThread = true;
            }

            // ÖNEMLİ: Aktif favori kontrolü - en az bir tarafın favori aktif olmalı
            // Thread User ID'ler arasında, ama favoriler Store ID veya User ID ile kaydediliyor
            // Store için: Store ID ile kaydediliyor, ama thread User ID'ler arasında
            // Bu yüzden Store ID'leri User ID'lere çevirip favori kontrolü yapıyoruz
            // ÖNEMLİ: Transaction commit edilmeden önce bu metod çağrılıyor olabilir (FavoriteManager'dan),
            // bu yüzden favori henüz DB'de görünmeyebilir. Bu durumda Store ID kontrolü yapıyoruz.
            
            bool isFavoriteActive = false;
            
            // Her iki yönde de favori kontrolü yap
            // 1. fromUserId -> toUserId yönünde
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }
            else
            {
                // Store ID kontrolü: toUserId bir Store Owner ID olabilir
                var store1 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == toUserId);
                if (store1 != null)
                {
                    // Store ID ile favori kontrolü yap
                    var favorite1Store = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == store1.Id && x.IsActive);
                    if (favorite1Store != null)
                        isFavoriteActive = true;
                }
            }
            
            // 2. toUserId -> fromUserId yönünde
            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
                else
                {
                    // Store ID kontrolü: fromUserId bir Store Owner ID olabilir
                    var store2 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == fromUserId);
                    if (store2 != null)
                    {
                        // Store ID ile favori kontrolü yap
                        var favorite2Store = await favoriteDal.Get(x => x.FavoritedFromId == toUserId && x.FavoritedToId == store2.Id && x.IsActive);
                        if (favorite2Store != null)
                            isFavoriteActive = true;
                    }
                }
            }
            
            // Eğer hiçbir tarafın favori aktif değilse thread gönderme (DB'de kalabilir ama SignalR ile gönderme)
            if (!isFavoriteActive)
            {
                // Thread DB'de kalabilir ama görünür olmamalı (GetThreadsAsync'te zaten filtreleniyor)
                // SignalR ile thread göndermiyoruz
                return new SuccessDataResult<Guid>(thread.Id);
            }

            // Her iki kullanıcı için de thread detaylarını al ve SignalR ile gönder
            // GetThreadsAsync mantığını kullanarak thread detaylarını doldur
            var recipients = new[] { fromUserId, toUserId }.Distinct().ToList();
            
            foreach (var recipientUserId in recipients)
            {
                try
                {
                    // Favori thread detaylarını oluştur
                    var otherUserId = thread.FavoriteFromUserId == recipientUserId 
                        ? thread.FavoriteToUserId!.Value 
                        : thread.FavoriteFromUserId!.Value;

                    var otherUser = await userDal.Get(u => u.Id == otherUserId);
                    if (otherUser == null) continue;

                    string displayName = "";
                    string? imageUrl = null;
                    BarberType? barberType = null;

                    if (otherUser.UserType == UserType.Customer)
                    {
                        displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                        if (otherUser.ImageId.HasValue)
                        {
                            var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else if (otherUser.UserType == UserType.BarberStore)
                    {
                        var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                        if (store != null)
                        {
                            displayName = store.StoreName;
                            barberType = store.Type;
                            var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else if (otherUser.UserType == UserType.FreeBarber)
                    {
                        var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                        if (freeBarber != null)
                        {
                            displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                            barberType = freeBarber.Type;
                            var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                            imageUrl = img?.ImageUrl;
                        }
                    }

                    // UnreadCount'u thread entity'den al
                    int unreadCount = 0;
                    if (thread.CustomerUserId == recipientUserId)
                        unreadCount = thread.CustomerUnreadCount;
                    else if (thread.StoreOwnerUserId == recipientUserId)
                        unreadCount = thread.StoreUnreadCount;
                    else if (thread.FreeBarberUserId == recipientUserId)
                        unreadCount = thread.FreeBarberUnreadCount;

                    var threadDto = new ChatThreadListItemDto
                    {
                        ThreadId = thread.Id,
                        AppointmentId = null,
                        Status = null,
                        IsFavoriteThread = true,
                        Title = displayName,
                        LastMessagePreview = thread.LastMessagePreview,
                        LastMessageAt = thread.LastMessageAt,
                        UnreadCount = unreadCount,
                        Participants = new List<ChatThreadParticipantDto>
                        {
                            new ChatThreadParticipantDto
                            {
                                UserId = otherUser.Id,
                                DisplayName = displayName,
                                ImageUrl = imageUrl,
                                UserType = otherUser.UserType,
                                BarberType = barberType
                            }
                        }
                    };

                    // Thread oluşturulduğunda veya güncellendiğinde SignalR ile bildir
                    if (isNewThread)
                        await realtime.PushChatThreadCreatedAsync(recipientUserId, threadDto);
                    else
                        await realtime.PushChatThreadUpdatedAsync(recipientUserId, threadDto);
                }
                catch
                {
                    // Hata durumunda devam et
                }
            }

            return new SuccessDataResult<Guid>(thread.Id);
        }

        public async Task PushAppointmentThreadCreatedAsync(Guid appointmentId)
        {
            // Appointment thread'i oluşturulduğunda veya güncellendiğinde tüm katılımcılara thread detaylarını gönder
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt == null) return;

            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            // Sadece Pending/Approved durumlarında thread görünür olmalı
            // Eğer durum Pending/Approved değilse threadRemoved event'i gönderilmeli (UpdateThreadOnAppointmentStatusChangeAsync'te yapılıyor)
            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
                return;

            var participants = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Store bilgisi
            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
            }

            // Kullanıcı bilgilerini batch olarak çek
            var users = await userDal.GetAll(u => participants.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id);

            // Store dict
            var storeDict = new Dictionary<Guid, BarberStore>();
            if (store != null)
            {
                storeDict[store.BarberStoreOwnerId] = store;
            }

            // FreeBarber dict
            var freeBarberDict = new Dictionary<Guid, FreeBarber>();
            if (appt.FreeBarberUserId.HasValue)
            {
                var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                if (freeBarber != null)
                {
                    freeBarberDict[freeBarber.FreeBarberUserId] = freeBarber;
                }
            }

            // Image'ları batch olarak çek (GetLatestImageAsync kullanarak)
            // User image'ları için user.Id (ImageOwnerId = user.Id, OwnerType = User)
            var userImageDict = new Dictionary<Guid, string?>();
            foreach (var user in users)
            {
                var img = await imageDal.GetLatestImageAsync(user.Id, ImageOwnerType.User);
                if (img != null)
                    userImageDict[user.Id] = img.ImageUrl;
            }

            // Store image'ları için store.Id (ImageOwnerId = store.Id, OwnerType = Store)
            var storeImageDict = new Dictionary<Guid, string?>();
            foreach (var storeEntity in storeDict.Values)
            {
                var img = await imageDal.GetLatestImageAsync(storeEntity.Id, ImageOwnerType.Store);
                if (img != null)
                    storeImageDict[storeEntity.Id] = img.ImageUrl;
            }

            // FreeBarber image'ları için freeBarber.Id (ImageOwnerId = freeBarber.Id, OwnerType = FreeBarber)
            var freeBarberImageDict = new Dictionary<Guid, string?>();
            foreach (var freeBarberEntity in freeBarberDict.Values)
            {
                var img = await imageDal.GetLatestImageAsync(freeBarberEntity.Id, ImageOwnerType.FreeBarber);
                if (img != null)
                    freeBarberImageDict[freeBarberEntity.Id] = img.ImageUrl;
            }

            foreach (var userId in participants)
            {
                try
                {
                    var title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Participants listesini oluştur
                    var participantsList = new List<ChatThreadParticipantDto>();

                    // Customer participant
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customerUser))
                        {
                            userImageDict.TryGetValue(customerUser.Id, out var customerImageUrl);
                            participantsList.Add(new ChatThreadParticipantDto
                            {
                                UserId = customerUser.Id,
                                DisplayName = $"{customerUser.FirstName} {customerUser.LastName}",
                                ImageUrl = customerImageUrl,
                                UserType = customerUser.UserType,
                                BarberType = null
                            });
                        }
                    }

                    // Store participant
                    if (appt.BarberStoreUserId.HasValue && appt.BarberStoreUserId.Value != userId)
                    {
                        if (storeDict.TryGetValue(appt.BarberStoreUserId.Value, out var storeEntity))
                        {
                            storeImageDict.TryGetValue(storeEntity.Id, out var storeImageUrl);
                            participantsList.Add(new ChatThreadParticipantDto
                            {
                                UserId = appt.BarberStoreUserId.Value,
                                DisplayName = storeEntity.StoreName,
                                ImageUrl = storeImageUrl,
                                UserType = UserType.BarberStore,
                                BarberType = storeEntity.Type
                            });
                        }
                    }

                    // FreeBarber participant
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                    {
                        if (freeBarberDict.TryGetValue(appt.FreeBarberUserId.Value, out var freeBarberEntity))
                        {
                            freeBarberImageDict.TryGetValue(freeBarberEntity.Id, out var freeBarberImageUrl);
                            participantsList.Add(new ChatThreadParticipantDto
                            {
                                UserId = appt.FreeBarberUserId.Value,
                                DisplayName = $"{freeBarberEntity.FirstName} {freeBarberEntity.LastName}",
                                ImageUrl = freeBarberImageUrl,
                                UserType = UserType.FreeBarber,
                                BarberType = freeBarberEntity.Type
                            });
                        }
                    }

                    // UnreadCount'u thread entity'den al
                    int unreadCount = 0;
                    if (thread.CustomerUserId == userId)
                        unreadCount = thread.CustomerUnreadCount;
                    else if (thread.StoreOwnerUserId == userId)
                        unreadCount = thread.StoreUnreadCount;
                    else if (thread.FreeBarberUserId == userId)
                        unreadCount = thread.FreeBarberUnreadCount;

                    var threadDto = new ChatThreadListItemDto
                    {
                        ThreadId = thread.Id,
                        AppointmentId = appt.Id,
                        Status = appt.Status,
                        IsFavoriteThread = false,
                        Title = title,
                        LastMessagePreview = thread.LastMessagePreview,
                        LastMessageAt = thread.LastMessageAt,
                        UnreadCount = unreadCount,
                        Participants = participantsList
                    };

                    // ThreadCreated gönder (yeni thread oluşturulduğunda)
                    await realtime.PushChatThreadCreatedAsync(userId, threadDto);
                }
                catch
                {
                    // Hata durumunda devam et
                }
            }
        }

        public async Task PushAppointmentThreadUpdatedAsync(Guid appointmentId)
        {
            // Appointment thread'i güncellendiğinde (status değiştiğinde) tüm katılımcılara thread detaylarını gönder
            // PushAppointmentThreadCreatedAsync ile aynı mantık, ama threadUpdated event'i gönderir
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt == null) return;

            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            // Sadece Pending/Approved durumlarında thread görünür olmalı
            // Eğer durum Pending/Approved değilse threadRemoved event'i gönderilmeli (UpdateThreadOnAppointmentStatusChangeAsync'te yapılıyor)
            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
                return;

            var participants = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Store bilgisi
            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
            }

            // Kullanıcı bilgilerini batch olarak çek
            var users = await userDal.GetAll(u => participants.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id);

            // Store dict
            var storeDict = new Dictionary<Guid, BarberStore>();
            if (store != null)
            {
                storeDict[store.BarberStoreOwnerId] = store;
            }

            // FreeBarber dict
            var freeBarberDict = new Dictionary<Guid, FreeBarber>();
            if (appt.FreeBarberUserId.HasValue)
            {
                var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                if (freeBarber != null)
                {
                    freeBarberDict[freeBarber.FreeBarberUserId] = freeBarber;
                }
            }

            // Image'ları batch olarak çek (GetLatestImageAsync kullanarak)
            // User image'ları için user.Id (ImageOwnerId = user.Id, OwnerType = User)
            var userImageDict = new Dictionary<Guid, string?>();
            foreach (var user in users)
            {
                var img = await imageDal.GetLatestImageAsync(user.Id, ImageOwnerType.User);
                if (img != null)
                    userImageDict[user.Id] = img.ImageUrl;
            }

            // Store image'ları için store.Id (ImageOwnerId = store.Id, OwnerType = Store)
            var storeImageDict = new Dictionary<Guid, string?>();
            foreach (var storeEntity in storeDict.Values)
            {
                var img = await imageDal.GetLatestImageAsync(storeEntity.Id, ImageOwnerType.Store);
                if (img != null)
                    storeImageDict[storeEntity.Id] = img.ImageUrl;
            }

            // FreeBarber image'ları için freeBarber.Id (ImageOwnerId = freeBarber.Id, OwnerType = FreeBarber)
            var freeBarberImageDict = new Dictionary<Guid, string?>();
            foreach (var freeBarberEntity in freeBarberDict.Values)
            {
                var img = await imageDal.GetLatestImageAsync(freeBarberEntity.Id, ImageOwnerType.FreeBarber);
                if (img != null)
                    freeBarberImageDict[freeBarberEntity.Id] = img.ImageUrl;
            }

            foreach (var userId in participants)
            {
                try
                {
                    var title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Participants listesini oluştur
                    var participantsList = new List<ChatThreadParticipantDto>();

                    // Customer participant
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customerUser))
                        {
                            userImageDict.TryGetValue(customerUser.Id, out var customerImageUrl);
                            participantsList.Add(new ChatThreadParticipantDto
                            {
                                UserId = customerUser.Id,
                                DisplayName = $"{customerUser.FirstName} {customerUser.LastName}",
                                ImageUrl = customerImageUrl,
                                UserType = customerUser.UserType,
                                BarberType = null
                            });
                        }
                    }

                    // Store participant
                    if (appt.BarberStoreUserId.HasValue && appt.BarberStoreUserId.Value != userId)
                    {
                        if (storeDict.TryGetValue(appt.BarberStoreUserId.Value, out var storeEntity))
                        {
                            storeImageDict.TryGetValue(storeEntity.Id, out var storeImageUrl);
                            participantsList.Add(new ChatThreadParticipantDto
                            {
                                UserId = appt.BarberStoreUserId.Value,
                                DisplayName = storeEntity.StoreName,
                                ImageUrl = storeImageUrl,
                                UserType = UserType.BarberStore,
                                BarberType = storeEntity.Type
                            });
                        }
                    }

                    // FreeBarber participant
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                    {
                        if (freeBarberDict.TryGetValue(appt.FreeBarberUserId.Value, out var freeBarberEntity))
                        {
                            freeBarberImageDict.TryGetValue(freeBarberEntity.Id, out var freeBarberImageUrl);
                            participantsList.Add(new ChatThreadParticipantDto
                            {
                                UserId = appt.FreeBarberUserId.Value,
                                DisplayName = $"{freeBarberEntity.FirstName} {freeBarberEntity.LastName}",
                                ImageUrl = freeBarberImageUrl,
                                UserType = UserType.FreeBarber,
                                BarberType = freeBarberEntity.Type
                            });
                        }
                    }

                    // UnreadCount'u thread entity'den al
                    int unreadCount = 0;
                    if (thread.CustomerUserId == userId)
                        unreadCount = thread.CustomerUnreadCount;
                    else if (thread.StoreOwnerUserId == userId)
                        unreadCount = thread.StoreUnreadCount;
                    else if (thread.FreeBarberUserId == userId)
                        unreadCount = thread.FreeBarberUnreadCount;

                    var threadDto = new ChatThreadListItemDto
                    {
                        ThreadId = thread.Id,
                        AppointmentId = appt.Id,
                        Status = appt.Status,
                        IsFavoriteThread = false,
                        Title = title,
                        LastMessagePreview = thread.LastMessagePreview,
                        LastMessageAt = thread.LastMessageAt,
                        UnreadCount = unreadCount,
                        Participants = participantsList
                    };

                    // ThreadUpdated gönder (mevcut thread güncellendiğinde)
                    await realtime.PushChatThreadUpdatedAsync(userId, threadDto);
                }
                catch
                {
                    // Hata durumunda devam et
                }
            }
        }

        public async Task PushFavoriteThreadUpdatedAsync(Guid fromUserId, Guid toUserId, Guid threadId)
        {
            // Favori thread güncellendiğinde (mesaj gönderildiğinde) her iki kullanıcıya da thread güncellemesini push et
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread == null) return;

            // Favori aktif mi kontrol et
            bool isFavoriteActive = false;
            
            // 1. fromUserId -> toUserId yönünde
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }
            else
            {
                // Store ID kontrolü: toUserId bir Store Owner ID olabilir
                var store1 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == toUserId);
                if (store1 != null)
                {
                    var favorite1Store = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == store1.Id && x.IsActive);
                    if (favorite1Store != null)
                        isFavoriteActive = true;
                }
            }
            
            // 2. toUserId -> fromUserId yönünde
            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
                else
                {
                    // Store ID kontrolü: fromUserId bir Store Owner ID olabilir
                    var store2 = await barberStoreDal.Get(x => x.BarberStoreOwnerId == fromUserId);
                    if (store2 != null)
                    {
                        var favorite2Store = await favoriteDal.Get(x => x.FavoritedFromId == toUserId && x.FavoritedToId == store2.Id && x.IsActive);
                        if (favorite2Store != null)
                            isFavoriteActive = true;
                    }
                }
            }
            
            // Eğer hiçbir tarafın favori aktif değilse thread gönderme
            if (!isFavoriteActive)
            {
                return;
            }

            // Her iki kullanıcı için de thread detaylarını al ve SignalR ile gönder
            var recipients = new[] { fromUserId, toUserId }.Distinct().ToList();
            
            foreach (var recipientUserId in recipients)
            {
                try
                {
                    // Favori thread detaylarını oluştur
                    var otherUserId = thread.FavoriteFromUserId == recipientUserId 
                        ? thread.FavoriteToUserId!.Value 
                        : thread.FavoriteFromUserId!.Value;

                    var otherUser = await userDal.Get(u => u.Id == otherUserId);
                    if (otherUser == null) continue;

                    string displayName = "";
                    string? imageUrl = null;
                    BarberType? barberType = null;

                    if (otherUser.UserType == UserType.Customer)
                    {
                        displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                        if (otherUser.ImageId.HasValue)
                        {
                            var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else if (otherUser.UserType == UserType.BarberStore)
                    {
                        var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                        if (store != null)
                        {
                            displayName = store.StoreName;
                            barberType = store.Type;
                            var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else if (otherUser.UserType == UserType.FreeBarber)
                    {
                        var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                        if (freeBarber != null)
                        {
                            displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                            barberType = freeBarber.Type;
                            var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                            imageUrl = img?.ImageUrl;
                        }
                    }

                    // UnreadCount'u thread entity'den al
                    int unreadCount = 0;
                    if (thread.CustomerUserId == recipientUserId)
                        unreadCount = thread.CustomerUnreadCount;
                    else if (thread.StoreOwnerUserId == recipientUserId)
                        unreadCount = thread.StoreUnreadCount;
                    else if (thread.FreeBarberUserId == recipientUserId)
                        unreadCount = thread.FreeBarberUnreadCount;

                    var threadDto = new ChatThreadListItemDto
                    {
                        ThreadId = thread.Id,
                        AppointmentId = null,
                        Status = null,
                        IsFavoriteThread = true,
                        Title = displayName,
                        LastMessagePreview = thread.LastMessagePreview,
                        LastMessageAt = thread.LastMessageAt,
                        UnreadCount = unreadCount,
                        Participants = new List<ChatThreadParticipantDto>
                        {
                            new ChatThreadParticipantDto
                            {
                                UserId = otherUser.Id,
                                DisplayName = displayName,
                                ImageUrl = imageUrl,
                                UserType = otherUser.UserType,
                                BarberType = barberType
                            }
                        }
                    };

                    // ThreadUpdated gönder
                    await realtime.PushChatThreadUpdatedAsync(recipientUserId, threadDto);
                }
                catch
                {
                    // Hata durumunda devam et
                }
            }
        }

        public async Task<IDataResult<bool>> NotifyTypingAsync(Guid userId, Guid threadId, bool isTyping)
        {
            // Thread'i kontrol et
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread == null) return new ErrorDataResult<bool>(Messages.ChatNotFound);

            // Katılımcı kontrolü
            bool isParticipant = false;
            string? userName = null;

            if (thread.AppointmentId.HasValue)
            {
                // Randevu thread'i
                var appt = await appointmentDal.Get(x => x.Id == thread.AppointmentId.Value);
                if (appt == null) return new ErrorDataResult<bool>(Messages.AppointmentNotFound);
                
                isParticipant = thread.CustomerUserId == userId || 
                               thread.StoreOwnerUserId == userId || 
                               thread.FreeBarberUserId == userId;
            }
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                // Favori thread
                isParticipant = thread.FavoriteFromUserId == userId || thread.FavoriteToUserId == userId;
            }

            if (!isParticipant) return new ErrorDataResult<bool>(Messages.NotAParticipant);

            // Kullanıcı adını al
            var user = await userDal.Get(u => u.Id == userId);
            if (user != null)
            {
                if (user.UserType == UserType.Customer)
                {
                    userName = $"{user.FirstName} {user.LastName}";
                }
                else if (user.UserType == UserType.BarberStore)
                {
                    var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                    userName = store?.StoreName ?? "Berber";
                }
                else if (user.UserType == UserType.FreeBarber)
                {
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                    userName = freeBarber != null ? $"{freeBarber.FirstName} {freeBarber.LastName}" : "Serbest Berber";
                }
            }

            // Thread'deki diğer katılımcılara typing event'i gönder
            var participants = new List<Guid>();
            
            if (thread.CustomerUserId.HasValue && thread.CustomerUserId != userId)
                participants.Add(thread.CustomerUserId.Value);
            if (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId != userId)
                participants.Add(thread.StoreOwnerUserId.Value);
            if (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId != userId)
                participants.Add(thread.FreeBarberUserId.Value);

            foreach (var participantId in participants.Distinct())
            {
                await realtime.PushChatTypingAsync(participantId, threadId, userId, userName ?? "Kullanıcı", isTyping);
            }

            return new SuccessDataResult<bool>(true);
        }
    }
}
