using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;

namespace Business.Mapping
{
    public class GeneralMapping : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<BarberStoreCreateDto, BarberStore>.NewConfig().Ignore(dest => dest.Address.Id);
            TypeAdapterConfig<BarberStoreUpdateDto, BarberStore>.NewConfig();
            config.NewConfig<BarberStore, BarberStoreDetailDto>();
            config.NewConfig<BarberStore, BarberStoreListDto>();

            config.NewConfig<FreeBarberCreateDto, FreeBarber>()
                .Map(dest => dest.FullName, src => $"{src.Name} {src.Surname}")
                .Map(dest => dest.IsAvailable, src => true)
                .Ignore(dest => dest.FreeBarberUser);
            config.NewConfig<FreeBarberUpdateDto, FreeBarber>()
                .Map(dest => dest.FullName, src => $"{src.Name} {src.Surname}")
                .Ignore(dest => dest.FreeBarberUser)
                .Ignore(dest => dest.FreeBarberUserId);
            config.NewConfig<FreeBarber, FreeBarberDetailDto>();
            config.NewConfig<FreeBarber, FreeBarberListDto>();

            config.NewConfig<ServiceOfferingCreateDto, ServiceOffering>().Map(dest => dest.CreatedAt, src => DateTime.Now).Map(dest => dest.UpdatedAt, src => DateTime.Now);
            config.NewConfig<ServiceOfferingUpdateDto, ServiceOffering>().Ignore(dest => dest.OwnerId).Map(dest => dest.UpdatedAt, src => DateTime.Now);
            config.NewConfig<ServiceOffering, ServiceOfferingListDto>();

            config.NewConfig<BarberChairCreateDto, BarberChair>().Map(dest => dest.CreatedAtUtc, src => DateTime.Now).Map(dest => dest.UpdatedAtUtc, src => DateTime.Now).Map(dest => dest.IsActive, src => true);
            config.NewConfig<BarberChairUpdateDto, BarberChair>()
                .Map(dest => dest.UpdatedAtUtc, src => DateTime.Now);
            //config.NewConfig<BarberChair, BarberChairDto>()
            //    .Map(dest => dest.AssignedBarberName, src =>
            //        src.IsInternalEmployee
            //            ? src.AssignedBarberUser != null ? src.AssignedBarberUser.FirstName + " " + src.AssignedBarberUser.LastName : null
            //            : src.ManualBarber != null ? src.ManualBarber.FirstName + " " + src.ManualBarber.LastName : null
            //    );

            config.NewConfig<WorkingHourCreateDto, WorkingHour>().Map(dest => dest.IsClosed, src => false);            
            config.NewConfig<WorkingHourUpdateDto, WorkingHour>();
            config.NewConfig<WorkingHour, WorkingHourDto>();
            config.NewConfig<ManuelBarberCreateDto, ManuelBarber>();
            config.NewConfig<ManuelBarberUpdateDto, ManuelBarber>();
            config.NewConfig<ManuelBarber, ManuelBarberDto>()
                .Map(dest => dest.FullName, src => src.FirstName + " " + src.LastName);
        }
    }
}
