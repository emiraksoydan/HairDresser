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

        public async Task<IDataResult<bool>> MarkThreadReadAsync(Guid userId, Guid appointmentId)
        {
            // Performance: Use Get instead of GetAll().FirstOrDefault()
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
            // sadece Pending + Approved
            var allowed = new[] { AppointmentStatus.Pending, AppointmentStatus.Approved };

            var threads = await threadDal.GetThreadsForUserAsync(userId, allowed);

            // PERFORMANCE FIX: N+1 Query problemi çözüldü - Batch queries
            if (threads.Count == 0)
                return new SuccessDataResult<List<ChatThreadListItemDto>>(threads);

            // Tüm appointment ID'leri topla
            var appointmentIds = threads.Select(t => t.AppointmentId).ToList();
            
            // Tek sorguda tüm appointment'ları çek
            var appointments = await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id));
            var apptDict = appointments.ToDictionary(a => a.Id);

            // Tüm store owner ID'leri topla
            var storeOwnerIds = appointments
                .Where(a => a.BarberStoreUserId.HasValue)
                .Select(a => a.BarberStoreUserId!.Value)
                .Distinct()
                .ToList();

            // Tek sorguda tüm store'ları çek
            var stores = storeOwnerIds.Count > 0
                ? await barberStoreDal.GetAll(x => storeOwnerIds.Contains(x.BarberStoreOwnerId))
                : new List<BarberStore>();
            var storeDict = stores.ToDictionary(s => s.BarberStoreOwnerId);

            // Set Title for each thread (batch queries ile optimize edildi)
            foreach (var thread in threads)
            {
                if (!apptDict.TryGetValue(thread.AppointmentId, out var appt)) continue;

                storeDict.TryGetValue(appt.BarberStoreUserId ?? Guid.Empty, out var store);
                thread.Title = BuildThreadTitleForUser(userId, appt, store?.StoreName);
            }

            return new SuccessDataResult<List<ChatThreadListItemDto>>(threads);
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
    }
}
