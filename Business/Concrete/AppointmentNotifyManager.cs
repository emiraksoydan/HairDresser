using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class AppointmentNotifyManager(
        IAppointmentDal appointmentDal,
        IBarberStoreDal barberStoreDal,
        IBarberStoreChairDal chairDal,
        IManuelBarberDal manuelBarberDal,
        IImageDal imageDal,
        IUserSummaryService userSummarySvc,
        INotificationService notificationSvc
    ) : IAppointmentNotifyService
    {
        public async Task<IResult> NotifyAsync(
            Guid appointmentId,
            NotificationType type,
            Guid? actorUserId = null,
            object? extra = null)
        {
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorResult("Randevu bulunamadı");

            // recipients (customer, storeOwner, freebarber)
            var recipients = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            // tek seferde summary çek
            var userMapRes = await userSummarySvc.GetManyAsync(recipients);
            var userMap = (userMapRes.Success && userMapRes.Data is not null)
                ? userMapRes.Data
                : new Dictionary<Guid, UserNotifyDto>();

            UserNotifyDto? GetUser(Guid? id)
                => id.HasValue && userMap.TryGetValue(id.Value, out var u) ? u : null;

            var customerInfo = GetUser(appt.CustomerUserId);
            var freeBarberInfo = GetUser(appt.FreeBarberUserId);

            // Store (ownerId ile bulunuyor)
            var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId);

            // store image null-safe
            string? storeImageUrl = null;
            if (store is not null)
            {
                var imgs = await imageDal.GetAll(x => x.ImageOwnerId == store.Id);
                storeImageUrl = imgs
                    .OrderByDescending(i => i.CreatedAt)
                    .FirstOrDefault()
                    ?.ImageUrl;
            }

            var storeInfo = store is null ? null : new StoreNotifyDto
            {
                StoreId = store.Id,
                StoreOwnerUserId = store.BarberStoreOwnerId,
                StoreName = store.StoreName,
                ImageUrl = storeImageUrl
            };

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
                            chairInfo.ManuelBarberName = mb.FullName;
                    }
                }
            }

            // Not: FreeBarber varsa "manuel barber olmayacak" demiştin.
            // Yine de defensive kalalım:
            if (appt.FreeBarberUserId.HasValue && chairInfo is not null)
            {
                chairInfo.ManuelBarberId = null;
                chairInfo.ManuelBarberName = null;
            }

            foreach (var userId in recipients)
            {
                var role =
                    appt.CustomerUserId == userId ? "customer" :
                    appt.BarberStoreUserId == userId ? "store" :
                    appt.FreeBarberUserId == userId ? "freebarber" : "other";

                var title = BuildTitle(type, role);

                var payload = new AppointmentNotifyPayloadDto
                {
                    AppointmentId = appt.Id,
                    EventKey = type.ToString(),
                    RecipientRole = role,
                    Date = appt.AppointmentDate,
                    StartTime = appt.StartTime,
                    EndTime = appt.EndTime,
                    ActorUserId = actorUserId,
                    Store = storeInfo,
                    Chair = chairInfo,
                    Extra = extra,

                    // role'e göre aşağıda set edilecek
                    Customer = null,
                    FreeBarber = null,
                };

                // role bazlı “kimleri dahil edelim?”
                if (role == "store")
                {
                    payload.Customer = customerInfo;      // store -> müşteri bilgisi
                    payload.FreeBarber = freeBarberInfo;  // varsa
                }
                else if (role == "customer")
                {
                    payload.FreeBarber = freeBarberInfo;  // varsa
                }
                else if (role == "freebarber")
                {
                    payload.Customer = customerInfo;      // freebarber -> müşteri (varsa)
                }

                await notificationSvc.CreateAndPushAsync(
                    userId: userId,
                    type: type,
                    appointmentId: appt.Id,
                    title: title,
                    payload: payload,
                    body: null
                );
            }

            return new SuccessResult();
        }

        private static string BuildTitle(NotificationType type, string role)
        {
            return type switch
            {
                NotificationType.AppointmentCreated =>
                    role == "store" ? "Yeni randevu talebi" :
                    role == "freebarber" ? "Yeni randevu isteği" :
                    "Randevun oluşturuldu",

                NotificationType.AppointmentApproved => "Randevu onaylandı",
                NotificationType.AppointmentRejected => "Randevu reddedildi",
                NotificationType.AppointmentCancelled => "Randevu iptal edildi",
                NotificationType.AppointmentCompleted => "Randevu tamamlandı",
                NotificationType.AppointmentUnanswered => "Randevu yanıtlanmadı",
                _ => "Bildirim"
            };
        }
    }
}
