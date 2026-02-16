using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfChatMessageDal : EfEntityRepositoryBase<ChatMessage, DatabaseContext>, IChatMessageDal
    {
        public EfChatMessageDal(DatabaseContext context) : base(context) { }

        public async Task<List<ChatMessageItemDto>> GetMessagesForAppointmentAsync(Guid appointmentId, DateTime? beforeUtc)
        {
            var query = Context.ChatMessages.AsNoTracking()
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
            return msgs;
        }

        public async Task<List<ChatMessageItemDto>> GetMessagesByThreadIdAsync(Guid threadId, DateTime? beforeUtc)
        {
            var query = Context.ChatMessages.AsNoTracking()
                .Where(m => m.ThreadId == threadId);

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
            return msgs;
        }

        public async Task<List<ChatMessageItemDto>> GetMessagesByThreadIdWithReadStatusAsync(Guid threadId, DateTime? beforeUtc, List<Guid> allParticipantIds)
        {
            var query = Context.ChatMessages.AsNoTracking()
                .Where(m => m.ThreadId == threadId);

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

            if (msgs.Count == 0 || allParticipantIds.Count == 0)
            {
                msgs.Reverse();
                return msgs;
            }

            var messageIds = msgs.Select(m => m.MessageId).ToList();

            // Read receipt'leri yükle
            var receipts = await Context.MessageReadReceipts
                .AsNoTracking()
                .Where(r => r.ThreadId == threadId && messageIds.Contains(r.MessageId))
                .Select(r => new { r.MessageId, r.UserId })
                .ToListAsync();

            var receiptsByMessage = receipts
                .GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(r => r.UserId)));

            foreach (var msg in msgs)
            {
                var requiredReaders = allParticipantIds.Where(id => id != msg.SenderUserId).ToList();
                if (requiredReaders.Count == 0)
                {
                    msg.IsFullyRead = true;
                    continue;
                }

                if (receiptsByMessage.TryGetValue(msg.MessageId, out var readers))
                    msg.IsFullyRead = requiredReaders.All(id => readers.Contains(id));
            }

            msgs.Reverse();
            return msgs;
        }
    }
}
