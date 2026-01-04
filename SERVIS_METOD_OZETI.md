# HairDresser Backend - Servis ve Metod Özeti

Bu dokümantasyon, HairDresser backend projesindeki tüm servis interface'lerini ve metodlarını kapsamlı bir şekilde özetlemektedir.

## 📋 İçindekiler

1. [Authentication & Authorization Services](#authentication--authorization-services)
2. [User Services](#user-services)
3. [Appointment Services](#appointment-services)
4. [Chat Services](#chat-services)
5. [Barber Store Services](#barber-store-services)
6. [Free Barber Services](#free-barber-services)
7. [Favorite Services](#favorite-services)
8. [Notification Services](#notification-services)
9. [Rating Services](#rating-services)
10. [Image Services](#image-services)
11. [Badge Services](#badge-services)
12. [Supporting Services](#supporting-services)

---

## 🔐 Authentication & Authorization Services

### IAuthService
**Amaç:** Kullanıcı kimlik doğrulama ve yetkilendirme işlemlerini yönetir.

#### Metodlar:
- **VerifyOtpAsync(UserForVerifyDto, string?, string?)**
  - OTP doğrulama ve kullanıcı kaydı/girişi
  - Döner: `IDataResult<AccessToken>`
  
- **LoginWithPassword(UserForVerifyDto, string?, string?)**
  - Şifre ile giriş yapma (register/login modu)
  - Döner: `IDataResult<AccessToken>`
  
- **SendOtpAsync(string, UserType?, OtpPurpose)**
  - OTP gönderme (Register/Login/Reset)
  - Döner: `IResult`
  
- **RefreshAsync(string, string?)**
  - Refresh token ile yeni access token alma
  - Döner: `IDataResult<AccessToken>`
  
- **RevokeAsync(Guid, string, string?)**
  - Refresh token'ı iptal etme
  - Döner: `IResult`

### IRefreshTokenService
**Amaç:** Refresh token oluşturma ve doğrulama işlemleri.

#### Metodlar:
- **CreateNew(int days)**
  - Yeni refresh token oluşturur
  - Döner: `(string Plain, byte[] Hash, byte[] Salt, DateTime Expires, string Fingerprint)`
  
- **Verify(string, byte[], byte[])**
  - Refresh token doğrulama
  - Döner: `bool`
  
- **MakeFingerprint(string)**
  - Token fingerprint oluşturma
  - Döner: `string`

### ITwilioVerifyService
**Amaç:** Twilio ile OTP gönderme ve doğrulama.

#### Metodlar:
- **SendAsync(string e164)**
  - OTP gönderme
  - Döner: `IResult`
  
- **CheckAsync(string e164, string code)**
  - OTP doğrulama
  - Döner: `IResult`

### IOperationClaimService
**Amaç:** Operasyon claim'lerini yönetir.

#### Metodlar:
- **GetAllOperationClaim()**
  - Tüm operasyon claim'lerini getirir
  - Döner: `IDataResult<List<OperationClaim>>`

### IUserOperationClaimService
**Amaç:** Kullanıcı-operasyon claim ilişkilerini yönetir.

#### Metodlar:
- **GetClaimByUserId(Guid userId)**
  - Kullanıcının claim'lerini getirir
  - Döner: `IDataResult<List<UserOperationClaim>>`
  
- **AddUserOperationsClaim(List<UserOperationClaim>)**
  - Kullanıcıya claim ekler
  - Döner: `IDataResult<List<UserOperationClaim>>`

---

## 👤 User Services

### IUserService
**Amaç:** Kullanıcı CRUD işlemleri ve profil yönetimi.

#### Metodlar:
- **GetClaims(User user)**
  - Kullanıcının operasyon claim'lerini getirir
  - Döner: `IDataResult<List<OperationClaim>>`
  
- **Add(User user)**
  - Yeni kullanıcı ekler
  - Döner: `IResult`
  
- **GetByPhone(string phoneNumber)**
  - Telefon numarasına göre kullanıcı getirir
  - Döner: `IDataResult<User>`
  
- **GetByPhoneAll(string phoneNumber)**
  - Aynı telefon numarasına sahip tüm kullanıcıları getirir
  - Döner: `IDataResult<List<User>>`
  
- **GetByCustomerNumber(string customerNumber)**
  - Müşteri numarasına göre kullanıcı getirir
  - Döner: `IDataResult<User>`
  
- **GetById(Guid id)**
  - ID'ye göre kullanıcı getirir
  - Döner: `IDataResult<User>`
  
- **GetByName(string firstName, string lastName)**
  - İsim-soyisim ile kullanıcı getirir
  - Döner: `IDataResult<User>`
  
- **Update(User user)**
  - Kullanıcı bilgilerini günceller
  - Döner: `IResult`
  
- **GetMe(Guid userId)**
  - Kullanıcının profil bilgilerini getirir
  - Döner: `IDataResult<UserProfileDto>`
  
- **UpdateProfile(UpdateUserDto, Guid currentUserId)**
  - Kullanıcı profilini günceller ve yeni token döner
  - Döner: `IDataResult<AccessToken>`

### IUserSummaryService
**Amaç:** Kullanıcı özet bilgilerini (bildirimler için) sağlar.

#### Metodlar:
- **TryGetAsync(Guid userId)**
  - Tek kullanıcı özet bilgisi getirir
  - Döner: `IDataResult<UserNotifyDto?>`
  
- **GetManyAsync(IEnumerable<Guid> userIds)**
  - Birden fazla kullanıcı özet bilgisi getirir
  - Döner: `IDataResult<Dictionary<Guid, UserNotifyDto>>`

---

## 📅 Appointment Services

### IAppointmentService
**Amaç:** Randevu oluşturma, yönetme ve filtreleme işlemleri.

#### Kontrol Metodları:
- **AnyControl(Guid id)**
  - Kullanıcının aktif randevusu var mı kontrol eder
  - Döner: `IDataResult<bool>`
  
- **AnyChairControl(Guid id)**
  - Koltuk için aktif randevu var mı kontrol eder
  - Döner: `IDataResult<bool>`
  
- **AnyStoreControl(Guid id)**
  - Dükkan için aktif randevu var mı kontrol eder
  - Döner: `IDataResult<bool>`
  
- **AnyManuelBarberControl(Guid id)**
  - Manuel barber için aktif randevu var mı kontrol eder
  - Döner: `IDataResult<bool>`

#### Randevu Oluşturma:
- **CreateCustomerToFreeBarberAsync(Guid customerUserId, CreateAppointmentRequestDto req)**
  - Müşteri → Serbest Berber randevusu oluşturur
  - Döner: `IDataResult<Guid>` (appointmentId)
  
- **CreateCustomerToStoreAndFreeBarberControlAsync(Guid customerUserId, CreateAppointmentRequestDto req)**
  - Müşteri → Dükkan + Serbest Berber randevusu oluşturur (3'lü sistem)
  - Döner: `IDataResult<Guid>` (appointmentId)
  
- **CreateFreeBarberToStoreAsync(Guid freeBarberUserId, CreateAppointmentRequestDto req)**
  - Serbest Berber → Dükkan randevusu oluşturur
  - Döner: `IDataResult<Guid>` (appointmentId)
  
- **CreateStoreToFreeBarberAsync(Guid storeOwnerUserId, CreateStoreToFreeBarberRequestDto req)**
  - Dükkan → Serbest Berber randevusu oluşturur
  - Döner: `IDataResult<Guid>` (appointmentId)
  
- **AddStoreToExistingAppointmentAsync(Guid freeBarberUserId, Guid appointmentId, Guid storeId, Guid chairId, DateOnly, TimeSpan, TimeSpan, List<Guid>)**
  - Mevcut randevuya dükkan ekler
  - Döner: `IDataResult<bool>`

#### Randevu Yönetimi:
- **GetAllAppointmentByFilter(Guid currentUserId, AppointmentFilter filter)**
  - Filtrelenmiş randevu listesi getirir
  - Döner: `IDataResult<List<AppointmentGetDto>>`
  
- **GetAvailibity(Guid storeId, DateOnly dateOnly, CancellationToken)**
  - Dükkan için müsaitlik durumunu getirir
  - Döner: `IDataResult<List<ChairSlotDto>>`

#### Karar Verme (3'lü Sistem):
- **StoreDecisionAsync(Guid storeOwnerUserId, Guid appointmentId, bool approve)**
  - Dükkan onay/red kararı verir
  - Döner: `IDataResult<bool>`
  
- **FreeBarberDecisionAsync(Guid freeBarberUserId, Guid appointmentId, bool approve)**
  - Serbest berber onay/red kararı verir
  - Döner: `IDataResult<bool>`
  
- **CustomerDecisionAsync(Guid customerUserId, Guid appointmentId, bool approve)**
  - Müşteri onay/red kararı verir
  - Döner: `IDataResult<bool>`

#### Randevu İşlemleri:
- **CancelAsync(Guid userId, Guid appointmentId)**
  - Randevuyu iptal eder
  - Döner: `IDataResult<bool>`
  
- **CompleteAsync(Guid userId, Guid appointmentId)**
  - Randevuyu tamamlandı olarak işaretler
  - Döner: `IDataResult<bool>`

### IAppointmentNotifyService
**Amaç:** Randevu bildirimlerini yönetir.

#### Metodlar:
- **NotifyAsync(Guid appointmentId, NotificationType, Guid?, object?)**
  - Randevu bildirimi gönderir
  - Döner: `IResult`
  
- **NotifyWithAppointmentAsync(Appointment, NotificationType, Guid?, object?)**
  - Randevu entity'si ile bildirim gönderir
  - Döner: `IResult`
  
- **NotifyToRecipientsAsync(Guid appointmentId, NotificationType, IReadOnlyCollection<Guid>, Guid?, object?)**
  - Belirli alıcılara bildirim gönderir
  - Döner: `IResult`
  
- **NotifyWithAppointmentToRecipientsAsync(Appointment, NotificationType, IReadOnlyCollection<Guid>, Guid?, object?)**
  - Randevu entity'si ile belirli alıcılara bildirim gönderir
  - Döner: `IResult`

---

## 💬 Chat Services

### IChatService
**Amaç:** Mesajlaşma ve thread yönetimi.

#### Mesaj Gönderme:
- **SendMessageAsync(Guid senderUserId, Guid appointmentId, string text)**
  - Randevu thread'ine mesaj gönderir
  - Döner: `IDataResult<ChatMessageDto>`
  
- **SendFavoriteMessageAsync(Guid senderUserId, Guid threadId, string text)**
  - Favori thread'ine mesaj gönderir
  - Döner: `IDataResult<ChatMessageDto>`

#### Thread Yönetimi:
- **GetThreadsAsync(Guid userId)**
  - Kullanıcının tüm thread'lerini getirir
  - Döner: `IDataResult<List<ChatThreadListItemDto>>`
  
- **EnsureFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId)**
  - Favori thread'i oluşturur veya günceller
  - Döner: `IDataResult<Guid>` (threadId)

#### Mesaj Getirme:
- **GetMessagesByThreadAsync(Guid userId, Guid threadId, DateTime?)**
  - Thread'e ait mesajları getirir
  - Döner: `IDataResult<List<ChatMessageItemDto>>`
  
- **GetMessagesAsync(Guid userId, Guid appointmentId, DateTime?)**
  - Randevu thread'ine ait mesajları getirir (geriye dönük uyumluluk)
  - Döner: `IDataResult<List<ChatMessageItemDto>>`

#### Okundu İşaretleme:
- **MarkThreadReadAsync(Guid userId, Guid threadId)**
  - Thread'i okundu olarak işaretler
  - Döner: `IDataResult<bool>`
  
- **MarkThreadReadByAppointmentAsync(Guid userId, Guid appointmentId)**
  - Randevu thread'ini okundu olarak işaretler (geriye dönük uyumluluk)
  - Döner: `IDataResult<bool>`

#### Diğer:
- **GetUnreadTotalAsync(Guid userId)**
  - Toplam okunmamış mesaj sayısını getirir
  - Döner: `IDataResult<int>`
  
- **NotifyTypingAsync(Guid userId, Guid threadId, bool isTyping)**
  - Typing indicator gönderir
  - Döner: `IDataResult<bool>`

#### Real-time Push Metodları:
- **PushAppointmentThreadCreatedAsync(Guid appointmentId)**
  - Randevu thread'i oluşturulduğunda SignalR ile bildirim gönderir
  
- **PushAppointmentThreadUpdatedAsync(Guid appointmentId)**
  - Randevu thread'i güncellendiğinde SignalR ile bildirim gönderir
  
- **PushFavoriteThreadUpdatedAsync(Guid fromUserId, Guid toUserId, Guid threadId)**
  - Favori thread güncellendiğinde SignalR ile bildirim gönderir

---

## 🏪 Barber Store Services

### IBarberStoreService
**Amaç:** Berber dükkanı CRUD işlemleri ve filtreleme.

#### CRUD İşlemleri:
- **Add(BarberStoreCreateDto, Guid currentUserId)**
  - Yeni dükkan oluşturur
  - Döner: `IResult`
  
- **Update(BarberStoreUpdateDto, Guid currentUserId)**
  - Dükkan bilgilerini günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid storeId, Guid currentUserId)**
  - Dükkanı siler
  - Döner: `IResult`

#### Getirme İşlemleri:
- **GetByIdAsync(Guid id)**
  - ID'ye göre dükkan detayını getirir
  - Döner: `IDataResult<BarberStoreDetail>`
  
- **GetByCurrentUserAsync(Guid currentUserId)**
  - Kullanıcının dükkanlarını getirir
  - Döner: `IDataResult<List<BarberStoreMineDto>>`
  
- **GetNearbyStoresAsync(double lat, double lon, double distance)**
  - Yakındaki dükkanları getirir
  - Döner: `IDataResult<List<BarberStoreGetDto>>`
  
- **GetFilteredStoresAsync(FilterRequestDto filter)**
  - Filtrelenmiş dükkan listesi getirir
  - Döner: `IDataResult<List<BarberStoreGetDto>>`
  
- **GetBarberStoreForUsers(Guid storeId)**
  - Kullanıcılar için dükkan bilgisi getirir
  - Döner: `IDataResult<BarberStoreMineDto>`

### IBarberStoreChairService
**Amaç:** Dükkan koltuk yönetimi.

#### Metodlar:
- **AddAsync(BarberChairCreateDto)**
  - Yeni koltuk ekler
  - Döner: `IResult`
  
- **AddRangeAsync(List<BarberChair>)**
  - Birden fazla koltuk ekler
  - Döner: `IResult`
  
- **UpdateAsync(BarberChairUpdateDto)**
  - Koltuk bilgilerini günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid id)**
  - Koltuk siler
  - Döner: `IResult`
  
- **GetAllByStoreAsync(Guid storeId)**
  - Dükkanın tüm koltuklarını getirir
  - Döner: `IDataResult<List<BarberChairDto>>`
  
- **GetById(Guid id)**
  - ID'ye göre koltuk getirir
  - Döner: `IDataResult<BarberChairDto>`
  
- **AttemptBarberControl(Guid id)**
  - Koltuk için barber kontrolü yapar
  - Döner: `IDataResult<bool>`

### IManuelBarberService
**Amaç:** Dükkan çalışanı (manuel barber) yönetimi.

#### Metodlar:
- **AddAsync(ManuelBarberCreateDto)**
  - Yeni manuel barber ekler
  - Döner: `IResult`
  
- **AddRangeAsync(List<ManuelBarberCreateDto>, Guid storeId)**
  - Birden fazla manuel barber ekler
  - Döner: `IResult`
  
- **UpdateAsync(ManuelBarberUpdateDto)**
  - Manuel barber bilgilerini günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid id)**
  - Manuel barber siler
  - Döner: `IResult`
  
- **GetAllByStoreAsync(Guid storeOwnerId)**
  - Dükkanın tüm manuel barberlerini getirir
  - Döner: `IDataResult<List<ManuelBarberDto>>`

### IServiceOfferingService
**Amaç:** Hizmet teklifi (service offering) yönetimi.

#### Metodlar:
- **Add(ServiceOfferingCreateDto, Guid currentUserId)**
  - Yeni hizmet teklifi ekler
  - Döner: `IResult`
  
- **AddRangeAsync(List<ServiceOffering>)**
  - Birden fazla hizmet teklifi ekler
  - Döner: `IResult`
  
- **Update(ServiceOfferingUpdateDto)**
  - Hizmet teklifini günceller
  - Döner: `IResult`
  
- **UpdateRange(List<ServiceOfferingUpdateDto>)**
  - Birden fazla hizmet teklifini günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid Id, Guid currentUserId)**
  - Hizmet teklifini siler
  - Döner: `IResult`
  
- **GetByIdAsync(Guid id)**
  - ID'ye göre hizmet teklifi getirir
  - Döner: `IDataResult<ServiceOfferingGetDto>`
  
- **GetAll()**
  - Tüm hizmet tekliflerini getirir
  - Döner: `IDataResult<List<ServiceOfferingGetDto>>`
  
- **GetServiceOfferingsIdAsync(Guid Id)**
  - Belirli ID'ye ait hizmet tekliflerini getirir
  - Döner: `IDataResult<List<ServiceOfferingGetDto>>`

### IWorkingHourService
**Amaç:** Çalışma saatleri yönetimi.

#### Metodlar:
- **AddAsync(WorkingHourCreateDto)**
  - Yeni çalışma saati ekler
  - Döner: `IResult`
  
- **AddRangeAsync(List<WorkingHour>)**
  - Birden fazla çalışma saati ekler
  - Döner: `IResult`
  
- **UpdateAsync(WorkingHourUpdateDto)**
  - Çalışma saatini günceller
  - Döner: `IResult`
  
- **UpdateRangeAsync(List<WorkingHourUpdateDto>)**
  - Birden fazla çalışma saatini günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid id)**
  - Çalışma saatini siler
  - Döner: `IResult`
  
- **GetByTargetAsync(Guid targetId)**
  - Hedef (store/freebarber) için çalışma saatlerini getirir
  - Döner: `IDataResult<List<WorkingHourDto>>`

---

## ✂️ Free Barber Services

### IFreeBarberService
**Amaç:** Serbest berber paneli yönetimi.

#### CRUD İşlemleri:
- **Add(FreeBarberCreateDto, Guid currentUserId)**
  - Yeni serbest berber paneli oluşturur
  - Döner: `IResult`
  
- **Update(FreeBarberUpdateDto, Guid currentUserId)**
  - Serbest berber panelini günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid panelId)**
  - Serbest berber panelini siler
  - Döner: `IResult`

#### Getirme İşlemleri:
- **GetNearbyFreeBarberAsync(double lat, double lon, double distance)**
  - Yakındaki serbest berberleri getirir
  - Döner: `IDataResult<List<FreeBarberGetDto>>`
  
- **GetFilteredFreeBarbersAsync(FilterRequestDto filter)**
  - Filtrelenmiş serbest berber listesi getirir
  - Döner: `IDataResult<List<FreeBarberGetDto>>`
  
- **GetMyPanel(Guid currentUserId)**
  - Kullanıcının serbest berber panelini getirir
  - Döner: `IDataResult<FreeBarberMinePanelDto>`
  
- **GetMyPanelDetail(Guid panelId)**
  - Panel detayını getirir
  - Döner: `IDataResult<FreeBarberMinePanelDetailDto>`
  
- **GetFreeBarberForUsers(Guid freeBarberId)**
  - Kullanıcılar için serbest berber bilgisi getirir
  - Döner: `IDataResult<FreeBarberMinePanelDto>`

#### Konum Güncelleme:
- **UpdateLocationAsync(Guid id, double lat, double lon)**
  - Serbest berber konumunu günceller
  - Döner: `IResult`

---

## ⭐ Favorite Services

### IFavoriteService
**Amaç:** Favori ekleme/çıkarma ve listeleme.

#### Metodlar:
- **ToggleFavoriteAsync(Guid userId, ToggleFavoriteDto dto)**
  - Favori ekler veya çıkarır
  - Döner: `IDataResult<ToggleFavoriteResponseDto>`
  
- **IsFavoriteAsync(Guid userId, Guid targetId)**
  - Favori durumunu kontrol eder
  - Döner: `IDataResult<bool>`
  
- **GetMyFavoritesAsync(Guid userId)**
  - Kullanıcının favorilerini getirir
  - Döner: `IDataResult<List<FavoriteGetDto>>`
  
- **RemoveFavoriteAsync(Guid userId, Guid targetId)**
  - Favoriyi kaldırır
  - Döner: `IDataResult<bool>`

---

## 🔔 Notification Services

### INotificationService
**Amaç:** Bildirim oluşturma, okundu işaretleme ve payload güncelleme.

#### Metodlar:
- **CreateAndPushAsync(Guid userId, NotificationType, Guid?, string title, object payload, string? body)**
  - Bildirim oluşturur ve SignalR ile gönderir
  - Döner: `IDataResult<Guid>` (notificationId)
  
- **GetUnreadCountAsync(Guid userId)**
  - Okunmamış bildirim sayısını getirir
  - Döner: `IDataResult<int>`
  
- **GetAllNotify(Guid userId)**
  - Kullanıcının tüm bildirimlerini getirir
  - Döner: `IDataResult<List<NotificationDto>>`
  
- **MarkReadAsync(Guid userId, Guid notificationId)**
  - Bildirimi okundu olarak işaretler
  - Döner: `IDataResult<bool>`
  
- **MarkReadByAppointmentIdAsync(Guid userId, Guid appointmentId)**
  - Randevuya ait bildirimleri okundu olarak işaretler
  - Döner: `IDataResult<bool>`
  
- **UpdateNotificationPayloadByAppointmentAsync(Guid appointmentId, AppointmentStatus, DecisionStatus?, DecisionStatus?, DecisionStatus?, DateTime?)**
  - Randevu bildirim payload'larını günceller ve SignalR ile gönderir
  - Döner: `IDataResult<bool>`

---

## ⭐ Rating Services

### IRatingService
**Amaç:** Değerlendirme (rating) ve yorum yönetimi.

#### Metodlar:
- **CreateRatingAsync(Guid userId, CreateRatingDto dto)**
  - Yeni değerlendirme oluşturur
  - Döner: `IDataResult<RatingGetDto>`
  
- **DeleteRatingAsync(Guid userId, Guid ratingId)**
  - Değerlendirmeyi siler
  - Döner: `IDataResult<bool>`
  
- **GetRatingByIdAsync(Guid ratingId)**
  - ID'ye göre değerlendirme getirir
  - Döner: `IDataResult<RatingGetDto>`
  
- **GetRatingsByTargetAsync(Guid targetId)**
  - Hedef için tüm değerlendirmeleri getirir
  - Döner: `IDataResult<List<RatingGetDto>>`
  
- **GetMyRatingForAppointmentAsync(Guid userId, Guid appointmentId, Guid targetId)**
  - Kullanıcının belirli randevu için değerlendirmesini getirir
  - Döner: `IDataResult<RatingGetDto>`

---

## 🖼️ Image Services

### IImageService
**Amaç:** Resim yükleme, güncelleme ve silme işlemleri (Azure Blob Storage).

#### CRUD İşlemleri:
- **AddAsync(CreateImageDto)**
  - Yeni resim kaydı ekler
  - Döner: `IResult`
  
- **AddRangeAsync(List<CreateImageDto>)**
  - Birden fazla resim kaydı ekler
  - Döner: `IResult`
  
- **UpdateAsync(UpdateImageDto)**
  - Resim kaydını günceller
  - Döner: `IResult`
  
- **UpdateRangeAsync(List<UpdateImageDto>)**
  - Birden fazla resim kaydını günceller
  - Döner: `IResult`
  
- **DeleteAsync(Guid id)**
  - Resim kaydını siler
  - Döner: `IResult`

#### Getirme İşlemleri:
- **GetImage(Guid id)**
  - ID'ye göre resim getirir
  - Döner: `IDataResult<ImageGetDto>`
  
- **GetImagesByOwnerAsync(Guid ownerId, ImageOwnerType ownerType)**
  - Sahibe ait tüm resimleri getirir
  - Döner: `IDataResult<List<ImageGetDto>>`

#### Upload İşlemleri:
- **UploadImageAsync(IFormFile file, ImageOwnerType ownerType, Guid ownerId)**
  - Tek resim yükler (Azure Blob Storage)
  - Döner: `IDataResult<string>` (imageUrl)
  
- **UploadImagesAsync(List<IFormFile> files, ImageOwnerType ownerType, Guid ownerId)**
  - Birden fazla resim yükler (Azure Blob Storage)
  - Döner: `IDataResult<List<string>>` (imageUrls)

---

## 🏷️ Badge Services

### IBadgeService
**Amaç:** Badge (bildirim/mesaj) sayılarını yönetir.

#### Metodlar:
- **GetCountsAsync(Guid userId)**
  - Kullanıcının badge sayılarını getirir (bildirim + mesaj)
  - Döner: `IDataResult<BadgeCountDto>`

### IBadgeUpdateService
**Amaç:** Transaction commit sonrası badge update'lerini yönetir.

#### Metodlar:
- **ScheduleBadgeUpdate(Guid userId)**
  - Badge update'i planlar (transaction commit sonrası çalıştırılacak)
  - Döner: `void`
  
- **ProcessScheduledBadgeUpdatesAsync()**
  - Planlanan tüm badge update'lerini çalıştırır
  - Döner: `Task`

---

## 🛠️ Supporting Services

### ICategoryService
**Amaç:** Kategori (hizmet kategorileri) yönetimi.

#### Metodlar:
- **GetAllCategories()**
  - Tüm kategorileri getirir
  - Döner: `IDataResult<List<Category>>`
  
- **GetParentCategories()**
  - Ana kategorileri getirir
  - Döner: `IDataResult<List<Category>>`
  
- **GetChildCategories(Guid parentId)**
  - Alt kategorileri getirir
  - Döner: `IDataResult<List<Category>>`
  
- **AddCategory(Category category)**
  - Yeni kategori ekler
  - Döner: `IResult`
  
- **DeleteCategory(Guid id)**
  - Kategori siler
  - Döner: `IResult`

### ISlotService
**Amaç:** Haftalık slot (müsaitlik) yönetimi.

#### Metodlar:
- **GetWeeklySlotsAsync(Guid storeId)**
  - Dükkan için haftalık slot'ları getirir
  - Döner: `IDataResult<List<WeeklySlotDto>>`

### IRealTimePublisher
**Amaç:** SignalR ile real-time bildirim gönderme.

#### Metodlar:
- **PushNotificationAsync(Guid userId, NotificationDto dto)**
  - Bildirim gönderir
  - Döner: `Task`
  
- **PushChatMessageAsync(Guid userId, ChatMessageDto dto)**
  - Chat mesajı gönderir
  - Döner: `Task`
  
- **PushBadgeAsync(Guid userId, BadgeCountDto dto)**
  - Badge güncellemesi gönderir
  - Döner: `Task`
  
- **PushChatThreadCreatedAsync(Guid userId, ChatThreadListItemDto dto)**
  - Thread oluşturuldu bildirimi gönderir
  - Döner: `Task`
  
- **PushChatThreadUpdatedAsync(Guid userId, ChatThreadListItemDto dto)**
  - Thread güncellendi bildirimi gönderir
  - Döner: `Task`
  
- **PushChatThreadRemovedAsync(Guid userId, Guid threadId)**
  - Thread kaldırıldı bildirimi gönderir
  - Döner: `Task`
  
- **PushChatTypingAsync(Guid userId, Guid threadId, Guid typingUserId, string typingUserName, bool isTyping)**
  - Typing indicator gönderir
  - Döner: `Task`
  
- **PushAppointmentUpdatedAsync(Guid userId, AppointmentGetDto appointment)**
  - Randevu güncellendi bildirimi gönderir
  - Döner: `Task`

---

## 📊 Özet İstatistikler

- **Toplam Servis Sayısı:** 24
- **Toplam Metod Sayısı:** ~150+
- **Ana Kategoriler:**
  - Authentication & Authorization: 5 servis
  - User Management: 2 servis
  - Appointment Management: 2 servis
  - Chat & Messaging: 1 servis
  - Store Management: 4 servis
  - Free Barber Management: 1 servis
  - Supporting Services: 9 servis

---

## 🔄 İş Akışı Örnekleri

### Randevu Oluşturma Akışı:
1. `IAppointmentService.CreateCustomerToFreeBarberAsync()` → Randevu oluşturulur
2. `IAppointmentNotifyService.NotifyAsync()` → Bildirimler gönderilir
3. `IChatService.EnsureFavoriteThreadAsync()` → Chat thread oluşturulur
4. `IRealTimePublisher.PushNotificationAsync()` → Real-time bildirim gönderilir

### Favori Ekleme Akışı:
1. `IFavoriteService.ToggleFavoriteAsync()` → Favori eklenir/çıkarılır
2. `IChatService.EnsureFavoriteThreadAsync()` → Chat thread oluşturulur/güncellenir
3. `IRealTimePublisher.PushChatThreadCreatedAsync()` → Real-time thread bildirimi gönderilir

---

**Son Güncelleme:** 2025-01-XX
**Versiyon:** 1.0

