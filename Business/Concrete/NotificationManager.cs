using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class NotificationManager(INotificationDal notificationDal,
        IBadgeService badgeService,
        IRealTimePublisher realtime) : INotificationService
    {
        // ÖNEMLİ: TransactionScopeAspect kaldırıldı çünkü bu metod zaten dış transaction scope içinde çağrılıyor
        // (AppointmentManager içindeki TransactionScopeAspect içinde)
        // İç içe transaction scope'lar sorun yaratabilir ve notification'lar commit edilmeyebilir
        public async Task<IDataResult<Guid>> CreateAndPushAsync(Guid userId, NotificationType type, Guid? appointmentId, string title, object payload, string? body = null)
        {
            var n = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AppointmentId = appointmentId,
                Type = type,
                Title = title,
                Body = body,
                PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                }),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await notificationDal.Add(n);

            var dto = new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                AppointmentId = n.AppointmentId,
                Title = n.Title,
                Body = n.Body,
                PayloadJson = n.PayloadJson,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead
            };

            // Real-time push - Global exception middleware hataları yakalayacak
            await realtime.PushNotificationAsync(userId, dto);

            // Badge güncelle ve SignalR ile tetikle
            try
            {
                var badges = await badgeService.GetCountsAsync(userId);
                if (badges.Success)
                    await realtime.PushBadgeAsync(userId, badges.Data);
            }
            catch
            {
                // Badge güncelleme hatası bildirim gönderimini etkilememeli
            }

            return new SuccessDataResult<Guid>(n.Id);
        }

        public async Task<IDataResult<List<NotificationDto>>> GetAllNotify(Guid userId)
        {
            var list = await notificationDal.GetAll(x => x.UserId == userId);

            var dto = list.OrderByDescending(x => x.CreatedAt)
                .Select(x => new NotificationDto
                {
                    Id = x.Id,
                    Type = x.Type,
                    AppointmentId = x.AppointmentId,
                    Title = x.Title,
                    Body = x.Body,
                    PayloadJson = x.PayloadJson,
                    CreatedAt = x.CreatedAt,
                    IsRead = x.IsRead
                }).ToList();

            return new SuccessDataResult<List<NotificationDto>>(dto);
        }
        public async Task<IDataResult<int>> GetUnreadCountAsync(Guid userId)
        {
            var count = (await notificationDal.GetAll(x => x.UserId == userId && !x.IsRead)).Count;
            return new SuccessDataResult<int>(count);
        }
        public async Task<IDataResult<bool>> MarkReadAsync(Guid userId, Guid notificationId)
        {
            var n = await notificationDal.Get(x => x.Id == notificationId && x.UserId == userId);
            if (n is null) return new ErrorDataResult<bool>(false, "Bildirim bulunamadı");

            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;

            await notificationDal.Update(n);

            // Badge güncelle ve SignalR ile tetikle
            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
                await realtime.PushBadgeAsync(userId, badges.Data);

            return new SuccessDataResult<bool>(true);
        }

        public async Task<IDataResult<bool>> MarkReadByAppointmentIdAsync(Guid userId, Guid appointmentId)
        {
            var notifications = await notificationDal.GetAll(x => x.UserId == userId && x.AppointmentId == appointmentId && !x.IsRead);
            
            if (notifications == null || !notifications.Any())
                return new SuccessDataResult<bool>(true); // Zaten okunmuş veya yok

            // Range ile toplu güncelleme
            var now = DateTime.UtcNow;
            foreach (var n in notifications)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
            
            await notificationDal.UpdateRange(notifications);

            // Badge güncelle ve SignalR ile tetikle
            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
                await realtime.PushBadgeAsync(userId, badges.Data);

            return new SuccessDataResult<bool>(true);
        }
    }
}
