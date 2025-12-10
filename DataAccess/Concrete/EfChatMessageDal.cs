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
    }
}
