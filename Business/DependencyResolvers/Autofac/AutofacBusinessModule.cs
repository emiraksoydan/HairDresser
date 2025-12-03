using Autofac;
using Autofac.Extras.DynamicProxy;
using Business.Abstract;
using Business.Concrete;
using Business.Mapping;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
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
            builder.RegisterType<AuthManager>().As<IAuthService>().InstancePerLifetimeScope();
            builder.RegisterType<JwtHelper>().As<ITokenHelper>();
            builder.RegisterType<Mapper>().As<IMapper>();
            builder.RegisterType<BarberStoreManager>().As<IBarberStoreService>().InstancePerLifetimeScope();
            builder.RegisterType<FreeBarberManager>().As<IFreeBarberService>().InstancePerLifetimeScope();
            builder.RegisterType<ManuelBarberManager>().As<IManuelBarberService>().InstancePerLifetimeScope();
            builder.RegisterType<AppointmentManager>().As<IAppointmentService>().InstancePerLifetimeScope();
            builder.RegisterType<CategoryManager>().As<ICategoryService>().InstancePerLifetimeScope();
            builder.RegisterType<ServiceOfferingManager>().As<IServiceOfferingService>().InstancePerLifetimeScope();
            builder.RegisterType<BarberStoreChairManager>().As<IBarberStoreChairService>().InstancePerLifetimeScope();
            builder.RegisterType<SlotManager>().As<ISlotService>().InstancePerLifetimeScope();
            builder.RegisterType<WorkingHourManager>().As<IWorkingHourService>().InstancePerLifetimeScope();
            builder.RegisterType<UserOperationClaimManager>().As<IUserOperationClaimService>().InstancePerLifetimeScope();
            builder.RegisterType<OperationClaimManager>().As<IOperationClaimService>().InstancePerLifetimeScope();
            builder.RegisterType<PhoneService>().As<IPhoneService>().InstancePerLifetimeScope();
            builder.RegisterType<ImageManager>().As<IImageService>().InstancePerLifetimeScope();
            builder.RegisterType<NotificationManager>().As<INotificationService>().InstancePerLifetimeScope();
            builder.RegisterType<BadgeManager>().As<IBadgeService>().InstancePerLifetimeScope();
            builder.RegisterType<ChatManager>().As<IChatService>().InstancePerLifetimeScope();


            builder.RegisterType<EfBarberStoreDal>().As<IBarberStoreDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfFreeBarberDal>().As<IFreeBarberDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfCategoriesDal>().As<ICategoriesDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfBarberStoreChairDal>().As<IBarberStoreChairDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfWorkingHourDal>().As<IWorkingHourDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfServiceOfferingDal>().As<IServiceOfferingDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfManuelBarberDal>().As<IManuelBarberDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfAppointmentDal>().As<IAppointmentDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfNotificationDal>().As<INotificationDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfAppointmentServiceOfferingDal>().As<IAppointmentServiceOffering>().InstancePerLifetimeScope();
            builder.RegisterType<TwilioVerifyManager>().As<ITwilioVerifyService>().InstancePerLifetimeScope();
            builder.RegisterType<RefreshTokenService>().As<IRefreshTokenService>().InstancePerLifetimeScope();
            builder.RegisterType<EfRefreshTokenDal>().As<IRefreshTokenDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfImageDal>().As<IImageDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserOperationClaimDal>().As<IUserOperationClaimDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfOperationClaimDal>().As<IOperationClaimDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserDal>().As<IUserDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfChatThreadDal>().As<IChatThreadDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfChatMessageDal>().As<IChatMessageDal>().InstancePerLifetimeScope();


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
