namespace Core.Utilities.Configuration
{
    public class AppointmentSettings
    {
        public int PendingTimeoutMinutes { get; set; } = 5;
        public double MaxDistanceKm { get; set; } = 1.0;
        public int SlotMinutes { get; set; } = 60;
    }

    public class BackgroundServicesSettings
    {
        public int AppointmentTimeoutWorkerIntervalSeconds { get; set; } = 300; // 5 dakika (300 saniye) - appsettings.json ile uyumlu
    }
}

