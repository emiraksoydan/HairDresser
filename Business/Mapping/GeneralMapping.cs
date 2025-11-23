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

            TypeAdapterConfig<BarberStoreUpdateDto, BarberStore>
               .NewConfig()
               .Map(d => d.UpdatedAt, s => DateTime.UtcNow);

            TypeAdapterConfig<ManuelBarberCreateDto, ManuelBarber>
                .NewConfig()
                 .Map(d => d.IsActive, _ => true)
                 .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                 .Map(d => d.UpdatedAt, s => DateTime.UtcNow);
               

            TypeAdapterConfig<ManuelBarberUpdateDto, ManuelBarber>.NewConfig()
             .Map(dest => dest.FullName, src => src.FullName.Trim())
             .Map(dest => dest.UpdatedAt, _ => DateTime.UtcNow);


            TypeAdapterConfig<BarberChairCreateDto, BarberChair>.NewConfig()
                .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow)
                .Map(d => d.IsAvailable, s => true)
                .Map(d => d.ManuelBarberId, s => s.BarberId);

            TypeAdapterConfig<BarberChairUpdateDto, BarberChair>.NewConfig()
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow)
                .Map(d => d.IsAvailable, s => true)
                .Map(d => d.ManuelBarberId, s => s.BarberId);

            TypeAdapterConfig<ServiceOfferingCreateDto, ServiceOffering>.NewConfig()
             .Map(d => d.CreatedAt, s => DateTime.UtcNow)
             .Map(d => d.UpdatedAt, s => DateTime.UtcNow);

            TypeAdapterConfig<ServiceOfferingUpdateDto, ServiceOffering>.NewConfig()
             .Map(d => d.UpdatedAt, s => DateTime.UtcNow);

            TypeAdapterConfig<WorkingHourCreateDto, WorkingHour>.NewConfig()
            .Map(d => d.StartTime, s => ParseHHmm(s.StartTime))
            .Map(d => d.EndTime, s => ParseHHmm(s.EndTime));

            TypeAdapterConfig<WorkingHourUpdateDto, WorkingHour>.NewConfig()
                .Map(d => d.StartTime, s => ParseHHmm(s.StartTime))
                .Map(d => d.EndTime, s => ParseHHmm(s.EndTime));
                

            TypeAdapterConfig<CreateImageDto, Image>.NewConfig()
                .Map(d => d.CreatedAt, s => DateTime.UtcNow)
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow);

            TypeAdapterConfig<UpdateImageDto, Image>.NewConfig()
                .Map(d => d.UpdatedAt, s => DateTime.UtcNow);
                

            TypeAdapterConfig<Image, ImageGetDto>.NewConfig();



        }
        static TimeSpan ParseHHmm(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TimeSpan.Zero;
            return TimeSpan.ParseExact(value.Trim(), "hh\\:mm", CultureInfo.InvariantCulture);
        }
    }
}
