using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class SlotManager(IWorkingHourDal workingHourDal, IManuelBarberDal manuelBarberDal, IAppointmentDal
        appointmentDal, IBarberStoreChairDal barberStoreChairDal) : ISlotService
    {

        public async Task<IDataResult<List<WeeklySlotDto>>> GetWeeklySlotsAsync(Guid storeId)
        {
            var today = DateTime.Today;
            var endDate = today.AddDays(6);
            var chairs = await barberStoreChairDal.GetAll(x => x.StoreId == storeId && x.IsActive);
            var chairIds = chairs.Select(c => c.Id).ToList();
            var manualBarberIds = chairs
                .Where(c => c.ManualBarberId.HasValue)
                .Select(c => c.ManualBarberId.Value)
                .Distinct()
                .ToList();
            var manualBarberInfos = await manuelBarberDal.GetManualBarberRatingsAsync(manualBarberIds);
            var workingHours = await workingHourDal.GetAll(x => x.OwnerId == storeId && !x.IsClosed);
            var appointments = await appointmentDal.GetAll(a =>
           a.Status != AppointmentStatus.Cancelled &&
           a.ChairId.HasValue &&
           chairIds.Contains(a.ChairId.Value) &&
           a.StartUtc >= today && a.StartUtc < endDate.AddDays(1));

            var result = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var day = today.AddDays(offset);
                    var dayOfWeek = day.DayOfWeek;
                    var dailyChairs = chairs.Select(chair =>
                    {
                        var wh = workingHours.FirstOrDefault(x =>
                            x.DayOfWeek == dayOfWeek && x.OwnerId == chair.StoreId);
                        if (wh == null) return null;
                        var slots = new List<SlotDto>();
                        var current = wh.StartTime;
                        while (current + TimeSpan.FromHours(1) <= wh.EndTime)
                        {
                            var start = current;
                            var end = current + TimeSpan.FromHours(1);
                            bool isBooked = appointments.Any(a =>
                                a.ChairId == chair.Id &&
                                a.StartUtc.Date == day &&
                                a.StartUtc.TimeOfDay == start);
                            bool isPast = day == DateTime.Today && end <= DateTime.Now.TimeOfDay;
                            slots.Add(new SlotDto
                            {
                                SlotId = Guid.NewGuid(),
                                Start = start.ToString(@"hh\:mm"),
                                End = end.ToString(@"hh\:mm"),
                                IsBooked = isBooked,
                                IsPast = isPast
                            });
                            current += TimeSpan.FromHours(1);
                        }
                        if (!slots.Any()) return null;
                        var mb = chair.ManualBarberId.HasValue
                            ? manualBarberInfos.FirstOrDefault(m => m.BarberId == chair.ManualBarberId.Value)
                            : null;
                        return new ChairSlotDto
                        {
                            ChairId = chair.Id,
                            ChairName = chair.Name,
                            BarberName = mb?.BarberName,
                            BarberRating = mb?.Rating ?? 0,
                            Slots = slots
                        };
                    })
                    .Where(c => c != null)
                    .ToList();
                    return new WeeklySlotDto
                    {
                        Date = day,
                        DayName = day.ToString("dddd", new CultureInfo("tr-TR")),
                        Chairs = dailyChairs
                    };
                })
                .ToList();
            return new SuccessDataResult<List<WeeklySlotDto>>(result);
        }


    }
}

