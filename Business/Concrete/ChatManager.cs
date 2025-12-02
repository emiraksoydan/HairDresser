using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
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
using System.Text;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class ChatManager(
             DatabaseContext db,
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
            if (text.Length == 0) return new ErrorDataResult<ChatMessageDto>("Empty message");

            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<ChatMessageDto>("Appointment not found");

            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<ChatMessageDto>("Chat is only allowed for Pending/Approved appointments");

            // yetki: sender katılımcı mı?
            var isParticipant =
                appt.CustomerUserId == senderUserId ||
                appt.FreeBarberUserId == senderUserId ||
                appt.BarberStoreUserId == senderUserId;

            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>("Not a participant");

            var thread = (await threadDal.GetAll(t => t.AppointmentId == appointmentId)).FirstOrDefault();
            var barberStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId);
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
            var thread = (await threadDal.GetAll(t => t.AppointmentId == appointmentId)).FirstOrDefault();
            if (thread is null) return new ErrorDataResult<bool>(false, "Sohbet bulunamadı");

            if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
            else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
            else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
            else return new ErrorDataResult<bool>(false, "Katılımcı bulunamadı");

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

            var threads = await db.ChatThreads.AsNoTracking()
                .Join(db.Appointments.AsNoTracking(),
                      t => t.AppointmentId,
                      a => a.Id,
                      (t, a) => new { t, a })
                .Where(x =>
                    allowed.Contains(x.a.Status) &&
                    (x.t.CustomerUserId == userId || x.t.StoreOwnerUserId == userId || x.t.FreeBarberUserId == userId))
                .OrderByDescending(x => x.t.LastMessageAt ?? x.t.CreatedAt)
                .Select(x => new ChatThreadListItemDto
                {
                    AppointmentId = x.a.Id,
                    Status = x.a.Status,
                    Title = x.t.StoreOwnerUserId == userId
                        ? (x.t.CustomerUserId.HasValue ? "Müşteri" : "Serbest Berber")
                        : "Berber Dükkanı",

                    LastMessagePreview = x.t.LastMessagePreview,
                    LastMessageAt = x.t.LastMessageAt,
                    UnreadCount = x.t.CustomerUserId == userId ? x.t.CustomerUnreadCount :
                                  x.t.StoreOwnerUserId == userId ? x.t.StoreUnreadCount :
                                  x.t.FreeBarberUserId == userId ? x.t.FreeBarberUnreadCount : 0
                })
                .ToListAsync();

            return new SuccessDataResult<List<ChatThreadListItemDto>>(threads);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]

        public async Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesAsync(
            Guid userId, Guid appointmentId, DateTime? beforeUtc)
        {

            var appt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<List<ChatMessageItemDto>>("Appointment not found");

            // sadece Pending + Approved sohbet gösterimi
            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<List<ChatMessageItemDto>>("Chat is only available for Pending/Approved");

            // katılımcı mı?
            var thread = await db.ChatThreads.AsNoTracking().FirstOrDefaultAsync(t => t.AppointmentId == appointmentId);
            if (thread is null) return new SuccessDataResult<List<ChatMessageItemDto>>();

            var isParticipant =
                thread.CustomerUserId == userId || thread.StoreOwnerUserId == userId || thread.FreeBarberUserId == userId;

            if (!isParticipant) return new ErrorDataResult<List<ChatMessageItemDto>>("Not a participant");

            var query = db.ChatMessages.AsNoTracking()
                .Where(m => m.AppointmentId == appointmentId);

            if (beforeUtc.HasValue)
                query = query.Where(m => m.CreatedAt < beforeUtc.Value);

            var msgs = await query
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new ChatMessageItemDto
                {
                    MessageId = m.Id,
                    SenderUserId = m.SenderUserId,
                    Text = m.Text,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            msgs.Reverse();

            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }
    }
}
