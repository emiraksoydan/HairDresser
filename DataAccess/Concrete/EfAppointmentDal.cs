using Core.DataAccess.EntityFramework;
using Core.Utilities.Helpers;
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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DataAccess.Concrete
{
    public class EfAppointmentDal : EfEntityRepositoryBase<Appointment, DatabaseContext>, IAppointmentDal
    {
        private readonly DatabaseContext _context;
        public EfAppointmentDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<ChairSlotDto>> GetAvailibilitySlot(Guid storeId, DateOnly dateOnly, CancellationToken ct)
        {
            const int slotMinutes = 60;

            // 1) Koltuklar
            var chairs = await _context.BarberChairs.AsNoTracking()
                .Where(c => c.StoreId == storeId)
                .Select(c => new
                {
                    c.Id,
                    ChairName = c.Name,
                    c.ManuelBarberId
                })
                .ToListAsync(ct);

            if (chairs.Count == 0)
                return new List<ChairSlotDto>();

            var chairIds = chairs.Select(x => x.Id).ToList();

            // 2) O günün aktif randevuları (chair bazlı)
            var appts = await _context.Appointments.AsNoTracking()
                .Where(a => a.ChairId != null
                    && chairIds.Contains(a.ChairId.Value)
                    && a.AppointmentDate == dateOnly
                    && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved))
                .Select(a => new { ChairId = a.ChairId!.Value, a.StartTime, a.EndTime })
                .ToListAsync(ct);

            var apptMap = appts
                .GroupBy(x => x.ChairId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (x.StartTime, x.EndTime)).ToList()
                );

            // 3) WorkingHour (store)
            var wh = await _context.WorkingHours.AsNoTracking()
                .Where(w => w.OwnerId == storeId
                    && w.DayOfWeek == dateOnly.DayOfWeek
                    && w.IsClosed == false)
                .Select(w => new { w.StartTime, w.EndTime })
                .FirstOrDefaultAsync(ct);

            var slotRanges = wh is null
                ? new List<(TimeSpan start, TimeSpan end)>()
                : BuildSlots(wh.StartTime, wh.EndTime, slotMinutes);

            // 4) TR zamanı (IsPast)
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var today = DateOnly.FromDateTime(nowLocal);

            // 5) Manuel berber isimleri
            var manualIds = chairs
                .Where(x => x.ManuelBarberId != null)
                .Select(x => x.ManuelBarberId!.Value)
                .Distinct()
                .ToList();

            var manualMap = manualIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.ManuelBarbers.AsNoTracking()
                    .Where(m => manualIds.Contains(m.Id))
                    .Select(m => new { m.Id, m.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName,ct);

            // 6) Manuel berber rating ortalaması
            var ratingRows = await _context.Ratings.AsNoTracking()
                .Where(r => manualIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new { TargetId = g.Key, Avg = g.Average(x => x.Score), Count = g.Count() })
                .ToListAsync(ct);

            var ratingMap = ratingRows.ToDictionary(x => x.TargetId, x => (x.Avg, x.Count));

            // 7) Response
            var result = new List<ChairSlotDto>(chairs.Count);

            foreach (var c in chairs)
            {
                Guid? barberId = null;
                string? barberName = null;
                double? barberRating = null;

                if (c.ManuelBarberId != null)
                {
                    barberId = c.ManuelBarberId.Value;
                    manualMap.TryGetValue(barberId.Value, out barberName);

                    if (ratingMap.TryGetValue(barberId.Value, out var r))
                        barberRating = r.Avg;
                }
                apptMap.TryGetValue(c.Id, out var chairAppts);
                chairAppts ??= new List<(TimeSpan StartTime, TimeSpan EndTime)>();

                var slots = slotRanges.Select(s =>
                {
                    var booked = chairAppts.Any(a => a.StartTime < s.end && a.EndTime > s.start);

                    var isPast =
                        dateOnly < today ? true :
                        dateOnly > today ? false :
                        s.start <= nowLocal.TimeOfDay;

                    return new SlotDto
                    {
                        SlotId = StableSlotId(c.Id, dateOnly, s.start, s.end),
                        Start = ToHHmm(s.start),
                        End = ToHHmm(s.end),
                        IsBooked = booked,
                        IsPast = isPast
                    };
                }).ToList();

                result.Add(new ChairSlotDto
                {
                    ChairId = c.Id,
                    ChairName = c.ChairName,
                    BarberId = barberId,
                    BarberName = barberName,
                    BarberRating = barberRating,
                    Slots = slots
                });
            }

            return result;
        }


        static List<(TimeSpan start, TimeSpan end)> BuildSlots(TimeSpan start, TimeSpan end, int slotMin)
        {
            var list = new List<(TimeSpan, TimeSpan)>();
            for (var t = start; t + TimeSpan.FromMinutes(slotMin) <= end; t += TimeSpan.FromMinutes(slotMin))
                list.Add((t, t + TimeSpan.FromMinutes(slotMin)));
            return list;
        }

        static string ToHHmm(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";

        static Guid StableSlotId(Guid chairId, DateOnly date, TimeSpan start, TimeSpan end)
        {
            var raw = $"{chairId:N}|{date:yyyyMMdd}|{(int)start.TotalMinutes}|{(int)end.TotalMinutes}";
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            return new Guid(bytes);
        }
    }
}
