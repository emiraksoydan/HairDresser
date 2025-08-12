using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.DynamicProxy;
using Business.Abstract;
using Business.Concrete;
using Business.Mapping;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Core.Utilities.Security.JWT;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Mapster;
using MapsterMapper;

namespace Business.DependencyResolvers.Autofac
{
    public class AutofacBusinessModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            
            builder.RegisterType<UserManager>().As<IUserService>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserDal>().As<IUserDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfRefreshTokenDal>().As<IRefreshTokenDal>().InstancePerLifetimeScope();
            builder.RegisterType<AuthManager>().As<IAuthService>().InstancePerLifetimeScope();
            builder.RegisterType<JwtHelper>().As<ITokenHelper>();
            builder.RegisterType<Mapper>().As<IMapper>();
            builder.RegisterType<BarberStoreManager>().As<IBarberStoreService>().InstancePerLifetimeScope();
            builder.RegisterType<FreeBarberManager>().As<IFreeBarberService>().InstancePerLifetimeScope();
            builder.RegisterType<ManuelBarberManager>().As<IManuelBarberService>().InstancePerLifetimeScope();
            builder.RegisterType<CategoryManager>().As<ICategoryService>().InstancePerLifetimeScope();
            builder.RegisterType<ServiceOfferingManager>().As<IServiceOfferingService>().InstancePerLifetimeScope();
            builder.RegisterType<BarberStoreChairManager>().As<IBarberStoreChairService>().InstancePerLifetimeScope();
            builder.RegisterType<SlotManager>().As<ISlotService>().InstancePerLifetimeScope();
            builder.RegisterType<WorkingHourManager>().As<IWorkingHourService>().InstancePerLifetimeScope();
            builder.RegisterType<EfBarberStoreDal>().As<IBarberStoreDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfFreeBarberDal>().As<IFreeBarberDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfCategoriesDal>().As<ICategoriesDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfBarberStoreChairDal>().As<IBarberStoreChairDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfWorkingHourDal>().As<IWorkingHourDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfServiceOfferingDal>().As<IServiceOfferingDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfManuelBarberDal>().As<IManuelBarberDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfAppointmentDal>().As<IAppointmentDal>().InstancePerLifetimeScope();


            TypeAdapterConfig.GlobalSettings.Scan(typeof(GeneralMapping).Assembly);

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces()
                .EnableInterfaceInterceptors(new ProxyGenerationOptions()
                {
                    Selector = new AspectInterceptorSelector()
                }).InstancePerLifetimeScope();
        }
    }
}
