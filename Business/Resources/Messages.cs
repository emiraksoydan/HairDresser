namespace Business.Resources
{
    /// <summary>
    /// Centralized error and success messages
    /// Avoids hardcoded strings throughout the codebase
    /// </summary>
    public static class Messages
    {
        // Appointment Messages
        public const string AppointmentNotFound = "Randevu bulunamadÄ±";
        public const string AppointmentExpired = "Randevu sÃ¼resi dolmuÅŸ";
        public const string AppointmentAlreadyCompleted = "Randevu zaten tamamlanmÄ±ÅŸ";
        public const string AppointmentAlreadyCancelled = "Randevu zaten iptal edilmiÅŸ";
        public const string AppointmentCannotBeCancelled = "Ä°ptal edilemez";
        public const string AppointmentTimeNotPassed = "Randevu sÃ¼resi dolmadan tamamlanamaz";
        public const string AppointmentNotApproved = "Kabul edilmemiÅŸ randevu";
        public const string AppointmentNotPending = "Beklemede deÄŸil";
        public const string AppointmentNotPendingStatus = "Bekleme yok";
        public const string AppointmentDecisionAlreadyGiven = "Karar zaten verilmiÅŸ";
        public const string AppointmentSlotTaken = "Bu randevu zamanÄ± baÅŸka bir kullanÄ±cÄ± tarafÄ±ndan alÄ±ndÄ±. LÃ¼tfen baÅŸka bir saat seÃ§in.";
        public const string AppointmentSlotOverlap = "Bu koltuk iÃ§in seÃ§ilen saat aralÄ±ÄŸÄ±nda baÅŸka bir randevu var.";
        public const string AppointmentPastDate = "GeÃ§miÅŸ tarih iÃ§in randevu alÄ±namaz.";
        public const string AppointmentPastTime = "GeÃ§miÅŸ saat iÃ§in randevu alÄ±namaz.";
        public const string AppointmentTimeoutExpired = "Randevu sÃ¼resi dolmuÅŸ (yanÄ±tlanmadÄ±).";
        public const string AppointmentCreatedSuccess = "Randevu baÅŸarÄ±yla oluÅŸturuldu.";
        public const string AppointmentApprovedSuccess = "Randevu onaylandÄ±.";
        public const string AppointmentRejectedSuccess = "Randevu reddedildi.";
        public const string AppointmentCancelledSuccess = "Randevu iptal edildi.";
        public const string AppointmentCompletedSuccess = "Randevu tamamlandÄ±.";

        // Authorization Messages
        public const string Unauthorized = "Yetki yok";
        public const string UnauthorizedOperation = "Ä°ÅŸleme yetkiniz bulunmamaktadÄ±r";
        public const string NotAParticipant = "Bu randevuya katÄ±lÄ±mcÄ± deÄŸilsiniz";

        // Store Messages
        public const string StoreNotFound = "DÃ¼kkan bulunamadÄ±";
        public const string StoreNotFoundEnglish = "Store not found";
        public const string StoreNotFoundOrNotOwner = "Store not found or not owner";
        public const string StoreNotOpen = "DÃ¼kkan bu saat aralÄ±ÄŸÄ±nda aÃ§Ä±k deÄŸil";
        public const string StoreClosed = "DÃ¼kkan bu gÃ¼n kapalÄ± (tatil)";
        public const string StoreNoWorkingHours = "DÃ¼kkan bu gÃ¼n iÃ§in Ã§alÄ±ÅŸma saati tanÄ±mlamamÄ±ÅŸ (kapalÄ±)";
        public const string StoreCreatedSuccess = "Berber dÃ¼kkanÄ± baÅŸarÄ±yla oluÅŸturuldu.";
        public const string StoreUpdatedSuccess = "Berber dÃ¼kkanÄ± baÅŸarÄ±yla gÃ¼ncellendi.";

        // Chair Messages
        public const string ChairNotFound = "Koltuk bulunamadÄ±";
        public const string ChairNotInStore = "Koltuk dÃ¼kkanda bulunamadÄ±";
        public const string ChairRequired = "Koltuk seçimi gereklidir.";

        // FreeBarber Messages
        public const string FreeBarberNotFound = "Serbest berber bulunamadı";
        public const string FreeBarberNotAvailable = "Serbest berber şu an müsait değil";
        public const string FreeBarberInvalidCoordinates = "Serbest berber koordinatlarÄ± geÃ§ersiz";
        public const string FreeBarberDistanceExceeded = "Serbest berber 1 km dÄ±ÅŸÄ±nda. YakÄ±n deÄŸilken randevu oluÅŸturamazsÄ±n.";
        public const string FreeBarberStoreDistanceExceeded = "Serbest berber ile dÃ¼kkan arasÄ± 1 km dÄ±ÅŸÄ±nda. Bu eÅŸleÅŸmeyle randevu aÃ§Ä±lamaz.";
        public const string StoreFreeBarberDistanceExceeded = "DÃ¼kkan ile serbest berber arasÄ± 1 km dÄ±ÅŸÄ±nda. Bu eÅŸleÅŸmeyle randevu aÃ§Ä±lamaz.";
        public const string FreeBarberUserIdRequired = "Serbest berber seçimi gereklidir.";
        public const string FreeBarberNotAllowedForStoreAppointment = "Dükkan randevusunda serbest berber seçilemez.";
        public const string FreeBarberUpdateUnauthorized = "Bu serbest berberi gÃ¼ncelleme yetkiniz yok";
        public const string FreeBarberPanelAlreadyExists = "Zaten bir serbest berber paneliniz bulunmaktadÄ±r. Her kullanÄ±cÄ±nÄ±n sadece bir paneli olabilir.";

        // Customer Messages
        public const string CustomerHasActiveAppointment = "MÃ¼ÅŸterinin aktif (Pending/Approved) randevusu var.";
        public const string CustomerDistanceExceeded = "DÃ¼kkan 1 km dÄ±ÅŸÄ±nda. YakÄ±n deÄŸilken randevu oluÅŸturamazsÄ±n.";

        // Store Messages (continued)
        public const string StoreHasActiveCall = "DÃ¼kkanÄ±n aktif bir serbest berber Ã§aÄŸrÄ±sÄ± var. Ã–nce onu sonuÃ§landÄ±r.";
        public const string FreeBarberHasActiveAppointment = "Serbest berberin aktif (Pending/Approved) randevusu var.";
        public const string FreeBarberHasActiveAppointmentUpdate = "Randevu iÅŸleminiz bulunmaktadÄ±r. LÃ¼tfen iÅŸlemden sonra gÃ¼ncelleyiniz";

        // Validation Messages
        public const string InvalidDate = "GeÃ§ersiz tarih";
        public const string InvalidTime = "GeÃ§ersiz saat";
        public const string StartTimeGreaterThanEndTime = "BaÅŸlangÄ±Ã§ saati bitiÅŸten bÃ¼yÃ¼k/eÅŸit olamaz.";
        public const string StartTimeEndTimeRequired = "Başlangıç ve bitiş saati gereklidir.";
        public const string LocationRequired = "Konum bilgisi gerekli (RequestLatitude/RequestLongitude).";
        public const string ServiceOfferingRequired = "En az bir hizmet seÃ§ilmelidir";
        public const string ServiceOfferingOwnerMismatch = "Seçilen hizmetler bu kullanıcıya ait değil.";
        public const string AppointmentEndTimeCalculationFailed = "Randevu bitiÅŸ zamanÄ± hesaplanamadÄ±.";

        // Chat Messages
        public const string ChatOnlyForActiveAppointments = "Chat is only allowed for Pending/Approved appointments";
        public const string EmptyMessage = "Empty message";
        public const string ChatThreadNotFound = "Chat thread bulunamadÄ±";
        public const string ChatNotFound = "Sohbet bulunamadÄ±";
        public const string ParticipantNotFound = "KatÄ±lÄ±mcÄ± bulunamadÄ±";

        // ManuelBarber Messages
        public const string ManuelBarberNotFound = "Berber bulunamadÄ±";
        public const string ManuelBarberHasActiveAppointments = "Bu berberinize ait beklemekte olan veya aktif olan randevu iÅŸlemi vardÄ±r.";
        public const string ManuelBarberAddedSuccess = "Manuel berber eklendi.";
        public const string ManuelBarberUpdatedSuccess = "Manuel berber gÃ¼ncellendi.";
        public const string ManuelBarberDeletedSuccess = "Manuel berber silindi.";

        // General Messages
        public const string OperationSuccess = "Ä°ÅŸlem baÅŸarÄ±lÄ±";
        public const string OperationFailed = "Ä°ÅŸlem baÅŸarÄ±sÄ±z";
        public const string EntityNotFound = "KayÄ±t bulunamadÄ±";
        public const string StoreHasActiveAppointments = "Bu dÃ¼kkana ait aktif veya bekleyen randevu var Ã¶nce mÃ¼sait olmalÄ±sÄ±nÄ±z ";
        public const string BarberAssignedToMultipleChairs = "Bir berber birden fazla koltuÄŸa atanamaz.";
        public const string BarberAssignedToChair = "Bu berberiniz bir koltuÄŸa atanmÄ±ÅŸ. Ã–nce koltuk ayarÄ±nÄ± deÄŸiÅŸtiriniz.";
        
        
        // Additional Notification Messages
        public const string AppointmentCreatedNotification = "Randevun oluÅŸturuldu";
        public const string AppointmentApprovedNotification = "Randevu onaylandÄ±";
        public const string AppointmentRejectedNotification = "Randevu reddedildi";
        public const string AppointmentCancelledNotification = "Randevu iptal edildi";
        public const string AppointmentCompletedNotification = "Randevu tamamlandÄ±";
        public const string AppointmentUnansweredNotification = "Randevu yanÄ±tlanmadÄ±";
        
        // UserOperationClaim Messages
        public const string UserOperationClaimsAddedSuccess = "KullanÄ±cÄ± Yetkileri Eklendi";
        public const string UserOperationClaimsAdded = "KullanÄ±cÄ± Yetkileri Eklendi";
        public const string UserOperationClaimsNotFound = "KullanÄ±cÄ± yetkileri bulunamadÄ±";
        public const string OperationClaimsGetFailed = "Yetkiler getirilemedi";
        
        // Additional Distance/Coordinate Messages
        public const string LocationNotSet = "konumu ayarlÄ± deÄŸil";
        public const string LocationInvalid = "konumu geÃ§ersiz";
        public const string RequestLocationNotSet = "Ä°stek konumu ayarlÄ± deÄŸil";
        public const string TargetLocationNotSet = "Hedef konumu ayarlÄ± deÄŸil";
        public const string FreeBarberLocationNotSet = "Serbest berber konumu ayarlÄ± deÄŸil";
        public const string FreeBarberLocationInvalid = "Serbest berber konumu geÃ§ersiz";
        public const string DistanceExceeded = "Mesafe limiti aÅŸÄ±ldÄ±";
        
        // Additional Chat Thread Title Messages
        public const string ChatThreadTitleCustomer = "MÃ¼ÅŸteri";
        public const string ChatThreadTitleFreeBarber = "Serbest Berber";
        public const string ChatThreadTitleBarberStore = "Berber DÃ¼kkanÄ±";
        
        // Additional Notification Messages
        public const string NotificationDefault = "Bildirim";
        public const string NotificationNewAppointmentRequest = "Yeni randevu isteÄŸi";
        public const string NotificationNewAppointmentRequestForStore = "Yeni randevu talebi";
        
        // Rating Messages
        public const string RatingCreatedSuccess = "DeÄŸerlendirme baÅŸarÄ±yla kaydedildi.";
        public const string RatingUpdatedSuccess = "DeÄŸerlendirme baÅŸarÄ±yla gÃ¼ncellendi.";
        public const string RatingDeletedSuccess = "DeÄŸerlendirme silindi.";
        public const string RatingNotFound = "DeÄŸerlendirme bulunamadÄ±.";
        public const string RatingOnlyForCompleted = "Sadece tamamlanmÄ±ÅŸ veya iptal edilmiÅŸ randevular iÃ§in deÄŸerlendirme yapÄ±labilir.";
        public const string CannotRateYourself = "Kendi kendinize deÄŸerlendirme yapamazsÄ±nÄ±z.";
        public const string InvalidTargetForRating = "GeÃ§ersiz hedef. Sadece Store ID, FreeBarber ID veya Customer UserId ile deÄŸerlendirme yapÄ±labilir. ManuelBarber'a deÄŸerlendirme yapÄ±lamaz.";
        
        // Favorite Messages
        public const string FavoriteAddedSuccess = "Favorilere eklendi.";
        public const string FavoriteUpdatedSuccess = "Favori gÃ¼ncellendi.";
        public const string FavoriteRemovedSuccess = "Favorilerden Ã§Ä±karÄ±ldÄ±.";
        public const string FavoriteNotFound = "Favori bulunamadÄ±.";
        public const string CannotFavoriteYourself = "Kendi kendinizi favorilere ekleyemezsiniz.";
        public const string TargetUserNotFound = "Hedef kullanÄ±cÄ± bulunamadÄ±.";
    }
}


