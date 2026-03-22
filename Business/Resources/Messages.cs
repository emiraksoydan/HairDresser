namespace Business.Resources
{
    /// <summary>
    /// Centralized error and success messages
    /// Avoids hardcoded strings throughout the codebase
    /// </summary>
    public static class Messages
    {
        // Appointment Messages
        public const string AppointmentNotFound = "Randevu bulunamadı";
        public const string AppointmentExpired = "Randevu süresi dolmuş";
        public const string AppointmentAlreadyCompleted = "Randevu zaten tamamlanmış";
        public const string AppointmentAlreadyCancelled = "Randevu zaten iptal edilmiş";
        public const string AppointmentCannotBeCancelled = "İptal edilemez";
        public const string AppointmentTimeNotPassed = "Randevu süresi dolmadan tamamlanamaz";
        public const string AppointmentNotApproved = "Kabul edilmemiş randevu";
        public const string AppointmentNotPending = "Beklemede değil";
        public const string AppointmentNotPendingStatus = "Bekleme yok";
        public const string AppointmentDecisionAlreadyGiven = "Karar zaten verilmiş";
        public const string AppointmentSlotTaken = "Bu randevu zamanı başka bir kullanıcı tarafından alındı. Lütfen başka bir saat seçin.";
        public const string AppointmentSlotOverlap = "Bu koltuk için seçilen saat aralığında başka bir randevu var.";
        public const string AppointmentPastDate = "Geçmiş tarih için randevu alınamaz.";
        public const string AppointmentPastTime = "Geçmiş saat için randevu alınamaz.";
        public const string AppointmentTimeoutExpired = "Randevu süresi dolmuş (yanıtlanmadı).";
        public const string AppointmentCreatedSuccess = "Randevu başarıyla oluşturuldu.";
        public const string AppointmentApprovedSuccess = "Randevu onaylandı.";
        public const string AppointmentRejectedSuccess = "Randevu reddedildi.";
        public const string AppointmentCancelledSuccess = "Randevu iptal edildi.";
        public const string AppointmentCompletedSuccess = "Randevu tamamlandı.";

        // Authorization Messages
        public const string Unauthorized = "Yetki yok";
        public const string UnauthorizedOperation = "İşleme yetkiniz bulunmamaktadır";
        public const string NotAParticipant = "Bu randevuya katılımcı değilsiniz";

        // Store Messages
        public const string StoreNotFound = "Dükkan bulunamadı";
        public const string StoreNotFoundOrNotOwner = "Dükkan bulunamadı veya sahibi değilsiniz";
        public const string StoreNotOpen = "Dükkan bu saat aralığında açık değil";
        public const string StoreClosed = "Dükkan bu gün kapalı (tatil)";
        public const string StoreNoWorkingHours = "Dükkan bu gün için çalışma saati tanımlamamış (kapalı)";
        public const string StoreCreatedSuccess = "Berber dükkanı başarıyla oluşturuldu.";
        public const string StoreUpdatedSuccess = "Berber dükkanı başarıyla güncellendi.";

        // Chair Messages
        public const string ChairNotFound = "Koltuk bulunamadı";
        public const string ChairNotInStore = "Koltuk dükkanda bulunamadı";
        public const string ChairRequired = "Koltuk seçimi gereklidir.";

        // FreeBarber Messages
        public const string FreeBarberNotFound = "Serbest berber bulunamadı";
        public const string FreeBarberNotAvailable = "Serbest berber şu an müsait değil";
        public const string FreeBarberInvalidCoordinates = "Serbest berber koordinatları geçersiz";
        public const string FreeBarberDistanceExceeded = "Serbest berber 1 km dışında. Yakın değilken randevu oluşturamazsın.";
        public const string FreeBarberStoreDistanceExceeded = "Serbest berber ile dükkan arası 1 km dışında. Bu eşleşmeyle randevu açılamaz.";
        public const string StoreFreeBarberDistanceExceeded = "Dükkan ile serbest berber arası 1 km dışında. Bu eşleşmeyle randevu açılamaz.";
        public const string FreeBarberUserIdRequired = "Serbest berber seçimi gereklidir.";
        public const string FreeBarberNotAllowedForStoreAppointment = "Dükkan randevusunda serbest berber seçilemez.";
        public const string FreeBarberUpdateUnauthorized = "Bu serbest berberi güncelleme yetkiniz yok";
        public const string FreeBarberPanelAlreadyExists = "Zaten bir serbest berber paneliniz bulunmaktadır. Her kullanıcının sadece bir paneli olabilir.";
        public const string FreeBarberPanelRequired = "Randevu oluşturmak için önce serbest berber panelinizi oluşturmanız gerekmektedir.";

        // Customer Messages
        public const string CustomerHasActiveAppointment = "Müşterinin aktif (Bekleyen/Onaylanmış) randevusu var.";
        public const string CustomerAlreadyHasActiveAppointment = "Zaten aktif bir randevunuz var. Önce onu tamamlayın.";
        public const string CustomerDistanceExceeded = "Dükkan 1 km dışında. Yakın değilken randevu oluşturamazsın.";

        // Store Messages (continued)
        public const string StoreHasActiveCall = "Dükkanın aktif bir serbest berber çağrısı var. Önce onu sonuçlandır.";
        public const string StoreAlreadyHasActiveAppointment = "Dükkanın zaten aktif bir randevusu var.";
        public const string StoreAlreadyHasActiveAppointmentWithThisFreeBarber = "Bu dükkanınızın bu serbest berber ile aktif bir randevusu var. Önce onu sonuçlandırın.";
        public const string FreeBarberHasActiveAppointment = "Serbest berberin aktif (Bekleyen/Onaylanmış) randevusu var.";
        public const string FreeBarberAlreadyHasActiveAppointment = "Serbest berberin zaten aktif bir randevusu var.";
        public const string FreeBarberHasActiveAppointmentUpdate = "Randevu işleminiz bulunmaktadır. Lütfen işlemden sonra güncelleyiniz";

        // Validation Messages
        public const string InvalidDate = "Geçersiz tarih";
        public const string InvalidTime = "Geçersiz saat";
        public const string AppointmentDateCannotBePast = "Randevu tarihi geçmişte olamaz.";
        public const string StartTimeGreaterThanEndTime = "Başlangıç saati bitişten büyük/eşit olamaz.";
        public const string StartTimeEndTimeRequired = "Başlangıç ve bitiş saati gereklidir.";
        public const string LocationRequired = "Konum bilgisi gerekli (RequestLatitude/RequestLongitude).";
        public const string ServiceOfferingRequired = "En az bir hizmet seçilmelidir";
        public const string ServiceOfferingOwnerMismatch = "Seçilen hizmetler bu kullanıcıya ait değil.";
        public const string AppointmentEndTimeCalculationFailed = "Randevu bitiş zamanı hesaplanamadı.";
        
        // User Messages
        public const string UserNotFound = "Kullanıcı bulunamadı.";
        public const string OnlyCustomersCanCreateAppointment = "Sadece müşteriler randevu oluşturabilir.";
        public const string UserBlockedCannotCreateAppointment = "Engellenen bir kullanıcıdan randevu alamazsınız.";

        // Chat Messages
        public const string ChatOnlyForActiveAppointments = "Sohbet sadece Bekleyen/Onaylanmış randevular için aktiftir.";
        public const string EmptyMessage = "Boş mesaj gönderilemez";
        public const string ChatThreadNotFound = "Sohbet kaydı bulunamadı";
        public const string ChatNotFound = "Sohbet bulunamadı";
        public const string ParticipantNotFound = "Katılımcı bulunamadı";

        // ManuelBarber Messages
        public const string ManuelBarberNotFound = "Berber bulunamadı";
        public const string ManuelBarberHasActiveAppointments = "Bu berberinize ait beklemekte olan veya aktif olan randevu işlemi vardır.";
        public const string ManuelBarberAddedSuccess = "Manuel berber eklendi.";
        public const string ManuelBarberUpdatedSuccess = "Manuel berber güncellendi.";
        public const string ManuelBarberDeletedSuccess = "Manuel berber silindi.";

        // General Messages
        public const string OperationSuccess = "İşlem başarılı";
        public const string OperationFailed = "İşlem başarısız";
        public const string EntityNotFound = "Kayıt bulunamadı";
        public const string StoreHasActiveAppointments = "Bu dükkana ait aktif veya bekleyen randevu var önce müsait olmalısınız ";
        public const string BarberAssignedToMultipleChairs = "Bir berber birden fazla koltuğa atanamaz.";
        public const string BarberAssignedToChair = "Bu berberiniz bir koltuğa atanmış. Önce koltuk ayarını değiştiriniz.";
        
        
        // Additional Notification Messages
        public const string AppointmentCreatedNotification = "Randevun oluşturuldu";
        public const string AppointmentApprovedNotification = "Randevu onaylandı";
        public const string AppointmentRejectedNotification = "Randevu reddedildi";
        public const string AppointmentCancelledNotification = "Randevu iptal edildi";
        public const string AppointmentCompletedNotification = "Randevu tamamlandı";
        public const string AppointmentUnansweredNotification = "Randevu yanıtlanmadı";
        
        // UserOperationClaim Messages
        public const string UserOperationClaimsAddedSuccess = "Kullanıcı Yetkileri Eklendi";
        public const string UserOperationClaimsAdded = "Kullanıcı Yetkileri Eklendi";
        public const string UserOperationClaimsNotFound = "Kullanıcı yetkileri bulunamadı";
        public const string OperationClaimsGetFailed = "Yetkiler getirilemedi";
        
        // Additional Distance/Coordinate Messages
        public const string LocationNotSet = "Konumu ayarlı değil";
        public const string LocationInvalid = "Konumu geçersiz";
        public const string RequestLocationNotSet = "İstek konumu ayarlı değil";
        public const string TargetLocationNotSet = "Hedef konumu ayarlı değil";
        public const string FreeBarberLocationNotSet = "Serbest berber konumu ayarlı değil";
        public const string FreeBarberLocationInvalid = "Serbest berber konumu geçersiz";
        public const string DistanceExceeded = "Mesafe limiti aşıldı";
        
        // Additional Chat Thread Title Messages
        public const string ChatThreadTitleCustomer = "Müşteri";
        public const string ChatThreadTitleFreeBarber = "Serbest Berber";
        public const string ChatThreadTitleBarberStore = "Berber Dükkanı";
        
        // Additional Notification Messages
        public const string NotificationDefault = "Bildirim";
        public const string NotificationNewAppointmentRequest = "Yeni randevu isteği";
        public const string NotificationNewAppointmentRequestForStore = "Yeni randevu talebi";
        
        // Rating Messages
        public const string RatingCreatedSuccess = "Değerlendirme başarıyla kaydedildi.";
        public const string RatingUpdatedSuccess = "Değerlendirme başarıyla güncellendi.";
        public const string RatingDeletedSuccess = "Değerlendirme silindi.";
        public const string RatingNotFound = "Değerlendirme bulunamadı.";
        public const string RatingOnlyForCompleted = "Sadece tamamlanmış veya iptal edilmiş randevular için değerlendirme yapılabilir.";
        public const string CannotRateYourself = "Kendi kendinize değerlendirme yapamazsınız.";
        public const string InvalidTargetForRating = "Geçersiz hedef. Sadece Store ID, FreeBarber ID veya Customer UserId ile değerlendirme yapılabilir. ManuelBarber'a değerlendirme yapılamaz.";
        
        // Favorite Messages
        public const string FavoriteAddedSuccess = "Favorilere eklendi.";
        public const string FavoriteUpdatedSuccess = "Favori güncellendi.";
        public const string FavoriteRemovedSuccess = "Favorilerden çıkarıldı.";
        public const string FavoriteNotFound = "Favori bulunamadı.";
        public const string CannotFavoriteYourself = "Kendi kendinizi favorilere ekleyemezsiniz.";
        public const string TargetUserNotFound = "Hedef kullanıcı bulunamadı.";
        public const string AppointmentMustBeCompletedForFavorite = "Randevu sayfasından favorileme için randevunuzun sonuçlanması gerekir.";
        
        // Appointment Additional Messages
        public const string AppointmentCannotAddStore = "Bu randevuya dükkan eklenemez.";
        public const string FreeBarberApprovalStepNotAvailable = "Bu randevuda serbest berber onay adımı yok. Dükkan seçimi bekleniyor.";
        public const string CannotRejectAfterApproval = "Randevu onaylandı, artık red edemezsiniz.";
        public const string CannotRejectAfterCancellation = "Randevu iptal edildi, artık red edemezsiniz.";
        public const string CannotRejectAfterCompletion = "Randevu tamamlandı, artık red edemezsiniz.";
        public const string RejectionTimeoutExpired = "Reddetme süresi doldu.";
        public const string FreeBarberApprovalPending = "Serbest berber onayı bekleniyor.";
        public const string CustomerDecisionNotAllowed = "Bu randevu için müşteri kararı verilemez.";
        public const string StoreApprovalPending = "Dükkan onayı bekleniyor.";
        public const string CannotDeletePendingOrApproved = "Pending veya Approved durumundaki randevular silinemez";
        public const string AppointmentNotFoundForDelete = "Silinecek randevu bulunamadı.";
        public const string NoAppointmentsDeleted = "Hiçbir randevu silinemedi. {0} adet randevu Pending veya Approved durumunda.";
        
        // Rating Additional Messages
        public const string RatingAlreadyExists = "Bu randevu için bu hedefe zaten değerlendirme yaptınız. Değerlendirme güncellenemez.";
        public const string TargetNotFound = "Hedef bulunamadı.";
        
        // Chat Additional Messages
        public const string MessageRequiresActiveAppointmentOrFavorite = "Mesaj göndermek için randevu aktif olmalı veya karşılıklı favori olmalısınız.";
        public const string MethodOnlyForFavoriteThreads = "Bu metod sadece favori thread'ler için kullanılabilir";
        public const string FavoriteNotActive = "Favori aktif değil, mesaj gönderilemez";
        public const string FavoriteNotActiveForMessages = "Favori aktif değil";
        
        // FreeBarber Additional Messages
        public const string FreeBarberPortalCreatedSuccess = "Serbest berber portalı başarıyla oluşturuldu.";
        public const string FreeBarberUpdatedSuccess = "Serbest berber güncellendi.";
        public const string FreeBarberDeletedSuccess = "Serbest berber silindi.";
        public const string BarberNotFound = "Berber bulunamadı";
        public const string LocationUpdatedSuccess = "Konum başarıyla güncellendi";
        public const string PanelGetFailed = "Panel getirilemedi";
        public const string FilteredFreeBarbersRetrieved = "Filtrelenmiş serbest berberler getirildi";
        public const string PanelDetailGetFailed = "Panel detayı getirilemedi";
        
        // BarberStore Additional Messages
        public const string BarberStoreUpdatedSuccess = "Berber dükkanı başarıyla güncellendi.";
        public const string StoreDeletedSuccess = "Dükkan silindi.";
        public const string NearbyBarbersRetrieved = "1 Kilometreye sınırdaki berberler getirildi";
        public const string FilteredBarberStoresRetrieved = "Filtrelenmiş berber dükkanları getirildi";
        
        // Category Messages
        public const string CategoryAddedSuccess = "Kategori Eklendi";
        public const string CategoryDeletedSuccess = "Kategori Silindi";
        public const string MainCategoriesRetrieved = "Ana kategoriler getirildi";
        public const string SubCategoriesRetrieved = "Alt kategoriler getirildi";
        public const string CategoriesRetrieved = "Kategoriler getirildi";
        
        // Image Messages
        public const string ImageOwnerIdRequired = "Resim sahibi ID'si boş olamaz";
        public const string ImageIdRequired = "Resim ID'si boş olamaz";
        
        // HelpGuide Messages
        public const string InvalidUserType = "Geçersiz kullanıcı tipi.";
        
        // Chat Default Names
        public const string BarberDefaultName = "Berber";
        public const string FreeBarberDefaultName = "Serbest Berber";
        public const string UserDefaultName = "Kullanıcı";
        
        // Additional Appointment Messages
        public const string CannotRejectAfterCustomerApproval = "Müşteri onay verdiği için bu randevu artık reddedilemez.";

        // Ban Messages
        public const string UserBanned = "Hesabınız yönetici tarafından askıya alınmıştır.";
        public const string UserBannedWithReason = "Hesabınız askıya alınmıştır. Sebep: {0}";
        public const string UserBannedSuccess = "Kullanıcı başarıyla engellendi.";
        public const string UserUnbannedSuccess = "Kullanıcı engeli başarıyla kaldırıldı.";

        // Subscription / Trial Messages
        public const string TrialExpired = "Deneme süreniz sona ermiştir. Devam etmek için lütfen abone olunuz.";
        public const string TrialPanelLimitReached = "Deneme süresinde yalnızca 1 panel ekleyebilirsiniz. Birden fazla panel için lütfen abone olunuz.";
        public const string BarberStorePanelAlreadyExists = "Zaten bir berber dükkanı paneliniz bulunmaktadır.";
    }
}

