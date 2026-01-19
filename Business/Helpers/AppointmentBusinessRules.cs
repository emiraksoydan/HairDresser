using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    public class AppointmentBusinessRules
    {
        private readonly IAppointmentDal _appointmentDal;
        private readonly IUserDal _userDal;
        private readonly IFreeBarberDal _freeBarberDal;
        private readonly IBarberStoreDal _barberStoreDal;
        private readonly IBarberStoreChairDal _chairDal;
        private const double MaxDistanceKm = 1.0; // Maksimum mesafe: 1 kilometre
        private static readonly AppointmentStatus[] Active = [AppointmentStatus.Pending, AppointmentStatus.Approved];

        public AppointmentBusinessRules(
            IAppointmentDal appointmentDal,
            IUserDal userDal,
            IFreeBarberDal freeBarberDal,
            IBarberStoreDal barberStoreDal,
            IBarberStoreChairDal chairDal)
        {
            _appointmentDal = appointmentDal;
            _userDal = userDal;
            _freeBarberDal = freeBarberDal;
            _barberStoreDal = barberStoreDal;
            _chairDal = chairDal;
        }

        public async Task<IResult> CheckUserIsCustomer(Guid userId)
        {
            var user = await _userDal.Get(x => x.Id == userId);
            if (user is null)
                return new ErrorResult(Messages.UserNotFound);

            if (user.UserType != UserType.Customer)
                return new ErrorResult(Messages.OnlyCustomersCanCreateAppointment);

            return new SuccessResult();
        }

        /// <summary>
        /// Checks active appointment rules based on requester type
        /// </summary>
        /// <param name="customerId">Customer user ID</param>
        /// <param name="freeBarberId">FreeBarber user ID</param>
        /// <param name="storeId">Specific store ID (not owner ID) - for multi-store support</param>
        /// <param name="requestedBy">Who is creating the appointment</param>
        public async Task<IResult> CheckActiveAppointmentRules(Guid? customerId, Guid? freeBarberId, Guid? storeId, AppointmentRequester requestedBy)
        {
            switch (requestedBy)
            {
                case AppointmentRequester.Customer:
                    if (customerId.HasValue)
                    {
                        var customerHasActive = await _appointmentDal.AnyAsync(x =>
                            x.CustomerUserId == customerId.Value &&
                            Active.Contains(x.Status));
                        if (customerHasActive)
                            return new ErrorResult(Messages.CustomerAlreadyHasActiveAppointment);
                    }

                    if (freeBarberId.HasValue)
                    {
                        // FreeBarber'ın Customer'dan gelen aktif randevusu var mı?
                        // ÖNEMLİ: StoreSelectionType.StoreSelection (Dükkan Seç) durumunda
                        // FreeBarber pending randevusu olsa bile başka Customer'dan istek alamaz
                        var freeBarberHasActiveFromCustomer = await _appointmentDal.AnyAsync(x =>
                            x.FreeBarberUserId == freeBarberId.Value &&
                            x.RequestedBy == AppointmentRequester.Customer &&
                            Active.Contains(x.Status));
                        if (freeBarberHasActiveFromCustomer)
                            return new ErrorResult(Messages.FreeBarberAlreadyHasActiveAppointment);
                    }

                    // Store kontrolü Customer → Store senaryosunda YAPILMAMALI
                    // Çünkü dükkan birden fazla koltukta aynı anda randevu alabilir
                    // EnsureChairNoOverlapAsync zaten spesifik koltuk kontrolü yapıyor
                    break;

                case AppointmentRequester.Store:
                    // ÖNEMLİ: Store → FreeBarber senaryosunda SADECE aynı store + aynı FreeBarber kontrolü yap
                    // Aynı store farklı FreeBarber'lara çağrı yapabilir (ALLOWED)
                    // Farklı store'lar aynı FreeBarber'a çağrı yapabilir (ALLOWED)
                    // Aynı store aynı FreeBarber'a tekrar çağrı yapamaz (NOT ALLOWED)
                    
                    if (storeId.HasValue && freeBarberId.HasValue)
                    {
                        // Bu STORE'un bu FREEBARBER ile aktif randevusu var mı?
                        var storeFreeBarberHasActive = await _appointmentDal.AnyAsync(x =>
                            x.StoreId == storeId.Value &&
                            x.FreeBarberUserId == freeBarberId.Value &&
                            Active.Contains(x.Status));
                        if (storeFreeBarberHasActive)
                            return new ErrorResult(Messages.StoreAlreadyHasActiveAppointmentWithThisFreeBarber);
                    }
                    
                    // FreeBarber genel müsaitlik kontrolü (herhangi bir aktif randevusu varsa alamaz)
                    if (freeBarberId.HasValue)
                    {
                        var freeBarberHasActive = await _appointmentDal.AnyAsync(x =>
                            x.FreeBarberUserId == freeBarberId.Value &&
                            Active.Contains(x.Status));
                        if (freeBarberHasActive)
                            return new ErrorResult(Messages.FreeBarberAlreadyHasActiveAppointment);
                    }
                    break;

                case AppointmentRequester.FreeBarber:
                    if (freeBarberId.HasValue)
                    {
                        // FreeBarber kendi panel'inden Store'a randevu alıyor
                        // ÖNEMLİ: Eğer FreeBarber'ın Customer'dan gelen StoreSelection (3'lü sistem) randevusu varsa
                        // o zaman dükkan randevusu ALABİLİR (dükkan seçmek için)
                        // FAKAT İsteğime Göre (CustomRequest) randevusu varsa dükkan randevusu ALAMAZ
                        
                        // Önce Customer'dan gelen aktif randevuyu kontrol et
                        var customerAppointment = await _appointmentDal.Get(x =>
                            x.FreeBarberUserId == freeBarberId.Value &&
                            x.RequestedBy == AppointmentRequester.Customer &&
                            Active.Contains(x.Status));
                        
                        if (customerAppointment != null)
                        {
                            // CustomRequest (İsteğime Göre) ise FreeBarber başka iş alamaz
                            if (customerAppointment.StoreSelectionType == StoreSelectionType.CustomRequest)
                                return new ErrorResult(Messages.FreeBarberAlreadyHasActiveAppointment);
                            
                            // StoreSelection (Dükkan Seç) ise FreeBarber dükkan randevusu alabilir (izin ver)
                        }
                        
                        // FreeBarber'ın kendi açtığı aktif randevusu var mı?
                        var freeBarberHasOwnActive = await _appointmentDal.AnyAsync(x =>
                            x.FreeBarberUserId == freeBarberId.Value &&
                            x.RequestedBy == AppointmentRequester.FreeBarber &&
                            Active.Contains(x.Status));
                        if (freeBarberHasOwnActive)
                            return new ErrorResult(Messages.FreeBarberAlreadyHasActiveAppointment);
                    }

                    // Store kontrolü FreeBarber → Store senaryosunda YAPILMAMALI
                    // Çünkü dükkan birden fazla koltukta aynı anda randevu alabilir
                    // EnsureChairNoOverlapAsync zaten spesifik koltuk kontrolü yapıyor
                    break;
            }

            return new SuccessResult();
        }

        public IResult CheckDistance(double fromLat, double fromLon, double toLat, double toLon, string errorMessage)
        {
            var v1 = CheckValidCoords(fromLat, fromLon, "İstek");
            if (!v1.Success) return v1;

            var v2 = CheckValidCoords(toLat, toLon, "Hedef");
            if (!v2.Success) return v2;

            var km = HaversineKm(fromLat, fromLon, toLat, toLon);
            if (km > MaxDistanceKm)
                return new ErrorResult($"{errorMessage} (Mesafe: {km:0.00} km)");

            return new SuccessResult();
        }

        public async Task<IResult> CheckFreeBarberExists(Guid freeBarberUserId)
        {
            var fb = await _freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null)
                return new ErrorResult(Messages.FreeBarberNotFound);

            var v = CheckValidCoords(fb.Latitude, fb.Longitude, "Serbest berber");
            if (!v.Success)
                return new ErrorResult(v.Message);

            return new SuccessResult();
        }

        public async Task<IResult> CheckFreeBarberAvailable(Guid freeBarberUserId)
        {
            var fb = await _freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
            if (fb is null)
                return new ErrorResult(Messages.FreeBarberNotFound);

            if (!fb.IsAvailable)
                return new ErrorResult(Messages.FreeBarberNotAvailable);

            return new SuccessResult();
        }

        public async Task<IResult> CheckStoreExists(Guid storeId)
        {
            var store = await _barberStoreDal.Get(x => x.Id == storeId);
            if (store is null)
                return new ErrorResult(Messages.StoreNotFound);

            return new SuccessResult();
        }

        public async Task<IResult> CheckStoreOwnership(Guid storeId, Guid ownerId)
        {
            var store = await _barberStoreDal.Get(x => x.Id == storeId && x.BarberStoreOwnerId == ownerId);
            if (store is null)
                return new ErrorResult(Messages.StoreNotFoundOrNotOwner);

            return new SuccessResult();
        }

        public async Task<IResult> CheckChairBelongsToStore(Guid chairId, Guid storeId)
        {
            var chair = await _chairDal.Get(c => c.Id == chairId && c.StoreId == storeId);
            if (chair is null)
                return new ErrorResult(Messages.ChairNotInStore);

            return new SuccessResult();
        }

        public IResult CheckTimeRangeValid(TimeSpan start, TimeSpan end)
        {
            if (start >= end)
                return new ErrorResult(Messages.StartTimeGreaterThanEndTime);

            return new SuccessResult();
        }

        public IResult CheckDateNotPast(DateOnly date, TimeSpan time)
        {
            var now = DateTime.UtcNow;
            var apptDateTime = date.ToDateTime(TimeOnly.FromTimeSpan(time));
            
            if (apptDateTime <= now)
                return new ErrorResult(Messages.AppointmentDateCannotBePast);

            return new SuccessResult();
        }

        private IResult CheckValidCoords(double lat, double lon, string who)
        {
            if (lat == 0 && lon == 0)
                return new ErrorResult($"{who} konumu ayarlı değil.");
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                return new ErrorResult($"{who} konumu geçersiz.");
            return new SuccessResult();
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180.0);
    }
}
