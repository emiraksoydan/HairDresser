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
            modelBuilder.Entity<BarberStoreListDto>(eb =>
            {
                eb.HasNoKey().ToView(null);           
                eb.Ignore(x => x.IsOpenNow);         
                eb.Ignore(x => x.ServiceOfferings);                                         
            });
            modelBuilder.Entity<FreeBarberListDto>().HasNoKey().ToView(null);
            modelBuilder.Entity<Favorite>()
            .HasOne(f => f.FavoritedFrom)
            .WithMany()
            .HasForeignKey(f => f.FavoritedFromId)
             .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.FavoritedTo)
                .WithMany()
                .HasForeignKey(f => f.FavoritedToId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Category>()
            .HasOne(c => c.Parent)
            .WithMany() 
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
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
        public DbSet<AddressInfo> AddressInfos { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<BarberStore> BarberStores { get; set; }
        public DbSet<BarberChair> BarberChairs { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<FreeBarber> FreeBarbers { get; set; }
        public DbSet<ManuelBarber> ManuelBarbers { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<WorkingHour> WorkingHours { get; set; }
        public DbSet<BarberStoreListDto> BarberStoreListDtos { get; set; }
        public DbSet<FreeBarberListDto> FreeBarberListDtos { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ServiceOffering> ServiceOfferings { get; set; }

    }
}
