using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfChatThreadDal : EfEntityRepositoryBase<ChatThread, DatabaseContext>, IChatThreadDal
    {
        public EfChatThreadDal(DatabaseContext context) : base(context) { }

        public async Task<List<ChatThreadListItemDto>> GetThreadsForUserAsync(Guid userId, AppointmentStatus[] allowedStatuses)
        {
            return await Context.ChatThreads.AsNoTracking()
                .Join(Context.Appointments.AsNoTracking(),
                      t => t.AppointmentId,
                      a => a.Id,
                      (t, a) => new { t, a })
                .Where(x =>
                    allowedStatuses.Contains(x.a.Status) &&
                    (x.t.CustomerUserId == userId || x.t.StoreOwnerUserId == userId || x.t.FreeBarberUserId == userId))
                .OrderByDescending(x => x.t.LastMessageAt ?? x.t.CreatedAt)
                .Select(x => new ChatThreadListItemDto
                {
                    AppointmentId = x.a.Id,
                    Status = x.a.Status,
                    Title = string.Empty, // Title will be set in business layer (ChatManager)
                    LastMessagePreview = x.t.LastMessagePreview,
                    LastMessageAt = x.t.LastMessageAt,
                    UnreadCount = x.t.CustomerUserId == userId ? x.t.CustomerUnreadCount :
                                  x.t.StoreOwnerUserId == userId ? x.t.StoreUnreadCount :
                                  x.t.FreeBarberUserId == userId ? x.t.FreeBarberUnreadCount : 0
                })
                .ToListAsync();
        }
    }
}
