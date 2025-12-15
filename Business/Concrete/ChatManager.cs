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

            // push -> tüm katılımcılara
            var recipients = new[] { thread.CustomerUserId, thread.StoreOwnerUserId, thread.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            foreach (var u in recipients)
            {
                await realtime.PushChatMessageAsync(u, dto);
                var badges = await badgeSvc.GetCountsAsync(u);
                if (badges.Success) await realtime.PushBadgeAsync(u, badges.Data);
            }

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
                var threadDict = threadEntities.ToDictionary(t => t.AppointmentId!.Value);

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
                var storeDict = stores.ToDictionary(s => s.BarberStoreOwnerId);

                var freeBarberIds = appointments.Where(a => a.FreeBarberUserId.HasValue)
                    .Select(a => a.FreeBarberUserId!.Value).Distinct().ToList();
                var freeBarbers = freeBarberIds.Any()
                    ? await freeBarberDal.GetAll(x => freeBarberIds.Contains(x.FreeBarberUserId))
                    : new List<FreeBarber>();
                var freeBarberDict = freeBarbers.ToDictionary(fb => fb.FreeBarberUserId);

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

                var userImageDict = userImages.ToDictionary(i => i.Id);
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
                    // Her iki yönde de kontrol et: fromUserId -> toUserId ve toUserId -> fromUserId
                    var favorite1 = await favoriteDal.GetByUsersAsync(threadEntity.FavoriteFromUserId!.Value, threadEntity.FavoriteToUserId!.Value);
                    var favorite2 = await favoriteDal.GetByUsersAsync(threadEntity.FavoriteToUserId!.Value, threadEntity.FavoriteFromUserId!.Value);
                    
                    // En az bir tarafın favori olması yeterli (aktif olmalı)
                    var isFavoriteActive = (favorite1 != null && favorite1.IsActive) || (favorite2 != null && favorite2.IsActive);
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
            var favorite1 = await favoriteDal.GetByUsersAsync(thread.FavoriteFromUserId!.Value, thread.FavoriteToUserId!.Value);
            var favorite2 = await favoriteDal.GetByUsersAsync(thread.FavoriteToUserId!.Value, thread.FavoriteFromUserId!.Value);
            
            // En az bir tarafın favori olması yeterli (aktif olmalı)
            var isFavoriteActive = (favorite1 != null && favorite1.IsActive) || (favorite2 != null && favorite2.IsActive);
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

            // Push -> karşı tarafa
            if (otherUserId.HasValue)
            {
                await realtime.PushChatMessageAsync(otherUserId.Value, dto);
                var badges = await badgeSvc.GetCountsAsync(otherUserId.Value);
                if (badges.Success) await realtime.PushBadgeAsync(otherUserId.Value, badges.Data);
            }

            // Sender'a da push et (kendi mesajını görmesi için)
            await realtime.PushChatMessageAsync(senderUserId, dto);
            var senderBadges = await badgeSvc.GetCountsAsync(senderUserId);
            if (senderBadges.Success) await realtime.PushBadgeAsync(senderUserId, senderBadges.Data);

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
                var favorite1 = await favoriteDal.GetByUsersAsync(thread.FavoriteFromUserId.Value, thread.FavoriteToUserId.Value);
                var favorite2 = await favoriteDal.GetByUsersAsync(thread.FavoriteToUserId.Value, thread.FavoriteFromUserId.Value);
                
                // En az bir tarafın favori olması yeterli (aktif olmalı)
                var isFavoriteActive = (favorite1 != null && favorite1.IsActive) || (favorite2 != null && favorite2.IsActive);
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
            
            if (existingThread != null)
            {
                // Thread zaten var, thread ID'yi döndür
                return new SuccessDataResult<Guid>(existingThread.Id);
            }

            // Yeni thread oluştur
            // Thread'deki kullanıcı tiplerini belirle
            var fromUser = await userDal.Get(u => u.Id == fromUserId);
            var toUser = await userDal.Get(u => u.Id == toUserId);
            
            if (fromUser == null || toUser == null)
                return new ErrorDataResult<Guid>("Kullanıcı bulunamadı");

            var newThread = new ChatThread
            {
                Id = Guid.NewGuid(),
                AppointmentId = null,
                FavoriteFromUserId = fromUserId,
                FavoriteToUserId = toUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Her iki kullanıcının UserType'ına göre CustomerUserId, StoreOwnerUserId veya FreeBarberUserId'yi set et
            // fromUser için
            if (fromUser.UserType == UserType.Customer)
                newThread.CustomerUserId = fromUserId;
            else if (fromUser.UserType == UserType.BarberStore)
                newThread.StoreOwnerUserId = fromUserId;
            else if (fromUser.UserType == UserType.FreeBarber)
                newThread.FreeBarberUserId = fromUserId;

            // toUser için (eğer fromUser ile aynı UserType değilse)
            if (toUser.UserType == UserType.Customer && newThread.CustomerUserId != toUserId)
                newThread.CustomerUserId = toUserId;
            else if (toUser.UserType == UserType.BarberStore && newThread.StoreOwnerUserId != toUserId)
                newThread.StoreOwnerUserId = toUserId;
            else if (toUser.UserType == UserType.FreeBarber && newThread.FreeBarberUserId != toUserId)
                newThread.FreeBarberUserId = toUserId;

            await threadDal.Add(newThread);

            // SignalR ile thread oluşturulduğunu bildir (GetThreadsAsync'te detaylar doldurulacak)
            var threadDto = new ChatThreadListItemDto
            {
                ThreadId = newThread.Id,
                AppointmentId = null,
                Status = null,
                IsFavoriteThread = true,
                Title = "", // GetThreadsAsync'te doldurulacak
                UnreadCount = 0
            };

            await realtime.PushChatThreadCreatedAsync(fromUserId, threadDto);
            await realtime.PushChatThreadCreatedAsync(toUserId, threadDto);

            return new SuccessDataResult<Guid>(newThread.Id);
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
