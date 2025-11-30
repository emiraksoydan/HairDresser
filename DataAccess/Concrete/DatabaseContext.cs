using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(b =>
            {


                //b.Property(u => u.PhoneEncrypted)
                //    .IsRequired();                               
                //b.Property(u => u.PhoneEncryptedNonce)
                //    .HasMaxLength(12)                           
                //    .IsRequired();                              
                //b.Property(u => u.PhoneSearchToken)
                //    .HasMaxLength(32)                            
                //    .IsRequired();                               
                //b.HasIndex(u => u.PhoneSearchToken)
                // .IsUnique()
                // .HasDatabaseName("UX_User_PhoneSearchToken");
            });
            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.ToTable("RefreshTokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
                e.Property(x => x.ReplacedByFingerprint).HasMaxLength(64);
                e.Property(x => x.Device).HasMaxLength(128);
                e.HasIndex(x => x.Fingerprint).IsUnique();
                e.HasIndex(x => x.FamilyId);
                e.HasIndex(x => new { x.UserId, x.RevokedAt, x.ExpiresAt });
            });
            modelBuilder.Entity<Appointment>().Property(x => x.RowVersion).IsRowVersion();
            modelBuilder.Entity<Appointment>().HasIndex(a => new { a.ChairId, a.AppointmentDate, a.StartTime
           ,a.EndTime}).IsUnique();



        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Boş bırakın veya sadece aşağıdaki koşullu koruma:
            if (!optionsBuilder.IsConfigured)
            {
                // optionsBuilder.UseSqlServer("..."); // Gerek yok, Program.cs yapıyor.
            }
        }
        public DbSet<User> Users { get; set; }
        public DbSet<OperationClaim> OperationClaims { get; set; }
        public DbSet<UserOperationClaim> UserOperationClaims { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<BarberStore> BarberStores { get; set; }
        public DbSet<BarberChair> BarberChairs { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<FreeBarber> FreeBarbers { get; set; }
        public DbSet<ManuelBarber> ManuelBarbers { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<WorkingHour> WorkingHours { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ServiceOffering> ServiceOfferings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AppointmentServiceOffering> AppointmentServiceOfferings { get; set; }
        public DbSet<Image> Images { get; set; }

    }
}
