using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class NotificationManager(INotificationDal notificationDal,
        IRealTimePublisher realtime,
        IBadgeService badgeService,
        IBadgeUpdateService badgeUpdateService,
        IAppointmentDal appointmentDal,
        IPushNotificationService? pushNotificationService = null) : INotificationService
    {
        // ÖNEMLİ: TransactionScopeAspect kaldırıldı çünkü bu metod zaten dış transaction scope içinde çağrılıyor
        // (AppointmentManager içindeki TransactionScopeAspect içinde)
        // İç içe transaction scope'lar sorun yaratabilir ve notification'lar commit edilmeyebilir
        public async Task<IDataResult<Guid>> CreateAndPushAsync(Guid userId, NotificationType type, Guid? appointmentId, string title, object payload, string? body = null)
        {
            // DUPLICATE KONTROLÜ: Aynı userId, appointmentId ve type'a sahip okunmamış notification var mı?
            // ÖNEMLİ: Notification Type değişmemeli - sadece payload güncellenmeli
            if (appointmentId.HasValue)
            {
                // AppointmentUnanswered için özel kontrol:
                // Eğer kullanıcıya bu appointment için herhangi bir notification varsa, onu güncelle (type değiştirme, sadece payload)
                // Eğer hiç notification yoksa, yeni AppointmentUnanswered oluştur
                if (type == NotificationType.AppointmentUnanswered)
                {
                    // Bu appointment için herhangi bir notification var mı? (type fark etmez
                    var existingAny = await notificationDal.Get(x =>x.UserId == userId &&  x.AppointmentId == appointmentId && x.Type == NotificationType.AppointmentUnanswered);
                    
                    if (existingAny != null)
                    {
                        // Mevcut notification var - sadece payload'ı güncelle, type değiştirme
                        existingAny.Title = title;
                        existingAny.Body = body;
                        existingAny.PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = false
                        });
                        existingAny.CreatedAt = DateTime.UtcNow; // Zamanı güncelle
                        
                        await notificationDal.Update(existingAny);
                        
                        var dto = new NotificationDto
                        {
                            Id = existingAny.Id,
                            Type = existingAny.Type, // Type değişmedi - aynı kaldı
                            AppointmentId = existingAny.AppointmentId,
                            Title = existingAny.Title,
                            Body = existingAny.Body,
                            PayloadJson = existingAny.PayloadJson,
                            CreatedAt = existingAny.CreatedAt,
                            IsRead = existingAny.IsRead
                        };
                        
                        // Güncellenmiş notification'ı SignalR ile push et
                        await realtime.PushNotificationAsync(userId, dto);
                        
                        // Push notification via FCM (app is closed/background)
                        if (pushNotificationService != null)
                        {
                            try
                            {
                                await pushNotificationService.SendPushNotificationAsync(userId, dto);
                            }
                            catch
                            {
                                // FCM push failure should not break the notification update
                            }
                        }

                        // Badge count'u transaction commit sonrası güncellemek için schedule et
                        badgeUpdateService.ScheduleBadgeUpdate(userId);

                        return new SuccessDataResult<Guid>(existingAny.Id);
                    }
                    // Eğer hiç notification yoksa, aşağıda yeni oluşturulacak
                }
                // AppointmentDecisionUpdated için duplicate'a izin ver
                else if (type == NotificationType.AppointmentDecisionUpdated)
                {
                    // AppointmentDecisionUpdated için duplicate'a izin ver (birden fazla gönderilebilir)
                }
                // Diğer type'lar için: Aynı type için duplicate kontrolü yap
                else
                {
                    var existing = await notificationDal.Get(x => 
                        x.UserId == userId && 
                        x.AppointmentId == appointmentId && 
                        x.Type == type && 
                        !x.IsRead);
                    
                    if (existing != null)
                    {
                        // Duplicate var, mevcut notification'ı güncelle ve döndür
                        existing.Title = title;
                        existing.Body = body;
                        existing.PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = false
                        });
                        existing.CreatedAt = DateTime.UtcNow; // Zamanı güncelle
                        
                        await notificationDal.Update(existing);
                        
                        var dto = new NotificationDto
                        {
                            Id = existing.Id,
                            Type = existing.Type,
                            AppointmentId = existing.AppointmentId,
                            Title = existing.Title,
                            Body = existing.Body,
                            PayloadJson = existing.PayloadJson,
                            CreatedAt = existing.CreatedAt,
                            IsRead = existing.IsRead
                        };
                        
                        // Güncellenmiş notification'ı SignalR ile push et
                        await realtime.PushNotificationAsync(userId, dto);
                        
                        // Push notification via FCM (app is closed/background)
                        if (pushNotificationService != null)
                        {
                            try
                            {
                                await pushNotificationService.SendPushNotificationAsync(userId, dto);
                            }
                            catch
                            {
                                // FCM push failure should not break the notification update
                            }
                        }

                        // Badge count'u transaction commit sonrası güncellemek için schedule et
                        badgeUpdateService.ScheduleBadgeUpdate(userId);

                        return new SuccessDataResult<Guid>(existing.Id);
                    }
                }
            }

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

            var notificationDto = new NotificationDto
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

            // Real-time push via SignalR (app is open)
            await realtime.PushNotificationAsync(userId, notificationDto);

            // Push notification via FCM (app is closed/background)
            if (pushNotificationService != null)
            {
                try
                {
                    await pushNotificationService.SendPushNotificationAsync(userId, notificationDto);
                }
                catch
                {
                    // FCM push failure should not break the notification creation
                    // Log error but continue
                }
            }

            // Badge count'u transaction commit sonrası güncellemek için schedule et
            // ÖNEMLİ: Transaction içinde olduğumuz için badge count henüz doğru hesaplanamaz
            // Transaction commit edildikten sonra ProcessScheduledBadgeUpdatesAsync ile güncellenir
            badgeUpdateService.ScheduleBadgeUpdate(userId);

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
            // PERFORMANCE: CountAsync kullan (GetAll().Count yerine)
            var count = await notificationDal.CountAsync(x => x.UserId == userId && !x.IsRead);
            return new SuccessDataResult<int>(count);
        }
        public async Task<IDataResult<bool>> MarkReadAsync(Guid userId, Guid notificationId)
        {
            var n = await notificationDal.Get(x => x.Id == notificationId && x.UserId == userId);
            if (n is null) return new ErrorDataResult<bool>(false, "Bildirim bulunamadı");

            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;

            await notificationDal.Update(n);

            // Badge count'u direkt hesaplayıp push et
            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
            {
                await realtime.PushBadgeAsync(userId, badges.Data);
            }

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

            // Badge count'u direkt hesaplayıp push et
            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
            {
                await realtime.PushBadgeAsync(userId, badges.Data);
            }

            return new SuccessDataResult<bool>(true);
        }

        public async Task<IDataResult<bool>> UpdateNotificationPayloadByAppointmentAsync(
            Guid appointmentId,
            AppointmentStatus status,
            DecisionStatus? storeDecision = null,
            DecisionStatus? freeBarberDecision = null,
            DecisionStatus? customerDecision = null,
            DateTime? pendingExpiresAt = null)
        {
            // Appointment'a ait tüm notification'ları bul (okunmuş/okunmamış fark etmez)
            var notifications = await notificationDal.GetAll(x => x.AppointmentId == appointmentId);
            
            if (notifications == null || !notifications.Any())
                return new SuccessDataResult<bool>(true);

            var updatedNotifications = new List<NotificationDto>();

            foreach (var n in notifications)
            {
                // Action notification payload'larını güncelle:
                // - AppointmentCreated: Store/FreeBarber karar butonları için
                // - StoreApprovedSelection: 3'lü sistemde Customer final karar butonları için
                // ÖNEMLİ: Rejected status'ünde eski bildirimleri güncelleme (mantık hatası)
                // Cancelled durumunda güncelleme yapılmalı (FreeBarber'ın reddet butonu kalkmalı)
                if (status == AppointmentStatus.Rejected)
                    continue;
                
                if (n.Type != NotificationType.AppointmentCreated &&
                    n.Type != NotificationType.StoreApprovedSelection)
                    continue;

                // Payload'ı parse et
                if (string.IsNullOrEmpty(n.PayloadJson) || n.PayloadJson.Trim() == "{}")
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(n.PayloadJson);
                    var root = doc.RootElement;

                    if (pendingExpiresAt.HasValue)
                    {
                        if (!root.TryGetProperty("pendingExpiresAt", out var pendingProp))
                            continue;

                        DateTime? payloadPending = null;
                        if (pendingProp.ValueKind == JsonValueKind.String)
                        {
                            var pendingString = pendingProp.GetString();
                            if (!string.IsNullOrWhiteSpace(pendingString) &&
                                DateTime.TryParse(pendingString, CultureInfo.InvariantCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                    out var parsedPending))
                            {
                                payloadPending = parsedPending;
                            }
                        }

                        var targetPending = pendingExpiresAt.Value;
                        if (targetPending.Kind == DateTimeKind.Unspecified)
                            targetPending = DateTime.SpecifyKind(targetPending, DateTimeKind.Utc);
                        else
                            targetPending = targetPending.ToUniversalTime();

                        if (!payloadPending.HasValue || payloadPending.Value != targetPending)
                            continue;
                    }

                    // Build a new payload map
                    var payloadDict = new Dictionary<string, object?>();
                    
                    // Mevcut tüm property'leri kopyala (object ve array'leri de dahil)
                    foreach (var prop in root.EnumerateObject())
                    {
                        // Status ve decision'ları atla, bunları aşağıda güncelleyeceğiz
                        if (prop.Name.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("storeDecision", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("freeBarberDecision", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("customerDecision", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("pendingExpiresAt", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        
                        payloadDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                            System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? (object)intVal : prop.Value.GetDecimal(),
                            System.Text.Json.JsonValueKind.True => true,
                            System.Text.Json.JsonValueKind.False => false,
                            System.Text.Json.JsonValueKind.Null => null,
                            System.Text.Json.JsonValueKind.Object => JsonSerializer.Deserialize<object>(prop.Value.GetRawText()),
                            System.Text.Json.JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(prop.Value.GetRawText()),
                            _ => prop.Value.GetRawText()
                        };
                    }
                    
                    // Status ve decision'ları güncelle
                    // ÖNEMLİ: Frontend'de enum değerleri number (int) olarak bekleniyor, string değil
                    payloadDict["status"] = (int)status;
                    if (storeDecision.HasValue)
                        payloadDict["storeDecision"] = (int)storeDecision.Value;
                    if (freeBarberDecision.HasValue)
                        payloadDict["freeBarberDecision"] = (int)freeBarberDecision.Value;
                    payloadDict["customerDecision"] = customerDecision.HasValue ? (int)customerDecision.Value : null;
                    payloadDict["pendingExpiresAt"] = pendingExpiresAt;
                    
                    // Geri JSON string'e çevir
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false 
                    };
                    n.PayloadJson = JsonSerializer.Serialize(payloadDict, options);
                    // UpdatedAt property'si Notification entity'sinde yok, sadece CreatedAt ve ReadAt var
                    // Bu yüzden UpdatedAt ataması yapmıyoruz
                    
                    await notificationDal.Update(n);
                    
                    // SignalR ile push et
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
                    
                    updatedNotifications.Add(dto);
                }
                catch
                {
                    // Payload parse edilemezse devam et
                    continue;
                }
            }

            // Tüm kullanıcılara güncellenmiş notification'ları push et
            var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
            foreach (var userId in userIds)
            {
                var userNotifications = updatedNotifications.Where(n => 
                    notifications.Any(orig => orig.Id == n.Id && orig.UserId == userId)
                ).ToList();
                
                foreach (var dto in userNotifications)
                {
                    try
                    {
                        // SignalR ile push et (app is open)
                        await realtime.PushNotificationAsync(userId, dto);
                        
                        // FCM ile push et (app is closed/background)
                        if (pushNotificationService != null)
                        {
                            try
                            {
                                await pushNotificationService.SendPushNotificationAsync(userId, dto);
                            }
                            catch
                            {
                                // FCM push failure should not break the notification update
                            }
                        }
                    }
                    catch
                    {
                        // SignalR push başarısız olursa devam et
                    }
                }
            }

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid notificationId)
        {
            var n = await notificationDal.Get(x => x.Id == notificationId && x.UserId == userId);
            if (n is null)
                return new ErrorDataResult<bool>(false, "Bildirim bulunamadı");

            // Randevu status'ünü kontrol et (appointmentId üzerinden)
            if (n.AppointmentId.HasValue)
            {
                var appointment = await appointmentDal.Get(x => x.Id == n.AppointmentId.Value);
                if (appointment != null && 
                    (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Approved))
                {
                    return new ErrorDataResult<bool>(false, "Pending veya Approved durumundaki randevuların bildirimleri silinemez");
                }
            }

            // Okunmamışsa okunmuş yap
            if (!n.IsRead)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                await notificationDal.Update(n);
                
                // Badge count'u güncelle
                var badges = await badgeService.GetCountsAsync(userId);
                if (badges.Success)
                {
                    await realtime.PushBadgeAsync(userId, badges.Data);
                }
            }

            // Bildirimi sil
            await notificationDal.Remove(n);

            // Badge count'u tekrar güncelle (silme sonrası)
            var badgesAfter = await badgeService.GetCountsAsync(userId);
            if (badgesAfter.Success)
            {
                await realtime.PushBadgeAsync(userId, badgesAfter.Data);
            }

            return new SuccessDataResult<bool>(true);
        }

        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> DeleteAllAsync(Guid userId)
        {
            var notifications = await notificationDal.GetAll(x => x.UserId == userId);

            if (notifications == null || !notifications.Any())
                return new ErrorDataResult<bool>(false, "Silinecek bildirim bulunamadı.");

            // Appointment ID'leri topla
            var appointmentIds = notifications
                .Where(n => n.AppointmentId.HasValue)
                .Select(n => n.AppointmentId.Value)
                .Distinct()
                .ToList();

            // Tüm appointment'ları bir seferde çek
            var appointments = appointmentIds.Any() 
                ? await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id))
                : new List<Appointment>();
            
            var appointmentStatusDict = appointments.ToDictionary(a => a.Id, a => a.Status);

            var notificationsToDelete = new List<Notification>();
            var notificationsToMarkRead = new List<Notification>();

            foreach (var n in notifications)
            {
                // Randevu status'ünü kontrol et (appointmentId üzerinden)
                if (n.AppointmentId.HasValue && appointmentStatusDict.TryGetValue(n.AppointmentId.Value, out var status))
                {
                    if (status == AppointmentStatus.Pending || status == AppointmentStatus.Approved)
                    {
                        continue; // Bu bildirimi atla
                    }
                }

                // Okunmamışsa okunmuş yap
                if (!n.IsRead)
                {
                    n.IsRead = true;
                    n.ReadAt = DateTime.UtcNow;
                    notificationsToMarkRead.Add(n);
                }

                notificationsToDelete.Add(n);
            }

            // Önce okunmamış bildirimleri okunmuş yap
            if (notificationsToMarkRead.Any())
            {
                await notificationDal.UpdateRange(notificationsToMarkRead);
            }

            // Sonra silinebilir bildirimleri sil (tek tek)
            if (!notificationsToDelete.Any())
            {
                return new ErrorDataResult<bool>(false, "Silinecek bildirim bulunamadı. Tüm bildirimler Pending veya Approved durumundaki randevulara ait.");
            }

            foreach (var n in notificationsToDelete)
            {
                await notificationDal.Remove(n);
            }

            // Badge count'u güncelle
            var badges = await badgeService.GetCountsAsync(userId);
            if (badges.Success)
            {
                await realtime.PushBadgeAsync(userId, badges.Data);
            }

            return new SuccessDataResult<bool>(true);
        }
    }
}
