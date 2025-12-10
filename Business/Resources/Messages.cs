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
        public const string StoreNotFoundEnglish = "Store not found";
        public const string StoreNotFoundOrNotOwner = "Store not found or not owner";
        public const string StoreNotOpen = "Dükkan bu saat aralığında açık değil";
        public const string StoreClosed = "Dükkan bu gün kapalı (tatil)";
        public const string StoreNoWorkingHours = "Dükkan bu gün için çalışma saati tanımlamamış (kapalı)";
        public const string StoreCreatedSuccess = "Berber dükkanı başarıyla oluşturuldu.";
        public const string StoreUpdatedSuccess = "Berber dükkanı başarıyla güncellendi.";

        // Chair Messages
        public const string ChairNotFound = "Koltuk bulunamadı";
        public const string ChairNotInStore = "Koltuk dükkanda bulunamadı";
        public const string ChairRequired = "ChairId is required";

        // FreeBarber Messages
        public const string FreeBarberNotFound = "Serbest berber bulunamadı";
        public const string FreeBarberNotAvailable = "Serbest berber şu an müsait değil";
        public const string FreeBarberInvalidCoordinates = "Serbest berber koordinatları geçersiz";
        public const string FreeBarberDistanceExceeded = "Serbest berber 1 km dışında. Yakın değilken randevu oluşturamazsın.";
        public const string FreeBarberStoreDistanceExceeded = "Serbest berber ile dükkan arası 1 km dışında. Bu eşleşmeyle randevu açılamaz.";
        public const string StoreFreeBarberDistanceExceeded = "Dükkan ile serbest berber arası 1 km dışında. Bu eşleşmeyle randevu açılamaz.";
        public const string FreeBarberUserIdRequired = "FreeBarberUserId is required";
        public const string FreeBarberUpdateUnauthorized = "Bu serbest berberi güncelleme yetkiniz yok";

        // Customer Messages
        public const string CustomerHasActiveAppointment = "Müşterinin aktif (Pending/Approved) randevusu var.";
        public const string CustomerDistanceExceeded = "Dükkan 1 km dışında. Yakın değilken randevu oluşturamazsın.";

        // Store Messages (continued)
        public const string StoreHasActiveCall = "Dükkanın aktif bir serbest berber çağrısı var. Önce onu sonuçlandır.";
        public const string FreeBarberHasActiveAppointment = "Serbest berberin aktif (Pending/Approved) randevusu var.";
        public const string FreeBarberHasActiveAppointmentUpdate = "Randevu işleminiz bulunmaktadır. Lütfen işlemden sonra güncelleyiniz";

        // Validation Messages
        public const string InvalidDate = "Geçersiz tarih";
        public const string InvalidTime = "Geçersiz saat";
        public const string StartTimeGreaterThanEndTime = "Başlangıç saati bitişten büyük/eşit olamaz.";
        public const string StartTimeEndTimeRequired = "StartTime/EndTime is required";
        public const string LocationRequired = "Konum bilgisi gerekli (RequestLatitude/RequestLongitude).";
        public const string ServiceOfferingRequired = "En az bir hizmet seçilmelidir";
        public const string AppointmentEndTimeCalculationFailed = "Randevu bitiş zamanı hesaplanamadı.";

        // Chat Messages
        public const string ChatOnlyForActiveAppointments = "Chat is only allowed for Pending/Approved appointments";
        public const string EmptyMessage = "Empty message";
        public const string ChatThreadNotFound = "Chat thread bulunamadı";
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
        public const string LocationNotSet = "konumu ayarlı değil";
        public const string LocationInvalid = "konumu geçersiz";
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
    }
}

