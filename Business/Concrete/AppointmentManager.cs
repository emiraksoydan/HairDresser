using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class AppointmentManager(IAppointmentDal appointmentDal, IBarberStoreChairDal barberStoreChairDal, INotificationOrchestrator notificationOrchestrator, IServiceOfferingDal serviceOfferingDal, IAppointmentServiceOffering appointmentServiceOffering, IUserDal userDal, IFreeBarberDal freeBarberDal,IBarberStoreDal barberStoreDal) : IAppointmentService
    {
        public async Task<IResult> ApproveAsync(Guid appointmentId, Guid userId, bool approve)
        {
            var a = await appointmentDal.Get(x => x.Id == appointmentId);
            if (a == null)
            {
                return new ErrorResult("Onaylamada hata");
            }
            var user = await userDal.Get(u => u.Id == userId);
            if (user == null)
                return new ErrorResult("Onaylamada hata");
            var userType = user.UserType;
            if (userType == UserType.FreeBarber)
            {
                a.BarberApproval = approve;   
            }   
            else if (userType == UserType.BarberStore)
                a.StoreApproval = approve;
            if (!approve)
            {
                a.Status = AppointmentStatus.Rejected;
                if (userType == UserType.FreeBarber && a.IsLinkedAppointment == true)
                    a.PerformerUserId = null;
                else if (userType == UserType.BarberStore && a.IsLinkedAppointment == true)
                    a.ChairId = null;
            }
            else if (a.BarberApproval == true && a.StoreApproval == true && a.IsLinkedAppointment == true)
                a.Status = AppointmentStatus.Approved;
            else if (a.IsLinkedAppointment == false && (a.BarberApproval == true || a.StoreApproval == true))
                a.Status = AppointmentStatus.Approved;

            a.UpdatedAt = DateTime.Now;
            await appointmentDal.Update(a);
            if (approve && a.BarberApproval == true && a.Status == AppointmentStatus.Approved)
            {
                var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == user.Id);
                if (freeBarber != null) {
                    freeBarber.IsAvailable = false;
                }
            }
            await notificationOrchestrator.ApprovalDecisionAsync(a, userType, approve);
            return new SuccessResult();
        }

        public async Task<IDataResult<Guid>> CustomerCreatesForFreeBarberAsync(Guid customerId, Guid freeBarberUserId, DateTime startUtc, DateTime endUtc, IEnumerable<Guid> serviceOfferingIds)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var findAppointment = await appointmentDal.Get(ap => ap.IsLinkedAppointment == true && ap.PerformerUserId == null && ap.CustomerId == customerId && (ap.Status == AppointmentStatus.Rejected || ap.Status == AppointmentStatus.Pending));
            if (findAppointment != null)
            {
                findAppointment.PerformerUserId = freeBarberUserId;
                findAppointment.Status = AppointmentStatus.Pending;
                findAppointment.UpdatedAt = DateTime.Now;
                findAppointment.StartUtc = startUtc;
                findAppointment.EndTime = endUtc;
                await appointmentDal.Update(findAppointment);
                return new SuccessDataResult<Guid>(findAppointment.Id, "Randevu hazırlığınız sürüyor,serbest berberden onay bekleniyor. Eğer dükkan seçmediyseniz lütfen berber dükkanı seçiniz");
            }
            var hasPending = await appointmentDal.AnyAsync(a => a.BookedByUserId == customerId && a.Status == AppointmentStatus.Pending);
            if (hasPending)
            {
                return new ErrorDataResult<Guid>("Bekleyen randevu bulunmaktadır.");
            }
            var hasApproved = await appointmentDal.AnyAsync(a =>
                a.BookedByUserId == customerId &&
                a.Status == AppointmentStatus.Approved
            );
            if (hasApproved)
            {
                return new ErrorDataResult<Guid>("Aktif randevu bulunmaktadır.");
            }
            if (serviceOfferingIds == null || !serviceOfferingIds.Any())
                return new ErrorDataResult<Guid>("En az bir hizmet seçilmelidir.");
            var offerings = await serviceOfferingDal.GetServiceOfferingsByIdsAsync(serviceOfferingIds.Distinct());
            if (offerings.Count == 0)
                return new ErrorDataResult<Guid>("Hizmet bulunamadı.");
            var snapshotItems = offerings.Select(o => new AppointmentServiceOffering
            {
                Id = Guid.NewGuid(),
                ServiceOfferingId = o.Id,
                ServiceName = o.ServiceName,
                Price = o.Price
            }).ToList();
            var a = new Appointment
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                PerformerUserId = freeBarberUserId,
                BookedByUserId = customerId,
                BookedByType = UserType.Customer,
                StartUtc = startUtc,
                EndTime = endUtc,
                BarberApproval = null,
                StoreApproval = null,
                IsLinkedAppointment = true,
                Status = AppointmentStatus.Pending,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
            await appointmentDal.Add(a);
            snapshotItems.ForEach(x => x.AppointmentId = a.Id);
            await appointmentServiceOffering.AddRange(snapshotItems);
            scope.Complete();
            return new SuccessDataResult<Guid>(a.Id, "Randevu hazırlığınız sürüyor,serbest berberden onay bekleniyor. Eğer dükkan seçmediyseniz lütfen berber dükkanı seçiniz");
        }

        public async Task<IDataResult<Guid>> CustomerCreatesForStoreAsync(Guid customerId, Guid chairId, Guid performerUserId, DateTime startUtc, DateTime endUtc, IEnumerable<Guid> serviceOfferingIds)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var findAppointment = await appointmentDal.Get(ap => ap.IsLinkedAppointment == true && ap.ChairId == null && ap.CustomerId == customerId && (ap.Status == AppointmentStatus.Rejected || ap.Status == AppointmentStatus.Pending));
            if (findAppointment != null)
            {
                var overlapExistsChair = await appointmentDal.AnyAsync(a => a.ChairId == chairId && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved) && a.StartUtc < endUtc && startUtc < a.EndTime);
                if (overlapExistsChair)
                    return new ErrorDataResult<Guid>("Seçilen saat aralığı dolu.");
                findAppointment.ChairId = chairId;
                findAppointment.Status = AppointmentStatus.Pending;
                findAppointment.StartUtc = startUtc;
                findAppointment.EndTime = endUtc;
                findAppointment.UpdatedAt = DateTime.Now;
                await appointmentDal.Update(findAppointment);
                await notificationOrchestrator.CustomerToStoreRequestedAsync(findAppointment, customerId, findAppointment.ServiceOfferings.ToList());
                return new SuccessDataResult<Guid>(findAppointment.Id, "Dükkan randevunuz hazırlık sürecinde onay bekliyor.Lütfen serbest berber seçmediyseniz serbest berber seçiniz");
            }
            var hasPending = await appointmentDal.AnyAsync(a => a.BookedByUserId == customerId && a.Status == AppointmentStatus.Pending);
            if (hasPending)
            {
                return new ErrorDataResult<Guid>("Bekleyen randevu bulunmaktadır.");
            }
            var hasApproved = await appointmentDal.AnyAsync(a =>
                a.BookedByUserId == customerId &&
                a.Status == AppointmentStatus.Approved
            );

            if (hasApproved)
            {
                return new ErrorDataResult<Guid>("Aktif randevu bulunmaktadır.");
            }
            if (serviceOfferingIds == null || !serviceOfferingIds.Any())
                return new ErrorDataResult<Guid>("En az bir hizmet seçilmelidir.");
            var chairResult = await barberStoreChairDal.Get(x => x.Id == chairId);
            if (chairResult == null)
                return new ErrorDataResult<Guid>("Geçersiz koltuk.");
            var overlapExists = await appointmentDal.AnyAsync(a =>
            a.ChairId == chairId &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved) &&
               a.StartUtc < endUtc && startUtc < a.EndTime);
            if (overlapExists)
                return new ErrorDataResult<Guid>("Seçilen saat aralığı dolu.");
            var offerings = await serviceOfferingDal.GetServiceOfferingsByIdsAsync(serviceOfferingIds.Distinct());
            if (offerings.Count == 0)
                return new ErrorDataResult<Guid>("Hizmet bulunamadı.");
            var snapshotItems = offerings.Select(o => new AppointmentServiceOffering
            {
                Id = Guid.NewGuid(),
                ServiceOfferingId = o.Id,
                ServiceName = o.ServiceName,
                Price = o.Price
            }).ToList();
            var a = new Appointment
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                PerformerUserId = performerUserId,
                BookedByUserId = customerId,
                BookedByType = UserType.Customer,
                StartUtc = startUtc,
                EndTime = endUtc,
                Status = AppointmentStatus.Pending,
                BarberApproval = null,
                StoreApproval = null,
                ChairId = chairId,
                IsLinkedAppointment = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await appointmentDal.Add(a);
            snapshotItems.ForEach(x => x.AppointmentId = a.Id);
            await appointmentServiceOffering.AddRange(snapshotItems);
            await notificationOrchestrator.CustomerToStoreRequestedAsync(a, customerId, snapshotItems);
            scope.Complete();
            return new SuccessDataResult<Guid>(a.Id, "Randevu hazırlığınız oluşturuldu dükkan onayı bekleniyor.");
        }

        public async Task<IDataResult<Guid>> FreeBarberToStoreAsync(Guid customerId, Guid chairId, DateTime startUtc, DateTime endUtc, IEnumerable<Guid> serviceOfferingIds)
        {
            var findAppointment = await appointmentDal.Get(ap => ap.IsLinkedAppointment == false  && ap.CustomerId == customerId && ap.BookedByType == UserType.FreeBarber && (ap.Status == AppointmentStatus.Rejected || ap.Status == AppointmentStatus.Pending));
            var findChair = await barberStoreChairDal.Get(x => x.Id == chairId);
            var findStore = await barberStoreDal.Get(x => x.Id == findChair.StoreId);
            if (findAppointment != null) {
                return new ErrorDataResult<Guid>("Dükkan için oluşturulmuş randevu hazırlığınız vardır. Başka randevu şu an hazırlanamaz");
            }
            var a = new Appointment
            {
                IsLinkedAppointment = false,
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                PerformerUserId = customerId,
                BookedByUserId = customerId,
                BookedByType = UserType.FreeBarber,
                StartUtc = startUtc,
                EndTime = endUtc,
                ChairId = chairId,
                Status = AppointmentStatus.Pending,
                BarberApproval = null,
                StoreApproval = null,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await appointmentDal.Add(a);
            await notificationOrchestrator.FreeBarberToStoreAsync(a, findStore.BarberStoreUserId);
            return new SuccessDataResult<Guid>(a.Id);
        }


        public async Task<IDataResult<Guid>> StoreInvitesBarberAsync(Guid storeOwnerUserId, Guid freeBarberUserId, Guid storeId, DateTime startUtc, DateTime endUtc)
        {
            var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            var findAppointment = await appointmentDal.AnyAsync(x => x.IsLinkedAppointment == false && x.BookedByUserId == storeOwnerUserId && x.PerformerUserId == freeBarberUserId && (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Approved));
            if(findAppointment != null)
            {
                return new ErrorDataResult<Guid>("Bu berberle ilgili randevu hazırlığınız bulunmaktadır");
            }
            if (freeBarber == null)
            {
                return new ErrorDataResult<Guid>("Serbest berber bulunamadı");
            }
            else if (!freeBarber.IsAvailable)
            {
                return new ErrorDataResult<Guid>("Serbest berber müsait değil");
            }
            var a = new Appointment
            {
                IsLinkedAppointment = false,               
                Id = Guid.NewGuid(),
                CustomerId = storeOwnerUserId,
                PerformerUserId = freeBarberUserId,
                BookedByUserId = storeOwnerUserId,
                BookedByType = UserType.BarberStore,
                StartUtc = startUtc,
                EndTime = endUtc,
                Status = AppointmentStatus.Pending,
                BarberApproval = null,
                StoreApproval = null,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await appointmentDal.Add(a);
            await notificationOrchestrator.StoreInvitesBarberAsync(a,storeOwnerUserId, freeBarber.FreeBarberUserId);
            return new SuccessDataResult<Guid>(a.Id);
        }
    }
}
