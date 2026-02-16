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
                b.Property(u => u.PhoneNumber)
                    .HasMaxLength(20)
                    .IsRequired();
                
                b.HasIndex(u => u.PhoneNumber)
                    .HasDatabaseName("IX_User_PhoneNumber");
            });
            modelBuilder.Entity<User>().HasOne(u => u.Image).WithMany() .HasForeignKey(u => u.ImageId).OnDelete(DeleteBehavior.SetNull);

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
            modelBuilder.Entity<Appointment>()
           .HasIndex(a => new { a.ChairId, a.AppointmentDate, a.StartTime, a.EndTime })
           .IsUnique()
           .HasFilter("\"Status\" IN (0, 1)");

            modelBuilder.Entity<Appointment>()
              .HasIndex(x => new { x.Status, x.PendingExpiresAt });

            // Performance indexes for active appointment queries
            modelBuilder.Entity<Appointment>()
                .HasIndex(x => new { x.CustomerUserId, x.Status })
                .HasFilter("\"Status\" IN (0, 1)"); // Pending, Approved

            modelBuilder.Entity<Appointment>()
                .HasIndex(x => new { x.FreeBarberUserId, x.Status })
                .HasFilter("\"Status\" IN (0, 1)");

            modelBuilder.Entity<Appointment>()
                .HasIndex(x => new { x.BarberStoreUserId, x.Status })
                .HasFilter("\"Status\" IN (0, 1)");

            modelBuilder.Entity<Appointment>().Property(x => x.RowVersion).IsRowVersion();



            // AppointmentId artık nullable (favori thread'ler için null)
            // Unique index sadece AppointmentId null değilse geçerli olmalı
            modelBuilder.Entity<ChatThread>()
                .HasIndex(x => x.AppointmentId)
                .IsUnique()
                .HasFilter("\"AppointmentId\" IS NOT NULL");

            // Favori thread'ler için composite index (her iki yönü desteklemek için)
            // Store bazlı thread'ler için: StoreId + FavoriteFromUserId + FavoriteToUserId
            // Diğer favori thread'ler için: FavoriteFromUserId + FavoriteToUserId (StoreId null)
            modelBuilder.Entity<ChatThread>()
                .HasIndex(x => new { x.FavoriteFromUserId, x.FavoriteToUserId, x.StoreId })
                .HasFilter("\"FavoriteFromUserId\" IS NOT NULL AND \"FavoriteToUserId\" IS NOT NULL")
                .IsUnique();

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(x => new { x.ThreadId, x.CreatedAt });

            modelBuilder.Entity<MessageReadReceipt>()
                .HasIndex(x => new { x.MessageId, x.UserId })
                .IsUnique();

            modelBuilder.Entity<MessageReadReceipt>()
                .HasIndex(x => new { x.ThreadId, x.UserId });

            modelBuilder.Entity<Notification>()
                .HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });

            // FreeBarber indexes for performance
            modelBuilder.Entity<FreeBarber>()
                .HasIndex(x => x.FreeBarberUserId)
                .IsUnique();

            modelBuilder.Entity<FreeBarber>()
                .HasIndex(x => new { x.IsAvailable, x.Latitude, x.Longitude });

            // Rating index for manuel barber rating queries
            modelBuilder.Entity<Rating>()
                .HasIndex(x => new { x.TargetId, x.Score });

            // Favorite indexes for performance (N+1 query optimization)
            modelBuilder.Entity<Favorite>()
                .HasIndex(x => new { x.FavoritedFromId, x.FavoritedToId, x.IsActive });

            modelBuilder.Entity<Favorite>()
                .HasIndex(x => new { x.FavoritedToId, x.FavoritedFromId, x.IsActive });

            modelBuilder.Entity<Favorite>()
                .HasIndex(x => new { x.FavoritedToId, x.IsActive });

            // Setting index - her kullanıcı için bir settings kaydı olmalı
            modelBuilder.Entity<Setting>()
                .HasIndex(x => x.UserId)
                .IsUnique();

            // Image indexes for efficient owner-based lookups
            modelBuilder.Entity<Image>()
                .HasIndex(x => new { x.OwnerType, x.ImageOwnerId });

            modelBuilder.Entity<Image>()
                .HasIndex(x => x.ImageOwnerId);

            // AppointmentServiceOffering index for appointment-based queries
            modelBuilder.Entity<AppointmentServiceOffering>()
                .HasIndex(x => x.AppointmentId);

            // Price precision ve scale ayarları
            modelBuilder.Entity<ServiceOffering>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<AppointmentServiceOffering>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.Property(e => e.StoreDecision).IsRequired(false);
                entity.Property(e => e.FreeBarberDecision).IsRequired(false);
                entity.Property(e => e.CustomerDecision).IsRequired(false);
            });

            // UserFcmToken indexes for efficient lookups
            modelBuilder.Entity<UserFcmToken>()
                .HasIndex(x => new { x.UserId, x.IsActive });
            
            modelBuilder.Entity<UserFcmToken>()
                .HasIndex(x => x.FcmToken)
                .IsUnique();

            // HelpGuide indexes for efficient lookups
            modelBuilder.Entity<HelpGuide>()
                .HasIndex(x => new { x.UserType, x.IsActive, x.Order });

            // Complaint indexes
            modelBuilder.Entity<Complaint>()
                .HasIndex(x => x.ComplaintFromUserId);

            modelBuilder.Entity<Complaint>()
                .HasIndex(x => new { x.ComplaintFromUserId, x.ComplaintToUserId, x.AppointmentId })
                .IsUnique();

            // Request indexes
            modelBuilder.Entity<Request>()
                .HasIndex(x => x.RequestFromUserId);

            // Blocked indexes for efficient blocking checks
            modelBuilder.Entity<Blocked>()
                .HasIndex(x => x.BlockedFromUserId);

            modelBuilder.Entity<Blocked>()
                .HasIndex(x => x.BlockedToUserId);

            modelBuilder.Entity<Blocked>()
                .HasIndex(x => new { x.BlockedFromUserId, x.BlockedToUserId })
                .IsUnique();

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

        public DbSet<ChatThread> ChatThreads { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<MessageReadReceipt> MessageReadReceipts { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<UserFcmToken> UserFcmTokens { get; set; }
        public DbSet<HelpGuide> HelpGuides { get; set; }

        // Complaint, Request, Blocked
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Blocked> Blockeds { get; set; }

    }
}
