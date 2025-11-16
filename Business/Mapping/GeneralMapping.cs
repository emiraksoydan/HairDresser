using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Mapping
{
    public class GeneralMapping : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<BarberStoreCreateDto, BarberStore>
                .NewConfig()
                .Map(d => d.Id, s => Guid.NewGuid())
                .Map(d => d.IsActive, s => true)
                .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow);


            TypeAdapterConfig<ManuelBarberCreateDto, ManuelBarber>
                .NewConfig()
                .Map(d => d.Id,
                 s => string.IsNullOrWhiteSpace(s.Id) ? Guid.NewGuid() : Guid.Parse(s.Id))
                 .Map(d => d.IsActive, _ => true)
                 .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                 .Map(d => d.UpdatedAt, s => DateTime.UtcNow)
                 .Ignore(d => d.StoreId);


            TypeAdapterConfig<BarberChairCreateDto, BarberChair>.NewConfig()
                .Map(d => d.Id, s => Guid.NewGuid())
                .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow)
                .Map(d => d.IsAvailable, s => true)
                .Map(d => d.ManuelBarberId, s => s.BarberId)
                .Ignore(d => d.StoreId);

            TypeAdapterConfig<ServiceOfferingCreateDto, ServiceOffering>.NewConfig()
             .Map(d => d.CreatedAt, s => DateTime.UtcNow)
             .Map(d => d.UpdatedAt, s => DateTime.UtcNow)
             .Ignore(d => d.OwnerId);

            TypeAdapterConfig<WorkingHourCreateDto, WorkingHour>.NewConfig()
            .Ignore(d => d.OwnerId)
            .Map(d => d.StartTime, s => ParseHHmm(s.StartTime))
            .Map(d => d.EndTime, s => ParseHHmm(s.EndTime));
            TypeAdapterConfig<CreateImageDto, Image>.NewConfig()
                .Ignore(d => d.Id)
                .Ignore(d=>d.ImageOwnerId)
                .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow);


        }
        static TimeSpan ParseHHmm(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TimeSpan.Zero;
            return TimeSpan.ParseExact(value.Trim(), "hh\\:mm", CultureInfo.InvariantCulture);
        }
    }
}
