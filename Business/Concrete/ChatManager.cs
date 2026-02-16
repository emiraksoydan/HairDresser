using Autofac;
using Business.Abstract;
using Business.BusinessAspect.Autofac;

using Business.Helpers;
using Business.Resources;
using Core.Aspect.Autofac.ExceptionHandling;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class ChatManager(
             IAppointmentDal appointmentDal,
             IChatThreadDal threadDal,
             IChatMessageDal messageDal,
             IBarberStoreDal barberStoreDal,
             IUserDal userDal,
             IFreeBarberDal freeBarberDal,
             IImageDal imageDal,
             IFavoriteDal favoriteDal,
             IMessageReadReceiptDal receiptDal,
             IRealTimePublisher realtime,
             FavoriteHelper favoriteHelper,
             BadgeService badgeService,
             IContentModerationService contentModeration,
             IMessageEncryptionService messageEncryption,
             ILifetimeScope lifetimeScope,
             ILogger<ChatManager> logger
     ) : IChatService
    {

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return new ErrorDataResult<ChatMessageDto>(Messages.EmptyMessage);

            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<ChatMessageDto>(Messages.AppointmentNotFound);

            // yetki: sender katılımcı mı?
            var isParticipant =
                appt.CustomerUserId == senderUserId ||
                appt.FreeBarberUserId == senderUserId ||
                appt.BarberStoreUserId == senderUserId;

            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>(Messages.NotAParticipant);

            // Mesaj gönderme kontrolü: Randevu status kontrolü VEYA favori kontrolü
            bool canSendMessage = false;

            // 1. Randevu status kontrolü: Pending veya Approved ise mesaj gönderilebilir
            if (appt.Status is AppointmentStatus.Pending or AppointmentStatus.Approved)
            {
                canSendMessage = true;
            }
            // 2. Randevu status uygun değilse, favori kontrolü yap
            else
            {
                // Katılımcılar arasında aktif favori var mı kontrol et
                var participants = new[] { appt.CustomerUserId, appt.FreeBarberUserId, appt.BarberStoreUserId }
                    .Where(x => x.HasValue && x.Value != senderUserId)
                    .Select(x => x!.Value)
                    .ToList();

                // Store ID'yi al (eğer varsa)
                Guid? storeId = null;
                if (appt.BarberStoreUserId.HasValue)
                {
                    var storeForFavoriteCheck = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
                    if (storeForFavoriteCheck != null)
                    {
                        storeId = storeForFavoriteCheck.Id;
                    }
                }

                // Optimized: Tek query ile tüm favori kontrolü
                canSendMessage = await favoriteHelper.IsFavoriteActiveMultipleParticipantsAsync(
                    senderUserId,
                    participants,
                    storeId
                );
            }

            if (!canSendMessage)
                return new ErrorDataResult<ChatMessageDto>(Messages.MessageRequiresActiveAppointmentOrFavorite);

            // Performance: Use Get instead of GetAll().FirstOrDefault()
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            BarberStore? barberStore = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                barberStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId);
                if (barberStore is null)
                    return new ErrorDataResult<ChatMessageDto>(Messages.StoreNotFound);
            }

            if (thread is null)
            {
                thread = new ChatThread
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = appointmentId,
                    CustomerUserId = appt.CustomerUserId,
                    StoreOwnerUserId = appt.BarberStoreUserId,
                    FreeBarberUserId = appt.FreeBarberUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await threadDal.Add(thread);
            }

            // Mesaj metnini şifrele (DB'ye kaydedilecek)
            var encryptedText = messageEncryption.Encrypt(text);
            var previewText = text.Length > 60 ? text[..60] : text;
            var encryptedPreview = messageEncryption.Encrypt(previewText);

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AppointmentId = appointmentId,
                SenderUserId = senderUserId,
                Text = encryptedText,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            };
            await messageDal.Add(msg);

            thread.LastMessageAt = msg.CreatedAt;
            thread.LastMessagePreview = encryptedPreview;
            thread.UpdatedAt = DateTime.UtcNow;

            // unread arttır (sender dışındaki katılımcılara)
            if (thread.CustomerUserId.HasValue && thread.CustomerUserId != senderUserId) thread.CustomerUnreadCount++;
            if (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId != senderUserId) thread.StoreUnreadCount++;
            if (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId != senderUserId) thread.FreeBarberUnreadCount++;

            await threadDal.Update(thread);

            // SignalR DTO'su plaintext kullanır (TLS ile korunur)
            var dto = new ChatMessageDto
            {
                ThreadId = thread.Id,
                AppointmentId = appointmentId,
                MessageId = msg.Id,
                SenderUserId = senderUserId,
                Text = text,
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
            }

            // Thread güncellemesini tüm katılımcılara push et (LastMessagePreview, LastMessageAt, UnreadCount değişti)
            await PushAppointmentThreadUpdatedAsync(appointmentId);

            // Badge update: sender dışındaki katılımcılar için badge count'u güncelle
            var recipientsForBadgeUpdate = recipients.Where(u => u != senderUserId).ToList();
            if (recipientsForBadgeUpdate.Any())
            {
                await badgeService.NotifyBadgeChangeBatchAsync(recipientsForBadgeUpdate, BadgeChangeReason.MessageReceived);
            }

            // Fire-and-forget moderation: mesaj anında gönderilir, arka planda kontrol edilir
            var messageIdForModeration = msg.Id;
            var threadIdForModeration = thread.Id;
            var recipientsSnapshot = recipients.ToList();
            _ = Task.Run(() => ModerateAndRemoveIfFlaggedAsync(messageIdForModeration, text, threadIdForModeration, recipientsSnapshot));

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        private async Task ModerateAndRemoveIfFlaggedAsync(Guid messageId, string plainText, Guid threadId, List<Guid> recipients)
        {
            try
            {
                await using var scope = lifetimeScope.BeginLifetimeScope();
                var moderation = scope.Resolve<IContentModerationService>();
                var msgDal = scope.Resolve<IChatMessageDal>();
                var rt = scope.Resolve<IRealTimePublisher>();

                var result = await moderation.CheckContentAsync(plainText);
                if (result.Success) return;

                var msgToDelete = await msgDal.Get(x => x.Id == messageId);
                if (msgToDelete != null)
                    await msgDal.Remove(msgToDelete);

                foreach (var userId in recipients)
                {
                    try { await rt.PushChatMessageRemovedAsync(userId, threadId, messageId); } catch { }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background moderation failed for message {MessageId}", messageId);
            }
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
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

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> MarkThreadReadByAppointmentAsync(Guid userId, Guid appointmentId)
        {
            return await MarkThreadReadByAppointmentInternalAsync(userId, appointmentId);
        }

        /// <summary>
        /// System/Background worker kullanımı için - TransactionScope olmadan
        /// Worker kendi transaction'ını yönettiği için nested transaction hatası vermez
        /// </summary>
        [LogAspect]
        public async Task<IDataResult<bool>> MarkThreadReadByAppointmentSystemAsync(Guid userId, Guid appointmentId)
        {
            return await MarkThreadReadByAppointmentInternalAsync(userId, appointmentId);
        }

        [ExceptionHandlingAspect]
        private async Task<IDataResult<bool>> MarkThreadReadByAppointmentInternalAsync(Guid userId, Guid appointmentId)
        {
            // Randevu thread'i için okundu işaretleme (geriye dönük uyumluluk)
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread is null) return new ErrorDataResult<bool>(false, Messages.ChatNotFound);

            bool needsUpdate = false;

            if (thread.CustomerUserId == userId)
            {
                if (thread.CustomerUnreadCount > 0)
                {
                    thread.CustomerUnreadCount = 0;
                    needsUpdate = true;
                }
            }
            else if (thread.StoreOwnerUserId == userId)
            {
                if (thread.StoreUnreadCount > 0)
                {
                    thread.StoreUnreadCount = 0;
                    needsUpdate = true;
                }
            }
            else if (thread.FreeBarberUserId == userId)
            {
                if (thread.FreeBarberUnreadCount > 0)
                {
                    thread.FreeBarberUnreadCount = 0;
                    needsUpdate = true;
                }
            }
            else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);

            if (needsUpdate)
            {
                await threadDal.Update(thread);

                // Read receipt'leri işle ve çift tik bildirimi gönder
                await ProcessReadReceiptsAsync(thread, userId);

                // Badge count'u güncelle (kendim için) - chat unread count hesapla
                var userThreads = await threadDal.GetAll(t =>
                    t.CustomerUserId == userId || t.StoreOwnerUserId == userId || t.FreeBarberUserId == userId);
                var chatUnreadCount = userThreads.Sum(t =>
                    t.CustomerUserId == userId ? t.CustomerUnreadCount :
                    t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
                    t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);
                await realtime.PushBadgeUpdateAsync(userId, chatUnreadCount: chatUnreadCount);
            }

            return new SuccessDataResult<bool>(true);
        }


        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        // Read-only query - no transaction needed
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

            // Mevcut kullanıcının UserType'ını al
            var currentUser = await userDal.Get(u => u.Id == userId);
            if (currentUser == null)
                return new ErrorDataResult<List<ChatThreadListItemDto>>(Messages.UserNotFound);
            var currentUserType = currentUser.UserType;

            // Mevcut kullanıcının profil resmini al
            string? currentUserImageUrl = null;
            if (currentUserType == UserType.Customer)
            {
                // Customer için User image
                if (currentUser.ImageId.HasValue)
                {
                    var userImg = await imageDal.GetLatestImageAsync(currentUser.Id, ImageOwnerType.User);
                    currentUserImageUrl = userImg?.ImageUrl;
                }
            }
            else if (currentUserType == UserType.BarberStore)
            {
                // BarberStore için Store image
                var userStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == userId);
                if (userStore != null)
                {
                    var storeImg = await imageDal.GetLatestImageAsync(userStore.Id, ImageOwnerType.Store);
                    currentUserImageUrl = storeImg?.ImageUrl;
                }
            }
            else if (currentUserType == UserType.FreeBarber)
            {
                // FreeBarber için FreeBarber image
                var userFreeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                if (userFreeBarber != null)
                {
                    var freeBarberImg = await imageDal.GetLatestImageAsync(userFreeBarber.Id, ImageOwnerType.FreeBarber);
                    currentUserImageUrl = freeBarberImg?.ImageUrl;
                }
            }

            // Randevu thread'leri için işlem
            if (appointmentThreads.Any())
            {
                // Performance: HashSet kullanarak daha hızlı Contains kontrolü
                var appointmentIds = new HashSet<Guid>(appointmentThreads.Select(t => t.AppointmentId!.Value));
                var appointments = await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id));
                var apptDict = appointments.ToDictionary(a => a.Id);

                // Thread entity'lerini getir (participant bilgileri için)
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

                // Performance: HashSet kullanarak daha hızlı lookup
                var storeOwnerIds = appointments
                    .Where(a => a.BarberStoreUserId.HasValue)
                    .Select(a => a.BarberStoreUserId!.Value)
                    .Distinct()
                    .ToHashSet();
                var stores = storeOwnerIds.Count > 0
                    ? await barberStoreDal.GetAll(x => storeOwnerIds.Contains(x.BarberStoreOwnerId))
                    : new List<BarberStore>();
                // GroupBy kullanarak duplicate BarberStoreOwnerId'leri handle et (her owner için en son store'u al)
                var storeDict = stores
                    .GroupBy(s => s.BarberStoreOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

                // Performance: HashSet kullanarak daha hızlı lookup
                var freeBarberIds = appointments
                    .Where(a => a.FreeBarberUserId.HasValue)
                    .Select(a => a.FreeBarberUserId!.Value)
                    .Distinct()
                    .ToHashSet();
                var freeBarbers = freeBarberIds.Count > 0
                    ? await freeBarberDal.GetAll(x => freeBarberIds.Contains(x.FreeBarberUserId))
                    : new List<FreeBarber>();
                // GroupBy kullanarak duplicate FreeBarberUserId'leri handle et (her user için en son freeBarber'ı al)
                var freeBarberDict = freeBarbers
                    .GroupBy(fb => fb.FreeBarberUserId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(fb => fb.CreatedAt).First());

                // Performance: HashSet kullanarak daha hızlı Contains kontrolü
                var userImageIds = users
                    .Where(u => u.ImageId.HasValue)
                    .Select(u => u.ImageId!.Value)
                    .Distinct()
                    .ToHashSet();
                var storeImageOwnerIds = stores.Select(s => s.Id).ToHashSet();
                var freeBarberImageOwnerIds = freeBarbers.Select(fb => fb.Id).ToHashSet();

                var userImages = userImageIds.Count > 0
                    ? await imageDal.GetAll(i => userImageIds.Contains(i.Id) && i.OwnerType == ImageOwnerType.User)
                    : new List<Image>();
                var storeImages = storeImageOwnerIds.Count > 0
                    ? await imageDal.GetAll(i => storeImageOwnerIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.Store)
                    : new List<Image>();
                var freeBarberImages = freeBarberImageOwnerIds.Count > 0
                    ? await imageDal.GetAll(i => freeBarberImageOwnerIds.Contains(i.ImageOwnerId) && i.OwnerType == ImageOwnerType.FreeBarber)
                    : new List<Image>();

                // GroupBy kullanarak duplicate ImageId'leri handle et (her image için en son olanı al)
                var userImageDict = userImages
                    .GroupBy(i => i.Id)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First());
                var storeImageDict = storeImages
                    .GroupBy(i => i.ImageOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);
                var freeBarberImageDict = freeBarberImages
                    .GroupBy(i => i.ImageOwnerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);

                // Her randevu thread'i için işlem
                foreach (var threadDto in appointmentThreads)
                {
                    if (!apptDict.TryGetValue(threadDto.AppointmentId!.Value, out var appt)) continue;
                    if (!threadDict.TryGetValue(threadDto.AppointmentId.Value, out var threadEntity)) continue;

                    // ÖNEMLİ: Appointment thread görünürlüğü kontrolü
                    // Thread görünür olması için iki koşuldan biri sağlanmalı:
                    // 1. Status Pending veya Approved ise görünür
                    // 2. Status Pending/Approved değilse, en az bir aktif favori varsa görünür (favori thread mantığı gibi)
                    bool isStatusVisible = appt.Status == AppointmentStatus.Pending || appt.Status == AppointmentStatus.Approved;
                    bool isFavoriteVisible = false;

                    // Store bilgisini al (favori kontrolü için)
                    storeDict.TryGetValue(appt.BarberStoreUserId ?? Guid.Empty, out var store);

                    // Status uygun değilse favori kontrolü yap
                    if (!isStatusVisible)
                    {
                        // Katılımcıları belirle (kendimiz hariç)
                        var threadParticipantIds = new List<Guid>();
                        if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                            threadParticipantIds.Add(appt.CustomerUserId.Value);
                        if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                            threadParticipantIds.Add(appt.FreeBarberUserId.Value);
                        if (appt.BarberStoreUserId.HasValue && appt.BarberStoreUserId.Value != userId)
                            threadParticipantIds.Add(appt.BarberStoreUserId.Value);

                        // Store bazlı favori kontrolü (eğer store varsa)
                        if (store != null && store.BarberStoreOwnerId != userId)
                        {
                            var favoriteStore = await favoriteDal.Get(x =>
                                x.FavoritedFromId == userId &&
                                x.FavoritedToId == store.Id &&
                                x.IsActive);
                            if (favoriteStore != null)
                            {
                                isFavoriteVisible = true;
                            }
                        }

                        // Performance: User bazlı favori kontrolü - batch olarak çek
                        if (!isFavoriteVisible && threadParticipantIds.Any())
                        {
                            // Tüm participant ID'leri için favorileri batch olarak çek
                            var participantIdsList = threadParticipantIds.ToList();
                            var favorites1 = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == userId &&
                                participantIdsList.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            var favorites2 = await favoriteDal.GetAll(x =>
                                participantIdsList.Contains(x.FavoritedFromId) &&
                                x.FavoritedToId == userId &&
                                x.IsActive);

                            if (favorites1.Any() || favorites2.Any())
                            {
                                isFavoriteVisible = true;
                            }
                        }
                    }

                    // Thread görünür değilse atla
                    if (!isStatusVisible && !isFavoriteVisible)
                    {
                        continue;
                    }
                    threadDto.Title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Status'u appointment'tan güncelle (threadDto.Status zaten appointment'tan geliyor ama güncel olması için)
                    threadDto.Status = appt.Status;

                    // Participants listesini doldur - ÖNEMLİ: Sadece kendi userId'miz hariç diğer participant'ları göster
                    threadDto.Participants = new List<ChatThreadParticipantDto>();

                    // Customer - sadece kendimiz değilsek ekle
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customer))
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (threadEntity.CustomerUserId == appt.CustomerUserId.Value)
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
                    }

                    // Store - sadece kendimiz değilsek ekle
                    if (appt.BarberStoreUserId.HasValue && store != null)
                    {
                        // Store sahibi userId'si ile karşılaştır
                        if (store.BarberStoreOwnerId != userId)
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (threadEntity.StoreOwnerUserId == store.BarberStoreOwnerId ||
                                (threadEntity.StoreOwnerUserId.HasValue && threadEntity.StoreOwnerUserId.Value == store.BarberStoreOwnerId))
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
                        }
                    }

                    // FreeBarber - sadece kendimiz değilsek ekle
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                    {
                        var freeBarber = freeBarbers.FirstOrDefault(fb => fb.FreeBarberUserId == appt.FreeBarberUserId.Value);
                        if (freeBarber != null)
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (threadEntity.FreeBarberUserId == freeBarber.FreeBarberUserId ||
                                (threadEntity.FreeBarberUserId.HasValue && threadEntity.FreeBarberUserId.Value == freeBarber.FreeBarberUserId))
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
                    }

                    // Mevcut kullanıcının profil resmini ekle
                    threadDto.CurrentUserImageUrl = currentUserImageUrl;

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

                    // REVIZE: Favori aktif mi kontrol et - en az bir tarafın favori olması yeterli
                    // Artık User ID bazlı kontrol yapılır (Store bazlı thread'ler için de)
                    bool isFavoriteActive = false;
                    var fromUserId = threadEntity.FavoriteFromUserId!.Value;
                    var toUserId = threadEntity.FavoriteToUserId!.Value;

                    // 1. User ID bazlı favori kontrolü (her iki yönde)
                    var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                    if (favorite1 != null && favorite1.IsActive)
                    {
                        isFavoriteActive = true;
                    }

                    if (!isFavoriteActive)
                    {
                        var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favorite2 != null && favorite2.IsActive)
                        {
                            isFavoriteActive = true;
                        }
                    }

                    // Performance: Store bazlı favoriler için kontrol - batch olarak çek
                    if (!isFavoriteActive)
                    {
                        var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
                        if (stores.Any())
                        {
                            var storeIds = stores.Select(s => s.Id).ToList();
                            var favoriteStores = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == fromUserId &&
                                storeIds.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            if (favoriteStores.Any())
                            {
                                isFavoriteActive = true;
                            }
                        }
                    }

                    // Performance: toUserId'nin fromUserId'nin store'larından birini favoriye eklemiş olabilir - batch olarak çek
                    if (!isFavoriteActive)
                    {
                        var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
                        if (stores.Any())
                        {
                            var storeIds = stores.Select(s => s.Id).ToList();
                            var favoriteStores = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == toUserId &&
                                storeIds.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            if (favoriteStores.Any())
                            {
                                isFavoriteActive = true;
                            }
                        }
                    }

                    // En az bir tarafın favori olması yeterli (aktif olmalı)
                    if (!isFavoriteActive) continue; // Hiçbiri aktif değilse thread'i atla

                    string displayName = "";
                    string? imageUrl = null;
                    BarberType? barberType = null;
                    Guid participantUserId = Guid.Empty;
                    UserType participantUserType;

                    // REVIZE: Thread bilgisinde dükkan/serbest berber panel bilgileri gösterilsin
                    // Store bazlı favoriler için: En az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                    // FreeBarber için: Panel bilgileri gösterilsin
                    var otherUserId = threadEntity.FavoriteFromUserId == userId
                        ? threadEntity.FavoriteToUserId!.Value
                        : threadEntity.FavoriteFromUserId!.Value;

                    // Diğer kullanıcının bilgilerini çek
                    var otherUser = await userDal.Get(u => u.Id == otherUserId);
                    if (otherUser == null) continue;

                    participantUserId = otherUser.Id;
                    participantUserType = otherUser.UserType;

                    if (otherUser.UserType == UserType.Customer)
                    {
                        displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                        if (otherUser.ImageId.HasValue)
                        {
                            var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                            imageUrl = img?.ImageUrl;
                        }
                        barberType = null;
                    }
                    else if (otherUser.UserType == UserType.BarberStore)
                    {
                        // REVIZE: Store sahibi için - en az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                        // userId'nin otherUserId'nin store'larından birini favoriye eklemiş olabilir
                        var favoriteStores = await favoriteDal.GetAll(x => x.FavoritedFromId == userId && x.IsActive);
                        var storeIds = favoriteStores.Select(f => f.FavoritedToId).ToList();
                        var stores = await barberStoreDal.GetAll(x => storeIds.Contains(x.Id) && x.BarberStoreOwnerId == otherUserId);

                        // En az bir dükkan favoriye alınmışsa, ilk dükkanın bilgilerini göster
                        var store = stores.FirstOrDefault();
                        if (store != null)
                        {
                            displayName = store.StoreName;
                            barberType = store.Type;
                            var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                        }
                        else
                        {
                            // Favori store yoksa, ilk store'u göster (fallback)
                            var firstStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                            if (firstStore != null)
                            {
                                displayName = firstStore.StoreName;
                                barberType = firstStore.Type;
                                var img = await imageDal.GetLatestImageAsync(firstStore.Id, ImageOwnerType.Store);
                                imageUrl = img?.ImageUrl;
                            }
                        }
                    }
                    else if (otherUser.UserType == UserType.FreeBarber)
                    {
                        // REVIZE: FreeBarber için panel bilgileri gösterilsin
                        var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                        if (freeBarber != null)
                        {
                            displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                            barberType = freeBarber.Type;
                            var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    else
                    {
                        continue; // Beklenmeyen durum
                    }

                    threadDto.Title = displayName;

                    // Participant bilgilerini ayarla
                    // ÖNEMLİ: participantUserId kendi userId'miz değil, diğer kullanıcının userId'si olmalı
                    // participantUserType set edilmemişse veya participantUserId geçersizse atla
                    if (participantUserId == userId || participantUserId == Guid.Empty || string.IsNullOrEmpty(displayName))
                    {
                        // Kendi bilgilerimiz participant olarak eklenmiş veya geçersiz - bu yanlış, atla
                        continue;
                    }

                    threadDto.Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = participantUserId,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = participantUserType,
                            BarberType = barberType
                        }
                    };

                    // Mevcut kullanıcının profil resmini ekle
                    threadDto.CurrentUserImageUrl = currentUserImageUrl;

                    activeFavoriteThreads.Add(threadDto);
                }

                result.AddRange(activeFavoriteThreads);
            }

            // Son mesaj zamanına göre sırala
            result = result.OrderByDescending(t => t.LastMessageAt ?? DateTime.MinValue).ToList();

            // LastMessagePreview'ları decrypt et
            foreach (var t in result)
                t.LastMessagePreview = messageEncryption.Decrypt(t.LastMessagePreview);

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

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        // Read-only query - no transaction needed
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

            // DB'den okunan mesajları decrypt et
            foreach (var m in msgs)
                m.Text = messageEncryption.Decrypt(m.Text);

            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
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

            // REVIZE: Favori aktif mi kontrolü - en az bir tarafın favori olması yeterli
            // Artık User ID bazlı kontrol yapılır (Store bazlı thread'ler için de)
            var fromUserId = thread.FavoriteFromUserId!.Value;
            var toUserId = thread.FavoriteToUserId!.Value;

            bool isFavoriteActive = false;

            // 1. User ID bazlı favori kontrolü (her iki yönde)
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }

            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
            }

            // 2. Store bazlı favoriler için kontrol (fromUserId'nin toUserId'nin store'larından birini favoriye eklemiş olabilir)
            if (!isFavoriteActive)
            {
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
                foreach (var store in stores)
                {
                    var favoriteStore = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == store.Id && x.IsActive);
                    if (favoriteStore != null)
                    {
                        isFavoriteActive = true;
                        break;
                    }
                }
            }

            // 3. toUserId'nin fromUserId'nin store'larından birini favoriye eklemiş olabilir
            if (!isFavoriteActive)
            {
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
                foreach (var store in stores)
                {
                    var favoriteStore = await favoriteDal.Get(x => x.FavoritedFromId == toUserId && x.FavoritedToId == store.Id && x.IsActive);
                    if (favoriteStore != null)
                    {
                        isFavoriteActive = true;
                        break;
                    }
                }
            }

            if (!isFavoriteActive)
                return new ErrorDataResult<ChatMessageDto>(Messages.FavoriteNotActive);

            // Mesaj metnini şifrele (DB'ye kaydedilecek)
            var encryptedText = messageEncryption.Encrypt(text);
            var previewText = text.Length > 60 ? text[..60] : text;
            var encryptedPreview = messageEncryption.Encrypt(previewText);

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AppointmentId = null, // Favori thread'de AppointmentId null
                SenderUserId = senderUserId,
                Text = encryptedText,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            };
            await messageDal.Add(msg);

            thread.LastMessageAt = msg.CreatedAt;
            thread.LastMessagePreview = encryptedPreview;
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

            // SignalR DTO'su plaintext kullanır (TLS ile korunur)
            var dto = new ChatMessageDto
            {
                ThreadId = thread.Id,
                AppointmentId = null,
                MessageId = msg.Id,
                SenderUserId = senderUserId,
                Text = text,
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
            }

            // Thread güncellemesini her iki kullanıcıya da push et (LastMessagePreview, LastMessageAt, UnreadCount değişti)
            // EnsureFavoriteThreadAsync mantığını kullanarak thread detaylarını oluştur ve push et
            await PushFavoriteThreadUpdatedAsync(fromUserId, toUserId, thread.Id);

            // Badge update: sender dışındaki katılımcı için badge count'u güncelle
            if (otherUserId.HasValue && otherUserId.Value != senderUserId)
            {
                await badgeService.NotifyBadgeChangeAsync(otherUserId.Value, BadgeChangeReason.MessageReceived);
            }

            // Fire-and-forget moderation
            var msgIdForMod = msg.Id;
            var threadIdForMod = thread.Id;
            var recipientsForMod = favoriteRecipients.Distinct().ToList();
            _ = Task.Run(() => ModerateAndRemoveIfFlaggedAsync(msgIdForMod, text, threadIdForMod, recipientsForMod));

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
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

            // Read receipt'leri işle ve çift tik bildirimi gönder
            await ProcessReadReceiptsAsync(thread, userId);

            // Badge count'u güncelle
            var userThreads = await threadDal.GetAll(t =>
                t.CustomerUserId == userId || t.StoreOwnerUserId == userId || t.FreeBarberUserId == userId);
            var chatUnreadCount = userThreads.Sum(t =>
                t.CustomerUserId == userId ? t.CustomerUnreadCount :
                t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
                t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);
            await realtime.PushBadgeUpdateAsync(userId, chatUnreadCount: chatUnreadCount);

            return new SuccessDataResult<bool>(true);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        // Read-only query - no transaction needed
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
                // Store bazlı thread'ler için: StoreId ile favori kontrolü yapılır
                // Diğer thread'ler için: User ID bazlı favori kontrolü yapılır
                var fromUserId = thread.FavoriteFromUserId.Value;
                var toUserId = thread.FavoriteToUserId.Value;

                bool isFavoriteActive = false;

                // Store bazlı thread'ler için StoreId ile favori kontrolü
                if (thread.StoreId.HasValue)
                {
                    // Store bazlı favori kontrolü: StoreId ile kontrol yap
                    // fromUserId -> StoreId yönünde
                    var favorite1Store = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == thread.StoreId.Value && x.IsActive);
                    if (favorite1Store != null)
                    {
                        isFavoriteActive = true;
                    }
                    // Ek kural: Karşı taraf userId bazlı favori yaptıysa thread görünür kalmalı
                    if (!isFavoriteActive)
                    {
                        var favUser1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                        if (favUser1 != null && favUser1.IsActive) isFavoriteActive = true;
                    }
                    if (!isFavoriteActive)
                    {
                        var favUser2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favUser2 != null && favUser2.IsActive) isFavoriteActive = true;
                    }
                }
                else
                {
                    // Store bazlı değil, User ID bazlı favori kontrolü
                    // 1. fromUserId -> toUserId yönünde
                    var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                    if (favorite1 != null && favorite1.IsActive)
                    {
                        isFavoriteActive = true;
                    }

                    // 2. toUserId -> fromUserId yönünde
                    if (!isFavoriteActive)
                    {
                        var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favorite2 != null && favorite2.IsActive)
                        {
                            isFavoriteActive = true;
                        }
                    }
                }

                if (!isFavoriteActive)
                    return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.FavoriteNotActiveForMessages);
            }

            if (!isParticipant) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.NotAParticipant);

            // Tüm katılımcı ID'lerini toparla (isFullyRead hesabı için)
            var allParticipantIds = new List<Guid>();
            if (thread.CustomerUserId.HasValue) allParticipantIds.Add(thread.CustomerUserId.Value);
            if (thread.StoreOwnerUserId.HasValue) allParticipantIds.Add(thread.StoreOwnerUserId.Value);
            if (thread.FreeBarberUserId.HasValue) allParticipantIds.Add(thread.FreeBarberUserId.Value);

            var msgs = await messageDal.GetMessagesByThreadIdWithReadStatusAsync(threadId, beforeUtc, allParticipantIds);

            // DB'den okunan mesajları decrypt et
            foreach (var m in msgs)
                m.Text = messageEncryption.Decrypt(m.Text);

            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ExceptionHandlingAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> EnsureFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId = null)
        {
            // REVIZE: StoreId parametresi artık kullanılmıyor - User ID bazlı tek thread olmalı
            // Mevcut thread'i kontrol et (her iki yönde, StoreId null ile)
            var existingThread = await threadDal.GetFavoriteThreadAsync(fromUserId, toUserId, storeId: null);

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
                    return new ErrorDataResult<Guid>(Messages.UserNotFound);

                thread = new ChatThread
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = null,
                    FavoriteFromUserId = fromUserId,
                    FavoriteToUserId = toUserId,
                    StoreId = null, // REVIZE: StoreId null - User ID bazlı tek thread (birden fazla dükkan favorilense bile)
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

            // REVIZE: Aktif favori kontrolü - en az bir tarafın favori aktif olmalı
            // Artık User ID bazlı kontrol yapılır (Store bazlı thread'ler için de)
            // Store bazlı favoriler için: StoreId -> Store Owner User ID kontrolü yapılır
            // FreeBarber/Customer için: User ID -> User ID kontrolü yapılır
            // ÖNEMLİ: Transaction commit edilmeden önce bu metod çağrılıyor olabilir (FavoriteManager'dan),
            // bu yüzden favori henüz DB'de görünmeyebilir.

            bool isFavoriteActive = false;

            // REVIZE: User ID bazlı favori kontrolü (her iki yönde)
            // 1. fromUserId -> toUserId yönünde (User ID bazlı veya Store bazlı)
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }

            // 2. toUserId -> fromUserId yönünde (User ID bazlı veya Store bazlı)
            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
            }

            // 3. Store bazlı favoriler için kontrol (StoreId -> Store Owner User ID)
            // fromUserId'nin toUserId'nin store'larından birini favoriye eklemiş olabilir
            if (!isFavoriteActive)
            {
                // toUserId'nin store'larını bul
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
                foreach (var store in stores)
                {
                    var favoriteStore = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == store.Id && x.IsActive);
                    if (favoriteStore != null)
                    {
                        isFavoriteActive = true;
                        break;
                    }
                }
            }

            // 4. toUserId'nin fromUserId'nin store'larından birini favoriye eklemiş olabilir
            if (!isFavoriteActive)
            {
                // fromUserId'nin store'larını bul
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
                foreach (var store in stores)
                {
                    var favoriteStore = await favoriteDal.Get(x => x.FavoritedFromId == toUserId && x.FavoritedToId == store.Id && x.IsActive);
                    if (favoriteStore != null)
                    {
                        isFavoriteActive = true;
                        break;
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
                // ExceptionHandlingAspect method seviyesinde exception'ları handle eder
                // Burada try-catch'e gerek yok, aspect otomatik handle edecek
                string displayName = "";
                string? imageUrl = null;
                BarberType? barberType = null;
                Guid participantUserId = Guid.Empty;
                UserType participantUserType;

                // REVIZE: Thread bilgisinde dükkan/serbest berber panel bilgileri gösterilsin
                // Store bazlı favoriler için: En az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                // FreeBarber için: Panel bilgileri gösterilsin
                var otherUserId = thread.FavoriteFromUserId == recipientUserId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;

                var otherUser = await userDal.Get(u => u.Id == otherUserId);
                if (otherUser == null) continue;

                participantUserId = otherUser.Id;

                if (otherUser.UserType == UserType.Customer)
                {
                    displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                    if (otherUser.ImageId.HasValue)
                    {
                        var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                        imageUrl = img?.ImageUrl;
                    }
                    barberType = null;
                    participantUserType = UserType.Customer;
                }
                else if (otherUser.UserType == UserType.BarberStore)
                {
                    // REVIZE: Store sahibi için - en az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                    // recipientUserId'nin otherUserId'nin store'larından birini favoriye eklemiş olabilir
                    var favoriteStores = await favoriteDal.GetAll(x => x.FavoritedFromId == recipientUserId && x.IsActive);
                    var storeIds = favoriteStores.Select(f => f.FavoritedToId).ToList();
                    var stores = await barberStoreDal.GetAll(x => storeIds.Contains(x.Id) && x.BarberStoreOwnerId == otherUserId);

                    // En az bir dükkan favoriye alınmışsa, ilk dükkanın bilgilerini göster
                    var store = stores.FirstOrDefault();
                    if (store != null)
                    {
                        displayName = store.StoreName;
                        barberType = store.Type;
                        var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                        imageUrl = img?.ImageUrl;
                    }
                    else
                    {
                        // Favori store yoksa, ilk store'u göster (fallback)
                        var firstStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                        if (firstStore != null)
                        {
                            displayName = firstStore.StoreName;
                            barberType = firstStore.Type;
                            var img = await imageDal.GetLatestImageAsync(firstStore.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                    participantUserType = UserType.BarberStore;
                }
                else if (otherUser.UserType == UserType.FreeBarber)
                {
                    // REVIZE: FreeBarber için panel bilgileri gösterilsin
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                    if (freeBarber != null)
                    {
                        displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                        barberType = freeBarber.Type;
                        var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                        imageUrl = img?.ImageUrl;
                    }
                    participantUserType = UserType.FreeBarber;
                }
                else
                {
                    continue; // Beklenmeyen durum
                }

                // UnreadCount'u thread entity'den al
                int unreadCount = 0;
                if (thread.CustomerUserId == recipientUserId)
                    unreadCount = thread.CustomerUnreadCount;
                else if (thread.StoreOwnerUserId == recipientUserId)
                    unreadCount = thread.StoreUnreadCount;
                else if (thread.FreeBarberUserId == recipientUserId)
                    unreadCount = thread.FreeBarberUnreadCount;

                // Mevcut kullanıcının (recipientUserId) profil resmini al
                string? currentUserImageUrlForRecipient = null;
                var recipientUser = await userDal.Get(u => u.Id == recipientUserId);
                if (recipientUser != null)
                {
                    if (recipientUser.UserType == UserType.Customer)
                    {
                        var userImg = await imageDal.GetLatestImageAsync(recipientUser.Id, ImageOwnerType.User);
                        currentUserImageUrlForRecipient = userImg?.ImageUrl;
                    }
                    else if (recipientUser.UserType == UserType.BarberStore)
                    {
                        var userStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == recipientUserId);
                        if (userStore != null)
                        {
                            var storeImg = await imageDal.GetLatestImageAsync(userStore.Id, ImageOwnerType.Store);
                            currentUserImageUrlForRecipient = storeImg?.ImageUrl;
                        }
                    }
                    else if (recipientUser.UserType == UserType.FreeBarber)
                    {
                        var userFreeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == recipientUserId);
                        if (userFreeBarber != null)
                        {
                            var freeBarberImg = await imageDal.GetLatestImageAsync(userFreeBarber.Id, ImageOwnerType.FreeBarber);
                            currentUserImageUrlForRecipient = freeBarberImg?.ImageUrl;
                        }
                    }
                }

                var threadDto = new ChatThreadListItemDto
                {
                    ThreadId = thread.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = true,
                    Title = displayName,
                    LastMessagePreview = messageEncryption.Decrypt(thread.LastMessagePreview),
                    LastMessageAt = thread.LastMessageAt,
                    UnreadCount = unreadCount,
                    CurrentUserImageUrl = currentUserImageUrlForRecipient,
                    Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = participantUserId,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = participantUserType,
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

            return new SuccessDataResult<Guid>(thread.Id);
        }

        private async Task PushAppointmentThreadAsync(Guid appointmentId, PushThreadAction action)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt == null) return;

            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread == null) return;

            if (appt.Status != AppointmentStatus.Pending && appt.Status != AppointmentStatus.Approved)
                return;

            var participants = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var users = await userDal.GetAll(u => participants.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id);

            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
            }

            var storeDict = new Dictionary<Guid, BarberStore>();
            if (store != null)
            {
                storeDict[store.BarberStoreOwnerId] = store;
            }

            var freeBarberDict = new Dictionary<Guid, FreeBarber>();
            if (appt.FreeBarberUserId.HasValue)
            {
                var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                if (freeBarber != null)
                {
                    freeBarberDict[freeBarber.FreeBarberUserId] = freeBarber;
                }
            }

            var userImageDict = await GetImagesForOwnersAsync(users.Select(u => u.Id).ToList(), ImageOwnerType.User);
            var storeImageDict = await GetImagesForOwnersAsync(storeDict.Values.Select(s => s.Id).ToList(), ImageOwnerType.Store);
            var freeBarberImageDict = await GetImagesForOwnersAsync(freeBarberDict.Values.Select(f => f.Id).ToList(), ImageOwnerType.FreeBarber);

            foreach (var userId in participants)
            {
                // ExceptionHandlingAspect method seviyesinde exception'ları handle eder
                // Burada try-catch'e gerek yok, aspect otomatik handle edecek
                var title = BuildThreadTitleForUser(userId, appt, store?.StoreName);

                    // Participants listesini oluştur - ÖNEMLİ: Sadece kendi userId'miz hariç diğer participant'ları göster
                    var participantsList = new List<ChatThreadParticipantDto>();

                    // Customer participant - sadece kendimiz değilsek ekle
                    if (appt.CustomerUserId.HasValue && appt.CustomerUserId.Value != userId)
                    {
                        if (userDict.TryGetValue(appt.CustomerUserId.Value, out var customerUser))
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (thread.CustomerUserId == appt.CustomerUserId.Value)
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
                    }

                    // Store participant - sadece kendimiz değilsek ekle
                    if (appt.BarberStoreUserId.HasValue && store != null)
                    {
                        // Store sahibi userId'si ile karşılaştır - ÖNEMLİ: Kendi store bilgilerimizi değil, karşı tarafın bilgilerini göster
                        if (store.BarberStoreOwnerId != userId)
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (thread.StoreOwnerUserId == store.BarberStoreOwnerId ||
                                (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId.Value == store.BarberStoreOwnerId))
                            {
                                storeImageDict.TryGetValue(store.Id, out var storeImageUrl);
                                participantsList.Add(new ChatThreadParticipantDto
                                {
                                    UserId = store.BarberStoreOwnerId,
                                    DisplayName = store.StoreName,
                                    ImageUrl = storeImageUrl,
                                    UserType = UserType.BarberStore,
                                    BarberType = store.Type
                                });
                            }
                        }
                    }

                    // FreeBarber participant - sadece kendimiz değilsek ekle
                    if (appt.FreeBarberUserId.HasValue && appt.FreeBarberUserId.Value != userId)
                    {
                        if (freeBarberDict.TryGetValue(appt.FreeBarberUserId.Value, out var freeBarberEntity))
                        {
                            // Ekstra güvenlik: Thread entity'deki mapping ile de kontrol et
                            if (thread.FreeBarberUserId == freeBarberEntity.FreeBarberUserId ||
                                (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId.Value == freeBarberEntity.FreeBarberUserId))
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
                    }

                    // UnreadCount'u thread entity'den al
                    int unreadCount = 0;
                    if (thread.CustomerUserId == userId)
                        unreadCount = thread.CustomerUnreadCount;
                    else if (thread.StoreOwnerUserId == userId)
                        unreadCount = thread.StoreUnreadCount;
                    else if (thread.FreeBarberUserId == userId)
                        unreadCount = thread.FreeBarberUnreadCount;

                    // Mevcut kullanıcının (userId) profil resmini al
                    string? currentUserImageUrlForThisUser = null;
                    if (userDict.TryGetValue(userId, out var currentUserEntity))
                    {
                        if (currentUserEntity.UserType == UserType.Customer)
                        {
                            userImageDict.TryGetValue(currentUserEntity.Id, out currentUserImageUrlForThisUser);
                        }
                        else if (currentUserEntity.UserType == UserType.BarberStore)
                        {
                            if (storeDict.TryGetValue(userId, out var userStore))
                            {
                                storeImageDict.TryGetValue(userStore.Id, out currentUserImageUrlForThisUser);
                            }
                        }
                        else if (currentUserEntity.UserType == UserType.FreeBarber)
                        {
                            if (freeBarberDict.TryGetValue(userId, out var userFreeBarber))
                            {
                                freeBarberImageDict.TryGetValue(userFreeBarber.Id, out currentUserImageUrlForThisUser);
                            }
                        }
                    }

                    var threadDto = new ChatThreadListItemDto
                    {
                        ThreadId = thread.Id,
                        AppointmentId = appt.Id,
                        Status = appt.Status,
                        IsFavoriteThread = false,
                        Title = title,
                        LastMessagePreview = messageEncryption.Decrypt(thread.LastMessagePreview),
                        LastMessageAt = thread.LastMessageAt,
                        UnreadCount = unreadCount,
                        CurrentUserImageUrl = currentUserImageUrlForThisUser,
                        Participants = participantsList
                    };

                    if (action == PushThreadAction.Created)
                        await realtime.PushChatThreadCreatedAsync(userId, threadDto);
                    else
                        await realtime.PushChatThreadUpdatedAsync(userId, threadDto);
            }
        }

        public async Task PushAppointmentThreadCreatedAsync(Guid appointmentId)
        {
            await PushAppointmentThreadAsync(appointmentId, PushThreadAction.Created);
        }

        public async Task PushAppointmentThreadUpdatedAsync(Guid appointmentId)
        {
            await PushAppointmentThreadAsync(appointmentId, PushThreadAction.Updated);
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

            // Performance: Her iki kullanıcı için de thread detaylarını al ve SignalR ile gönder
            var recipients = new[] { fromUserId, toUserId }.Distinct().ToList();

            // Performance: Parallel processing için Task.WhenAll kullanılabilir ama şimdilik sequential
            foreach (var recipientUserId in recipients)
            {
                // ExceptionHandlingAspect method seviyesinde exception'ları handle eder
                // Burada try-catch'e gerek yok, aspect otomatik handle edecek
                string displayName = "";
                string? imageUrl = null;
                BarberType? barberType = null;
                Guid participantUserId = Guid.Empty;
                UserType participantUserType;

                // REVIZE: Thread bilgisinde dükkan/serbest berber panel bilgileri gösterilsin
                // Store bazlı favoriler için: En az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                // FreeBarber için: Panel bilgileri gösterilsin
                var otherUserId = thread.FavoriteFromUserId == recipientUserId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;

                var otherUser = await userDal.Get(u => u.Id == otherUserId);
                if (otherUser == null) continue;

                participantUserId = otherUser.Id;
                participantUserType = otherUser.UserType; // ÖNEMLİ: participantUserType'ı burada set et

                if (otherUser.UserType == UserType.Customer)
                {
                    displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                    if (otherUser.ImageId.HasValue)
                    {
                        var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                        imageUrl = img?.ImageUrl;
                    }
                    barberType = null;
                }
                else if (otherUser.UserType == UserType.BarberStore)
                {
                    // REVIZE: Store sahibi için - en az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                    // recipientUserId'nin otherUserId'nin store'larından birini favoriye eklemiş olabilir
                    var favoriteStores = await favoriteDal.GetAll(x => x.FavoritedFromId == recipientUserId && x.IsActive);
                    var storeIds = favoriteStores.Select(f => f.FavoritedToId).ToList();
                    var stores = await barberStoreDal.GetAll(x => storeIds.Contains(x.Id) && x.BarberStoreOwnerId == otherUserId);

                    // En az bir dükkan favoriye alınmışsa, ilk dükkanın bilgilerini göster
                    var store = stores.FirstOrDefault();
                    if (store != null)
                    {
                        displayName = store.StoreName;
                        barberType = store.Type;
                        var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                        imageUrl = img?.ImageUrl;
                    }
                    else
                    {
                        // Favori store yoksa, ilk store'u göster (fallback)
                        var firstStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                        if (firstStore != null)
                        {
                            displayName = firstStore.StoreName;
                            barberType = firstStore.Type;
                            var img = await imageDal.GetLatestImageAsync(firstStore.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                        }
                    }
                }
                else if (otherUser.UserType == UserType.FreeBarber)
                {
                    // REVIZE: FreeBarber için panel bilgileri gösterilsin
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                    if (freeBarber != null)
                    {
                        displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                        barberType = freeBarber.Type;
                        var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                        imageUrl = img?.ImageUrl;
                    }
                }
                else
                {
                    continue; // Beklenmeyen durum
                }

                // Participant bilgilerini kontrol et - geçerli değilse atla
                if (participantUserId == recipientUserId || participantUserId == Guid.Empty || string.IsNullOrEmpty(displayName))
                {
                    continue; // Geçersiz participant bilgisi
                }

                // UnreadCount'u thread entity'den al
                int unreadCount = 0;
                if (thread.CustomerUserId == recipientUserId)
                    unreadCount = thread.CustomerUnreadCount;
                else if (thread.StoreOwnerUserId == recipientUserId)
                    unreadCount = thread.StoreUnreadCount;
                else if (thread.FreeBarberUserId == recipientUserId)
                    unreadCount = thread.FreeBarberUnreadCount;

                // Mevcut kullanıcının (recipientUserId) profil resmini al
                string? currentUserImageUrlForRecipient = null;
                var recipientUser = await userDal.Get(u => u.Id == recipientUserId);
                if (recipientUser != null)
                {
                    if (recipientUser.UserType == UserType.Customer)
                    {
                        var userImg = await imageDal.GetLatestImageAsync(recipientUser.Id, ImageOwnerType.User);
                        currentUserImageUrlForRecipient = userImg?.ImageUrl;
                    }
                    else if (recipientUser.UserType == UserType.BarberStore)
                    {
                        var userStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == recipientUserId);
                        if (userStore != null)
                        {
                            var storeImg = await imageDal.GetLatestImageAsync(userStore.Id, ImageOwnerType.Store);
                            currentUserImageUrlForRecipient = storeImg?.ImageUrl;
                        }
                    }
                    else if (recipientUser.UserType == UserType.FreeBarber)
                    {
                        var userFreeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == recipientUserId);
                        if (userFreeBarber != null)
                        {
                            var freeBarberImg = await imageDal.GetLatestImageAsync(userFreeBarber.Id, ImageOwnerType.FreeBarber);
                            currentUserImageUrlForRecipient = freeBarberImg?.ImageUrl;
                        }
                    }
                }

                var threadDto = new ChatThreadListItemDto
                {
                    ThreadId = thread.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = true,
                    Title = displayName,
                    LastMessagePreview = messageEncryption.Decrypt(thread.LastMessagePreview),
                    LastMessageAt = thread.LastMessageAt,
                    UnreadCount = unreadCount,
                    CurrentUserImageUrl = currentUserImageUrlForRecipient,
                    Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = participantUserId,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = participantUserType,
                            BarberType = barberType
                        }
                    }
                };

                // ThreadUpdated gönder
                await realtime.PushChatThreadUpdatedAsync(recipientUserId, threadDto);
            }
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
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
                    userName = store?.StoreName ?? Messages.BarberDefaultName;
                }
                else if (user.UserType == UserType.FreeBarber)
                {
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
                    userName = freeBarber != null ? $"{freeBarber.FirstName} {freeBarber.LastName}" : Messages.FreeBarberDefaultName;
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
                await realtime.PushChatTypingAsync(participantId, threadId, userId, userName ?? Messages.UserDefaultName, isTyping);
            }

            return new SuccessDataResult<bool>(true);
        }

        /// <summary>
        /// Generic helper method to fetch images for multiple owners based on owner type.
        /// Consolidates previous GetImagesForUsersAsync, GetImagesForStoresAsync, and GetImagesForFreeBarberAsync methods.
        /// Returns a dictionary mapping owner ID to their most recent image URL.
        /// </summary>
        /// <param name="ownerIds">List of owner IDs to fetch images for</param>
        /// <param name="ownerType">Type of owner (User, Store, FreeBarber)</param>
        /// <returns>Dictionary mapping owner ID to latest image URL (null if no image found)</returns>
        private async Task<Dictionary<Guid, string?>> GetImagesForOwnersAsync(
            List<Guid> ownerIds,
            ImageOwnerType ownerType)
        {
            if (ownerIds == null || ownerIds.Count == 0)
                return new Dictionary<Guid, string?>();

            var images = await imageDal.GetAll(img =>
                ownerIds.Contains(img.ImageOwnerId) &&
                img.OwnerType == ownerType);

            return images
                .GroupBy(img => img.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.ImageUrl);
        }

        /// <summary>
        /// Thread'deki mesajları kullanıcı için okundu işaretler ve tam okunan mesajları
        /// diğer katılımcılara SignalR ile bildirir (çift tik).
        /// </summary>
        private async Task ProcessReadReceiptsAsync(Entities.Concrete.Entities.ChatThread thread, Guid userId)
        {
            try
            {
                var newlyReadIds = await receiptDal.MarkThreadMessagesReadAsync(thread.Id, userId);
                if (newlyReadIds.Count == 0) return;

                var allParticipantIds = new List<Guid>();
                if (thread.CustomerUserId.HasValue) allParticipantIds.Add(thread.CustomerUserId.Value);
                if (thread.StoreOwnerUserId.HasValue) allParticipantIds.Add(thread.StoreOwnerUserId.Value);
                if (thread.FreeBarberUserId.HasValue) allParticipantIds.Add(thread.FreeBarberUserId.Value);

                var fullyReadIds = await receiptDal.GetFullyReadMessageIdsAsync(thread.Id, allParticipantIds);
                var newlyFullyRead = newlyReadIds.Where(id => fullyReadIds.Contains(id)).ToList();

                if (newlyFullyRead.Count == 0) return;

                // Diğer katılımcılara (gönderenler) bildir - çift tik görebilsinler
                var otherParticipants = allParticipantIds.Where(id => id != userId).ToList();
                foreach (var participantId in otherParticipants)
                {
                    await realtime.PushChatMessagesReadAsync(participantId, thread.Id, userId, newlyFullyRead);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ChatManager] ProcessReadReceiptsAsync failed for thread {ThreadId}, user {UserId}", thread.Id, userId);
            }
        }

    }
}
