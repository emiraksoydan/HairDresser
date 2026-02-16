using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfMessageReadReceiptDal : EfEntityRepositoryBase<MessageReadReceipt, DatabaseContext>, IMessageReadReceiptDal
    {
        public EfMessageReadReceiptDal(DatabaseContext context) : base(context) { }

        public async Task<List<Guid>> MarkThreadMessagesReadAsync(Guid threadId, Guid userId)
        {
            // Thread'deki userId tarafından GÖNDERİLMEMİŞ mesajları bul
            var messagesToRead = await Context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == threadId && m.SenderUserId != userId && !m.IsSystem)
                .Select(m => m.Id)
                .ToListAsync();

            if (messagesToRead.Count == 0)
                return new List<Guid>();

            // Zaten okunmuş olanları çıkar
            var alreadyRead = await Context.MessageReadReceipts
                .AsNoTracking()
                .Where(r => r.ThreadId == threadId && r.UserId == userId && messagesToRead.Contains(r.MessageId))
                .Select(r => r.MessageId)
                .ToListAsync();

            var alreadyReadSet = new HashSet<Guid>(alreadyRead);
            var newlyRead = messagesToRead.Where(id => !alreadyReadSet.Contains(id)).ToList();

            if (newlyRead.Count == 0)
                return new List<Guid>();

            var now = DateTime.UtcNow;
            var receipts = newlyRead.Select(messageId => new MessageReadReceipt
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                ThreadId = threadId,
                UserId = userId,
                ReadAt = now
            }).ToList();

            await Context.MessageReadReceipts.AddRangeAsync(receipts);
            await Context.SaveChangesAsync();

            return newlyRead;
        }

        public async Task<HashSet<Guid>> GetFullyReadMessageIdsAsync(Guid threadId, List<Guid> allParticipantIds)
        {
            if (allParticipantIds.Count == 0)
                return new HashSet<Guid>();

            // Thread'deki tüm mesajlar (sistem mesajları hariç)
            var messages = await Context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsSystem)
                .Select(m => new { m.Id, m.SenderUserId })
                .ToListAsync();

            if (messages.Count == 0)
                return new HashSet<Guid>();

            var messageIds = messages.Select(m => m.Id).ToList();

            // Bu mesajlara ait tüm read receipt'leri al
            var receipts = await Context.MessageReadReceipts
                .AsNoTracking()
                .Where(r => r.ThreadId == threadId && messageIds.Contains(r.MessageId))
                .Select(r => new { r.MessageId, r.UserId })
                .ToListAsync();

            // Her mesaj için receipt gruplama
            var receiptsByMessage = receipts
                .GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(r => r.UserId)));

            var fullyRead = new HashSet<Guid>();

            foreach (var msg in messages)
            {
                // Gönderici dışındaki katılımcılar
                var requiredReaders = allParticipantIds.Where(id => id != msg.SenderUserId).ToList();
                if (requiredReaders.Count == 0)
                {
                    // Sadece bir katılımcı varsa (kendi kendine) tam okundu say
                    fullyRead.Add(msg.Id);
                    continue;
                }

                if (!receiptsByMessage.TryGetValue(msg.Id, out var readers))
                    continue;

                // Tüm gerekli okuyucular okumuş mu?
                if (requiredReaders.All(id => readers.Contains(id)))
                    fullyRead.Add(msg.Id);
            }

            return fullyRead;
        }
    }
}
