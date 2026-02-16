using Autofac;
using Autofac.Extras.DynamicProxy;
using Business.Abstract;
using Business.Concrete;

using Business.Helpers;
using Business.Mapping;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;


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
            // V2 Refactored Services - Improved performance and real-time sync
            builder.RegisterType<NotificationManagerV2>().As<INotificationService>().InstancePerLifetimeScope();
            builder.RegisterType<ChatManager>().As<IChatService>().InstancePerLifetimeScope();
            builder.RegisterType<AppointmentNotifyManager>().As<IAppointmentNotifyService>().InstancePerLifetimeScope();
            
            // New Helper Services
            builder.RegisterType<BadgeService>().InstancePerLifetimeScope();
            builder.RegisterType<UserSummaryManager>().As<IUserSummaryService>().InstancePerLifetimeScope();
            builder.RegisterType<RatingManager>().As<IRatingService>().InstancePerLifetimeScope();
            builder.RegisterType<FavoriteManager>().As<IFavoriteService>().InstancePerLifetimeScope();
            builder.RegisterType<SettingManager>().As<ISettingService>().InstancePerLifetimeScope();
            builder.RegisterType<HelpGuideManager>().As<IHelpGuideService>().InstancePerLifetimeScope();
            builder.RegisterType<FirebasePushNotificationService>().As<IPushNotificationService>().InstancePerLifetimeScope();

            // Complaint, Request, Blocked Services
            builder.RegisterType<ComplaintManager>().As<IComplaintService>().InstancePerLifetimeScope();
            builder.RegisterType<RequestManager>().As<IRequestService>().InstancePerLifetimeScope();
            builder.RegisterType<BlockedManager>().As<IBlockedService>().InstancePerLifetimeScope();

            // Content Moderation Service (OpenAI)
            builder.RegisterType<ContentModerationManager>().As<IContentModerationService>().InstancePerLifetimeScope();

            // Message Encryption Service (AES-256)
            builder.RegisterType<MessageEncryptionService>().As<IMessageEncryptionService>().SingleInstance();

            // Helper classes (N+1 query optimization)
            builder.RegisterType<FavoriteHelper>().InstancePerLifetimeScope();
            builder.RegisterType<AppointmentBusinessRules>().InstancePerLifetimeScope();
            builder.RegisterType<BlockedHelper>().InstancePerLifetimeScope();

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
            builder.RegisterType<NetGsmSmsManager>().As<ISmsVerifyService>().InstancePerLifetimeScope();
            builder.RegisterType<RefreshTokenService>().As<IRefreshTokenService>().InstancePerLifetimeScope();
            builder.RegisterType<EfRefreshTokenDal>().As<IRefreshTokenDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfImageDal>().As<IImageDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserOperationClaimDal>().As<IUserOperationClaimDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfOperationClaimDal>().As<IOperationClaimDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserDal>().As<IUserDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfChatThreadDal>().As<IChatThreadDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfChatMessageDal>().As<IChatMessageDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfMessageReadReceiptDal>().As<IMessageReadReceiptDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfRatingDal>().As<IRatingDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfFavoriteDal>().As<IFavoriteDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfSettingDal>().As<ISettingDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfHelpGuideDal>().As<IHelpGuideDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserFcmTokenDal>().As<IUserFcmTokenDal>().InstancePerLifetimeScope();

            // Complaint, Request, Blocked DAL
            builder.RegisterType<EfComplaintDal>().As<IComplaintDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfRequestDal>().As<IRequestDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfBlockedDal>().As<IBlockedDal>().InstancePerLifetimeScope();

            // IHttpContextAccessor CoreModule'de ServiceCollection'a kayıtlı
            // Autofac.Extensions.DependencyInjection ile otomatik olarak Autofac'e aktarılıyor
            // Burada tekrar kaydetmeye gerek yok

            // Yerel disk dosya depolama
            builder.RegisterType<LocalFileStorageService>().As<IBlobStorageService>().SingleInstance();

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
