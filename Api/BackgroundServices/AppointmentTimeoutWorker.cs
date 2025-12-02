using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.BackgroundServices
{
    public class AppointmentTimeoutWorker(IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var notification = scope.ServiceProvider.GetRequiredService<Business.Abstract.INotificationService>();

                var now = DateTime.UtcNow;

                var expired = await db.Appointments
                    .Where(a => a.Status == AppointmentStatus.Pending
                             && a.PendingExpiresAt != null
                             && a.PendingExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                foreach (var appt in expired)
                {
                    appt.Status = AppointmentStatus.Unanswered;
                    if (appt.StoreDecision == DecisionStatus.Pending)
                        appt.StoreDecision = DecisionStatus.NoAnswer;
                    if (appt.FreeBarberDecision == DecisionStatus.Pending)
                        appt.FreeBarberDecision = DecisionStatus.NoAnswer;

                    // herkese noti
                    var targets = new[] { appt.CustomerUserId, appt.BarberStoreUserId, appt.FreeBarberUserId, }
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .Distinct()
                        .ToList();

                    foreach (var u in targets)
                    {
                        await notification.CreateAndPushAsync(
                            u,
                            NotificationType.AppointmentUnanswered,
                            appt.Id,
                            "Randevu yanıtlanmadı",
                            new { appointmentId = appt.Id, status = "Unanswered" }
                        );
                    }
                }

                if (expired.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
