using Business.Abstract;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Configuration;
using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace Api.BackgroundServices
{
    public class AppointmentTimeoutWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesSettings> backgroundServicesSettings,
        ILogger<AppointmentTimeoutWorker> logger
    ) : BackgroundService
    {
        private readonly BackgroundServicesSettings _settings = backgroundServicesSettings.Value;
        private readonly ILogger<AppointmentTimeoutWorker> _logger = logger;

        [TransactionScopeAspect]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var notifySvc = scope.ServiceProvider.GetRequiredService<IAppointmentNotifyService>();
                var freeBarberDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IFreeBarberDal>();
                var realtime = scope.ServiceProvider.GetRequiredService<IRealTimePublisher>();

                var now = DateTime.UtcNow;

                var expired = await db.Appointments
                    .Where(a => a.Status == AppointmentStatus.Pending
                             && a.PendingExpiresAt != null
                             && a.PendingExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                if (expired.Any())
                {
                    _logger.LogInformation("AppointmentTimeoutWorker: Found {Count} expired appointments", expired.Count);
                }

                // Her appointment için işlem yap
                foreach (var appt in expired)
                {
                    appt.Status = AppointmentStatus.Unanswered;
                    appt.PendingExpiresAt = null;
                    appt.UpdatedAt = DateTime.UtcNow;

                    if (appt.StoreDecision == DecisionStatus.Pending)
                        appt.StoreDecision = DecisionStatus.NoAnswer;

                    if (appt.FreeBarberDecision == DecisionStatus.Pending)
                        appt.FreeBarberDecision = DecisionStatus.NoAnswer;

                    // freebarber release
                    if (appt.FreeBarberUserId.HasValue)
                    {
                        var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                        if (fb != null)
                        {
                            fb.IsAvailable = true;
                            fb.UpdatedAt = DateTime.UtcNow;
                            await freeBarberDal.Update(fb);
                        }
                    }

                    // notify all participants (persist + realtime + badge)
                    await notifySvc.NotifyAsync(
                        appt.Id,
                        NotificationType.AppointmentUnanswered,
                        actorUserId: null,
                        extra: new { reason = "timeout_5min", status = "Unanswered" }
                    );
                }

                if (expired.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(_settings.AppointmentTimeoutWorkerIntervalSeconds), stoppingToken);
            }
        }
    }
}
