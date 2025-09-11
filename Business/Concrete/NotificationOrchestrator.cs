using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using Business.Abstract;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class NotificationOrchestrator(INotificationService _notif, INotificationDal notificationDal, IAppointmentDal appointmentDal, IBarberStoreDal barberStoreDal, IFreeBarberDal freeBarberDal, IUserDal userDal, IBarberStoreChairDal _chair, IManuelBarberDal manuelBarberDal) : INotificationOrchestrator
    {

        public async Task CustomerToStoreRequestedAsync(Appointment appt, Guid actorUserId, List<AppointmentServiceOffering> snapshotItems)
        {
            var chair = await _chair.Get(x => x.Id == appt.ChairId);
            var actorUser = await userDal.Get(x => x.Id == actorUserId);
            var storeOwner = await barberStoreDal.Get(x => x.Id == chair.StoreId!);
            string name;
            string barberImage = string.Empty;
            if (appt.PerformerUserId == null)
            {
                name = chair.Name ?? string.Empty;
            }
            else
            {
                var barber = await manuelBarberDal.Get(x => x.Id == appt.PerformerUserId);
                var freeBarber = await freeBarberDal.Get(x => x.Id == appt.PerformerUserId);

                if (barber != null)
                {
                    name = $"{barber.FirstName} {barber.LastName}";
                    barberImage = barber.ProfileImageUrl;
                }
                else if (freeBarber != null)
                {
                    name = freeBarber.FullName;
                    barberImage = freeBarber.FreeBarberImageUrl;
                }
                else
                    name = string.Empty;
            }
            await _notif.CreateAsync(
                storeOwner.BarberStoreUserId,
                NotificationType.Customer_To_Store_Requested,
                correlationId: appt.Id,
                new
                {
                    appointmentId = appt.Id,
                    appt.StartUtc,
                    appt.EndTime,
                    appt.PerformerUserId,
                    actorUserId,
                    actorUser.FirstName,
                    actorUser.LastName,
                    actorUser.ProfileImage,
                    services = appt.IsLinkedAppointment ? appt.ServiceOfferings.Select(s => new { s.ServiceName, s.Price }) : snapshotItems.Select(s => new { s.ServiceName, s.Price }),
                    name,
                    barberImage
                }

            );
            if (appt.IsLinkedAppointment && appt.PerformerUserId != null)
            {
                await _notif.CreateAsync(appt.PerformerUserId ?? Guid.Empty, NotificationType.Customer_To_FreeBarber_Requested, correlationId: appt.Id, new
                {
                    appt.Id,
                    appt.StartUtc,
                    appt.EndTime,
                    appt.PerformerUserId,
                    actorUserId,
                    actorUser.FirstName,
                    actorUser.LastName,
                    actorUser.ProfileImage,
                    storeOwner.PricingType,
                    storeOwner.PricingValue,
                    storeOwner.StoreImageUrl,
                    storeOwner.StoreName,
                    storeOwner.Address.AddressLine,

                });
            }
        }

        public async Task StoreInvitesBarberAsync(Appointment appt, Guid storeOwnerUserId, Guid actorUserId)
        {

            var storeOwner = await barberStoreDal.Get(x => x.BarberStoreUserId == storeOwnerUserId);
            await _notif.CreateAsync(
                actorUserId,
                NotificationType.Store_Invite_FreeBarber,
                correlationId: appt.Id,
                new { appointmentId = appt.Id, appt.StartUtc, appt.EndTime, StoreId = storeOwner.Id, storeOwner.PricingType, storeOwner.PricingValue, storeOwner.StoreName, storeOwner.Type, storeOwner.Address.AddressLine, storeOwner.StoreImageUrl }
            );
        }

        public async Task ApprovalDecisionAsync(Appointment appt, UserType byRole, bool approve)
        {
            if (appt.IsLinkedAppointment)
            {
                if (!approve)
                {
                    if (byRole == UserType.FreeBarber)
                    {
                        await _notif.CreateAsync(
                        appt.CustomerId,
                        NotificationType.FreeBarber_Rejected_ToCustomer,
                        correlationId: appt.Id,
                        new { appointmentId = appt.Id, byRole = byRole.ToString(), msg = "talepte bulunduğunuz berber tarafından reddedildi. Lütfen başka berber seçiniz" }, topic: "AppointmentRejected");
                        var findNotify = await notificationDal.Get(x => x.CorrelationId == appt.Id && x.Type == NotificationType.Customer_To_Store_Requested);
                        if (findNotify != null)
                        {
                            var node = JsonNode.Parse(findNotify.Payload) as JsonObject ?? new JsonObject();
                            node["msg"] = "Talepte bulunulan berber tarafından reddedildi. Müşterinin yeni berber seçimi bekleniyor.";
                            findNotify.IsRead = true;
                            findNotify.CreatedAtUtc = DateTime.Now;
                            findNotify.Payload = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                            await notificationDal.Update(findNotify);
                        }

                    }
                    else if (byRole == UserType.BarberStore)
                    {
                        await _notif.CreateAsync(
                        appt.CustomerId,
                        NotificationType.Store_Rejected_ToCustomer,
                        correlationId: appt.Id,
                        new { appointmentId = appt.Id, byRole = byRole.ToString(), msg = "talepte bulunduğunuz dükkan tarafından reddedildi. Lütfen başka dükkan seçiniz" }, topic: "AppointmentRejected");
                        var findNotify = await notificationDal.Get(x => x.CorrelationId == appt.Id && x.Type == NotificationType.Customer_To_FreeBarber_Requested);
                        if (findNotify != null)
                        {
                            var node = JsonNode.Parse(findNotify.Payload) as JsonObject ?? new JsonObject();
                            node["msg"] = "Talepte bulunulan dükkan tarafından reddedildi. Müşterinin yeni berber seçimi bekleniyor.";
                            findNotify.IsRead = true;
                            findNotify.Payload = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                            findNotify.CreatedAtUtc = DateTime.Now;
                            await notificationDal.Update(findNotify);
                        }
                    }
                }
                else if (approve)
                {
                    if (byRole == UserType.FreeBarber)
                    {
                        var findAppointment = await appointmentDal.Get(x => x.Id == appt.Id);
                        if (findAppointment.ChairId == null)
                        {
                            await _notif.CreateAsync(appt.CustomerId, NotificationType.FreeBarber_Approved_ToCustomer, correlationId: appt.Id, new { appointmentId = appt.Id, byRole = byRole.ToString(), msg = "talepte bulunduğunuz berber  isteğinizi onayladı. Lütfen dükkan seçiniz" }, topic: "AppointmentApproved");
                        }
                        else if (findAppointment.ChairId != null && findAppointment.Status == AppointmentStatus.Pending)
                        {
                            var findChair = await _chair.Get(s => s.Id == findAppointment.ChairId);
                            var findStore = await barberStoreDal.Get(x => x.Id == findChair.StoreId);
                            var findBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.PerformerUserId);
                            await _notif.CreateAsync(appt.CustomerId, NotificationType.FreeBarber_Approved_ToCustomer, correlationId: appt.Id, new { appointmentId = appt.Id, appt.StartUtc, appt.EndTime, findStore.Id,findStore.Address.AddressLine,findStore.Type,findStore.StoreImageUrl,findStore.StoreName,appt.ServiceOfferings,findBarber.FreeBarberUserId, findBarber.FullName,findBarber.FreeBarberImageUrl, byRole = byRole.ToString(), msg = "talepte bulunduğunuz berber  isteğinizi onayladı. Dükkan onayı bekleniyor" }, topic: "AppointmentApproved");
                        }
                        await _notif.CreateAsync(appt.CustomerId, NotificationType.FreeBarber_Approved_ToCustomer, correlationId: appt.Id, new { appointmentId = appt.Id, msg = "talepte bulunduğunuz berber  isteğinizi onayladı" }, topic: "AppointmentApproved");
                        var getAllAppointments = await appointmentDal.GetAll(x => x.Id != appt.Id && x.PerformerUserId == appt.PerformerUserId && x.Status == AppointmentStatus.Pending);
                        foreach (var item in getAllAppointments)
                        {
                            item.Status = AppointmentStatus.Rejected;
                            await appointmentDal.Update(item);
                            var getAllNotify = await notificationDal.GetAll(n => n.CorrelationId == item.Id && n.Topic == "AppointmentRequest");
                            foreach (var notify in getAllNotify)
                            {
                                notify.CreatedAtUtc = DateTime.Now;
                                notify.IsRead = true;
                                var node = JsonNode.Parse(notify.Payload) as JsonObject ?? new JsonObject();
                                node["msg"] = "İstek geçerli değil artık meşgulsunüz";
                                notify.Payload = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                                await notificationDal.Update(notify);
                            }
                            if(item.ChairId != null)
                            {
                                var findChair = await _chair.Get(s => s.Id == item.ChairId);
                                var findStore = await barberStoreDal.Get(x => x.Id == findChair.StoreId);
                                await _notif.CreateAsync(findStore.BarberStoreUserId, NotificationType.FreeBarber_Rejected_To_Store, correlationId: appt.Id, new { appointmentId = appt.Id, msg = "talepte bulunduğunuz berber meşgule döndü. Başka berber seçebilirsiniz" }, topic: "AppointmentRejected");
                            }
                            if(item.BookedByType == UserType.Customer)
                            {
                                await _notif.CreateAsync(item.BookedByUserId, NotificationType.FreeBarber_Rejected_ToCustomer, correlationId: appt.Id, new { appointmentId = appt.Id, msg = "talepte bulunduğunuz berber meşgule döndü. Başka berber seçebilirsiniz" }, topic: "AppointmentRejected");
                            }
                        }
                    }
                    else if(byRole == UserType.BarberStore)
                    {

                    }
                }
            }
        }

        public async Task FreeBarberToStoreAsync(Appointment appt, Guid actorUserId)
        {
            var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.BookedByUserId);
            await _notif.CreateAsync(
                actorUserId,
                NotificationType.FreeBarber_To_Store_Requested,
                appt.Id,
                new { appointmentId = appt.Id, appt.StartUtc, appt.EndTime, freeBarber.FreeBarberUserId, freeBarber.FreeBarberImageUrl, freeBarber.FullName, freeBarber.Address.AddressLine });
        }
    }
}
