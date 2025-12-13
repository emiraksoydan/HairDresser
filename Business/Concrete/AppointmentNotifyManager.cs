using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Linq;

namespace Business.Concrete
{
    public class AppointmentNotifyManager(
        IAppointmentDal appointmentDal,
        IBarberStoreDal barberStoreDal,
        IBarberStoreChairDal chairDal,
        IManuelBarberDal manuelBarberDal,
        IImageDal imageDal,
        IUserSummaryService userSummarySvc,
        INotificationService notificationSvc,
        IAppointmentServiceOffering appointmentServiceOfferingDal,
        IBadgeService badgeService,
        IRealTimePublisher realtime
    ) : IAppointmentNotifyService
    {
        // Overload 1: AppointmentId ile (mevcut randevular için - transaction dışında)
        public async Task<IResult> NotifyAsync(
            Guid appointmentId,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null)
            {
                return new ErrorResult(Messages.AppointmentNotFound);
            }
            
            return await NotifyAsyncInternal(appt, type, actorUserId, extra);
        }

        // Appointment entity ile (yeni oluşturulan randevular için - transaction içinde)
        public async Task<IResult> NotifyWithAppointmentAsync(
            Entities.Concrete.Entities.Appointment appointment,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null)
        {
            if (appointment is null)
            {
                return new ErrorResult(Messages.AppointmentNotFound);
            }
            
            return await NotifyAsyncInternal(appointment, type, actorUserId, extra);
        }

        // Internal method: Ortak bildirim gönderme mantığı
        private async Task<IResult> NotifyAsyncInternal(
            Entities.Concrete.Entities.Appointment appt,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null)
        {
            // ÖNEMLİ: Randevu oluşturulduğunda status Pending olmalı, Unanswered olmamalı
            // Eğer AppointmentCreated notification'ı gönderiliyorsa ve status Unanswered ise,
            // bu bir hata - status Pending olmalı
            if (type == NotificationType.AppointmentCreated && appt.Status == AppointmentStatus.Unanswered)
            {
                // Status'u Pending'e çevir (eğer yanlışlıkla Unanswered ise)
                appt.Status = AppointmentStatus.Pending;
                await appointmentDal.Update(appt);
            }

            // DÜZELTME: Randevuyu gönderen kişiyi (actorUserId) recipient listesinden hariç tut
            // AppointmentUnanswered durumunda TÜM ilgili kişilere bildirim gitmeli (actor dahil)
            // Diğer durumlarda gönderen kişi (actor) hariç tutulmalı
            var recipients = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue && (type == NotificationType.AppointmentUnanswered || (actorUserId.HasValue && x.Value != actorUserId.Value)))
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // Eğer hiç recipient yoksa hata döndür
            if (recipients.Count == 0)
            {
                return new ErrorResult("Randevu için alıcı bulunamadı.");
            }

            // ÖNEMLİ: Payload için TÜM ilgili kullanıcıların bilgilerini çek (recipients değil)
            // Çünkü payload'da customer, store owner, free barber bilgileri olmalı
            // Örneğin customer appointment oluşturduğunda, store owner customer bilgisini görmeli
            var allRelevantUserIds = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // tek seferde summary çek - TÜM ilgili kullanıcılar için
            var userMapRes = await userSummarySvc.GetManyAsync(allRelevantUserIds);
            var userMap = (userMapRes.Success && userMapRes.Data is not null)
                ? userMapRes.Data
                : new Dictionary<Guid, UserNotifyDto>();

            UserNotifyDto? GetUser(Guid? id)
                => id.HasValue && userMap.TryGetValue(id.Value, out var u) ? u : null;

            var customerInfo = GetUser(appt.CustomerUserId);
            var freeBarberInfo = GetUser(appt.FreeBarberUserId);

            // Store (ownerId ile bulunuyor)
            // ÖNEMLİ: BarberStoreUserId null ise store bulunamaz
            BarberStore? store = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
            }

            // store image null-safe - PERFORMANCE: Use GetLatestImageAsync
            string? storeImageUrl = null;
            if (store is not null)
            {
                var storeImage = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                storeImageUrl = storeImage?.ImageUrl;
            }

            StoreNotifyDto? storeInfo = null;
            if (store is not null)
            {
                storeInfo = new StoreNotifyDto
                {
                    StoreId = store.Id,
                    StoreOwnerUserId = store.BarberStoreOwnerId,
                    StoreName = store.StoreName,
                    ImageUrl = storeImageUrl,
                    Type = store.Type
                };
            }

            // Chair + ManuelBarber (opsiyonel)
            ChairNotifyDto? chairInfo = null;
            if (appt.ChairId.HasValue)
            {
                var chair = await chairDal.Get(c => c.Id == appt.ChairId.Value);
                if (chair is not null)
                {
                    chairInfo = new ChairNotifyDto
                    {
                        ChairId = chair.Id,
                        ChairName = chair.Name,              // sadece isimli de olabilir
                        ManuelBarberId = chair.ManuelBarberId // null olabilir
                    };

                    // ManuelBarber sadece varsa
                    if (chair.ManuelBarberId.HasValue)
                    {
                        var mb = await manuelBarberDal.Get(x => x.Id == chair.ManuelBarberId.Value);
                        if (mb is not null)
                        {
                            chairInfo.ManuelBarberName = mb.FullName;
                            
                            // DÜZELTME: Manuel barber fotoğrafını ekle - PERFORMANCE: Use GetLatestImageAsync
                            var manuelBarberImage = await imageDal.GetLatestImageAsync(mb.Id, ImageOwnerType.ManuelBarber);
                            
                            if (manuelBarberImage != null)
                            {
                                chairInfo.ManuelBarberImageUrl = manuelBarberImage.ImageUrl;
                            }
                        }
                    }
                }
            }

            // Not: FreeBarber varsa "manuel barber olmayacak" demiştin.
            // Yine de defensive kalalım:
            if (appt.FreeBarberUserId.HasValue && chairInfo is not null)
            {
                chairInfo.ManuelBarberId = null;
                chairInfo.ManuelBarberName = null;
                chairInfo.ManuelBarberImageUrl = null;
            }

            // Service Offerings - Appointment'a ait hizmetleri al
            // ÖNEMLİ: Transaction içinde çağrılıyorsa (NotifyWithAppointmentAsync), 
            // AppointmentServiceOffering kayıtları henüz commit edilmemiş olabilir.
            // Bu durumda appointmentServiceOfferingDal.GetAll boş dönebilir.
            // Çözüm: Veritabanından al, eğer boşsa transaction commit edilene kadar birkaç kez dene
            var serviceOfferings = new List<ServiceOfferingGetDto>();
            var appointmentServiceOfferings = await appointmentServiceOfferingDal.GetAll(x => x.AppointmentId == appt.Id);
            
            
            // Service offerings'i her zaman payload'a ekle (boş olsa bile null olarak)
            if (appointmentServiceOfferings != null && appointmentServiceOfferings.Any())
            {
                serviceOfferings = appointmentServiceOfferings
                    .Select(aso => new ServiceOfferingGetDto
                    {
                        Id = aso.ServiceOfferingId,
                        ServiceName = aso.ServiceName,
                        Price = aso.Price
                    })
                    .ToList();
            }

            foreach (var userId in recipients)
            {
                var role =
                    appt.CustomerUserId == userId ? "customer" :
                    appt.BarberStoreUserId == userId ? "store" :
                    appt.FreeBarberUserId == userId ? "freebarber" : "other";

                var title = BuildTitle(type, role, appt.Status, userId, appt);

                // PAYLOAD OPTİMİZASYONU: Gereksiz bilgileri çıkar
                // 1. AppointmentCreated durumunda: İsteği gönderen kişi kendi bilgisini alıcıya göndermesin
                // 2. Geri dönüşlerde ve cevapsız durumlarda: Alıcı kendi bilgisini görmesin
                
                UserNotifyDto? payloadCustomer = customerInfo;
                StoreNotifyDto? payloadStore = storeInfo;
                UserNotifyDto? payloadFreeBarber = freeBarberInfo;

                // AppointmentCreated durumunda: İsteği gönderen kişi kendi bilgisini alıcıya göndermesin
                if (type == NotificationType.AppointmentCreated)
                {
                    // Müşteri dükkana göndermişse → dükkana giden bildirimde dükkan bilgisi olmasın
                    if (appt.RequestedBy == AppointmentRequester.Customer && role == "store")
                    {
                        payloadStore = null;
                    }
                    // Dükkan serbest berbere göndermişse → serbest berbere giden bildirimde serbest berber bilgisi olmasın
                    else if (appt.RequestedBy == AppointmentRequester.Store && role == "freebarber")
                    {
                        payloadFreeBarber = null;
                    }
                    // Serbest berber dükkana göndermişse → dükkana giden bildirimde dükkan bilgisi olmasın
                    else if (appt.RequestedBy == AppointmentRequester.FreeBarber && role == "store")
                    {
                        payloadStore = null;
                    }
                }
                // Geri dönüşlerde (Approved, Rejected, Cancelled, Completed) veya cevapsız durumlarda (Unanswered):
                // Alıcı kendi bilgisini görmesin
                else if (type == NotificationType.AppointmentApproved ||
                         type == NotificationType.AppointmentRejected ||
                         type == NotificationType.AppointmentCancelled ||
                         type == NotificationType.AppointmentCompleted ||
                         type == NotificationType.AppointmentUnanswered)
                {
                    // Customer'a gidiyorsa → customer bilgisi göndermeye gerek yok
                    if (role == "customer")
                    {
                        payloadCustomer = null;
                    }
                    // Store'a gidiyorsa → store bilgisi göndermeye gerek yok
                    else if (role == "store")
                    {
                        payloadStore = null;
                    }
                    // FreeBarber'a gidiyorsa → freebarber bilgisi göndermeye gerek yok
                    else if (role == "freebarber")
                    {
                        payloadFreeBarber = null;
                    }
                }

                var payload = new AppointmentNotifyPayloadDto
                {
                    AppointmentId = appt.Id,
                    EventKey = type.ToString(),
                    RecipientRole = role,
                    Date = appt.AppointmentDate,
                    StartTime = appt.StartTime,
                    EndTime = appt.EndTime,
                    ActorUserId = actorUserId,
                    
                    // Optimize edilmiş payload - gereksiz bilgiler çıkarıldı
                    Customer = payloadCustomer,
                    Store = payloadStore,
                    FreeBarber = payloadFreeBarber,
                    Chair = chairInfo,            // Manuel barber fotoğrafı ile birlikte
                    
                    Extra = extra,

                    // Status bilgileri - Frontend'de filtreleme için
                    Status = appt.Status,
                    StoreDecision = appt.StoreDecision,
                    FreeBarberDecision = appt.FreeBarberDecision,

                    // Service offerings - Frontend'de hizmet butonlarını göstermek için
                    ServiceOfferings = serviceOfferings.Any() ? serviceOfferings : null,
                };

                // role bazlı "kimleri dahil edelim?"
                // Global exception middleware hataları yakalayacak
                await notificationSvc.CreateAndPushAsync(
                    userId: userId,
                    type: type,
                    appointmentId: appt.Id,
                    title: title,
                    payload: payload,
                    body: null
                );
            }

            // Tüm recipient'lara badge güncellemesi gönder
            foreach (var userId in recipients)
            {
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
            }

            return new SuccessResult();
        }

        private static string BuildTitle(NotificationType type, string role, AppointmentStatus status, Guid recipientUserId, Entities.Concrete.Entities.Appointment appt)
        {
            return type switch
            {
                NotificationType.AppointmentCreated =>
                    role == "store" ? Messages.NotificationNewAppointmentRequestForStore :
                    role == "freebarber" ? Messages.NotificationNewAppointmentRequest :
                    Messages.AppointmentCreatedNotification,

                NotificationType.AppointmentApproved => 
                    role == "customer" ? Messages.AppointmentApprovedNotification :
                    Messages.AppointmentApprovedNotification,

                NotificationType.AppointmentRejected => 
                    role == "customer" ? Messages.AppointmentRejectedNotification :
                    Messages.AppointmentRejectedNotification,

                NotificationType.AppointmentCancelled => Messages.AppointmentCancelledNotification,
                NotificationType.AppointmentCompleted => Messages.AppointmentCompletedNotification,
                
                NotificationType.AppointmentUnanswered =>
                    // Karar vermesi gereken kişiye "Randevuyu cevaplamadınız", diğerlerine "Randevunuz cevaplanamadı"
                    // StoreDecision ve FreeBarberDecision null değil, DecisionStatus enum'ı (Pending, NoAnswer, Approved, Rejected)
                    // Pending veya NoAnswer ise karar verilmemiş demektir
                    ((role == "store" && (appt.StoreDecision == DecisionStatus.Pending || appt.StoreDecision == DecisionStatus.NoAnswer)) || 
                     (role == "freebarber" && (appt.FreeBarberDecision == DecisionStatus.Pending || appt.FreeBarberDecision == DecisionStatus.NoAnswer)))
                        ? "Randevuyu cevaplamadınız"
                        : "Randevunuz cevaplanamadı",
                        
                _ => Messages.NotificationDefault
            };
        }
    }
}
