# Yeni Sistem Tasarım Dokümanı

## Genel Bakış

Bu doküman, mesaj/appointment/favoriler/badge sisteminin baştan tasarımını içerir. Tüm anlık güncellemeler SignalR ile sağlanır.

---

## 1. SignalR Event Yapısı

### Event'ler:
- `badge.updated` - Badge count güncellemesi (unreadNotifications, unreadMessages)
- `notification.received` - Yeni bildirim geldi
- `chat.message` - Yeni mesaj geldi
- `chat.threadCreated` - Yeni thread oluşturuldu
- `chat.threadUpdated` - Thread güncellendi
- `chat.threadRemoved` - Thread kaldırıldı
- `chat.typing` - Yazma göstergesi
- `appointment.updated` - Randevu güncellendi

### Group Yapısı:
- Her kullanıcı: `user:{userId}` group'una eklenir
- Tüm event'ler bu group üzerinden gönderilir

---

## 2. Badge Count Sistemi

### Mantık:
- **UnreadNotifications**: `Notification` tablosunda `UserId = userId AND IsRead = false` count
- **UnreadMessages**: `ChatThread` tablosunda kullanıcıya ait unread count toplamı
  - CustomerUnreadCount (eğer CustomerUserId = userId)
  - StoreUnreadCount (eğer StoreOwnerUserId = userId)
  - FreeBarberUnreadCount (eğer FreeBarberUserId = userId)

### Badge Count Güncelleme Trigger'ları:
1. **Notification oluşturulduğunda** → UnreadNotifications++
2. **Notification okunduğunda** → UnreadNotifications--
3. **Mesaj gönderildiğinde** → UnreadMessages++ (alıcı için, eğer thread açık değilse)
4. **Thread okunduğunda** → UnreadMessages = 0 (o thread için)
5. **Thread kaldırıldığında** → UnreadMessages = 0 (o thread için)

### BadgeUpdateService:
- `ScheduleBadgeUpdate(userId)` - Badge update'i schedule et
- `ProcessScheduledBadgeUpdatesAsync()` - Transaction commit sonrası tüm scheduled update'leri çalıştır

---

## 3. Bildirim Sistemi (Notification)

### Bildirim Tipleri:

#### Aksiyon Bildirimleri (Otomatik Okunur):
- `AppointmentCreated` - Randevu oluşturuldu (kabul/red butonları var)
- Bu bildirimler karar verildiğinde otomatik okunur

#### Geri Dönüş Bildirimleri (Manuel Okunur):
- `AppointmentApproved` - Randevu onaylandı
- `AppointmentRejected` - Randevu reddedildi
- `AppointmentCancelled` - Randevu iptal edildi
- `AppointmentCompleted` - Randevu tamamlandı
- `AppointmentUnanswered` - Randevu cevapsız kaldı
- `FreeBarberRejectedInitial` - FreeBarber ilk isteği reddetti
- `StoreRejectedSelection` - Store seçimi reddetti
- `StoreApprovedSelection` - Store onayladı
- `StoreSelectionTimeout` - Store cevap vermedi
- `CustomerRejectedFinal` - Müşteri final red verdi
- `CustomerApprovedFinal` - Müşteri final onay verdi
- `CustomerFinalTimeout` - Müşteri cevap vermedi

### Bildirim Akışı:

#### Randevu Oluşturulduğunda:
1. Randevu Pending durumuna düşer
2. İlgili kullanıcılara `AppointmentCreated` bildirimi gönderilir
3. Badge count güncellenir (UnreadNotifications++)
4. Thread oluşturulur (eğer yoksa)

#### Karar Verildiğinde:
1. Karar veren kullanıcının `AppointmentCreated` bildirimi otomatik okunur
2. İlgili kullanıcılara geri dönüş bildirimi gönderilir (Approved/Rejected)
3. Badge count güncellenir

#### Cevapsız Durumda:
1. 5 dakika (veya 30 dakika) geçtiğinde `AppointmentUnanswered` bildirimi gönderilir
2. Badge count güncellenir

---

## 4. Mesaj Sistemi (Chat)

### Thread Tipleri:

#### Randevu Thread'leri:
- `AppointmentId` dolu
- Sadece `Pending` veya `Approved` durumunda görünür
- Durum değiştiğinde thread kaldırılır

#### Favori Thread'leri:
- `AppointmentId` null
- `FavoriteFromUserId` ve `FavoriteToUserId` dolu
- En az 1 favori aktifse görünür
- Her iki kullanıcı da favoriyi kaldırırsa thread kaldırılır

### Mesaj Gönderme Kuralları:

#### Randevu Thread'leri:
- Sadece `Pending` veya `Approved` durumunda mesaj gönderilebilir
- İlk mesaj gönderildiğinde thread oluşturulur

#### Favori Thread'leri:
- En az 1 favori aktifse mesaj gönderilebilir
- İlk mesaj gönderildiğinde thread oluşturulur

### Unread Count Yönetimi:

#### Mesaj Gönderildiğinde:
1. Mesaj kaydedilir
2. Alıcı için unread count artırılır (eğer thread açık değilse)
3. Badge count güncellenir (UnreadMessages++)
4. SignalR ile `chat.message` event'i gönderilir

#### Thread Açıldığında:
1. Thread okundu işaretlenir (unread count = 0)
2. Badge count güncellenir (UnreadMessages -= previousUnreadCount)
3. SignalR ile `chat.threadUpdated` event'i gönderilir

#### Thread Kaldırıldığında:
1. Thread unread count'ları sıfırlanır
2. Badge count güncellenir
3. SignalR ile `chat.threadRemoved` event'i gönderilir

---

## 5. Randevu Senaryoları

### Senaryo 1: Müşteri → Dükkan
- Pending süresi: 5 dakika
- Thread: Pending/Approved'da görünür
- Badge: Her iki tarafta da güncellenir
- Bildirimler: AppointmentCreated → Approved/Rejected/Unanswered

### Senaryo 2: FreeBarber → Dükkan
- Pending süresi: 5 dakika
- Thread: Pending/Approved'da görünür
- Badge: Her iki tarafta da güncellenir
- Bildirimler: AppointmentCreated → Approved/Rejected/Unanswered

### Senaryo 3: Dükkan → FreeBarber
- Pending süresi: 5 dakika
- Tarih/Saat: Yok
- Thread: Pending/Approved'da görünür
- Badge: Her iki tarafta da güncellenir
- Bildirimler: AppointmentCreated → Approved/Rejected/Unanswered

### Senaryo 4: Müşteri → FreeBarber
#### Normal:
- Pending süresi: 5 dakika
- Tarih/Saat: Yok
- Thread: Pending/Approved'da görünür
- Badge: Her iki tarafta da güncellenir
- Bildirimler: AppointmentCreated → Approved/Rejected/Unanswered

#### Dükkan Seçili (StoreSelection):
- Pending süresi: 30 dakika
- FreeBarber: Sadece reddetme (30 dk boyunca)
- Dükkan: 5 dakika onay/red
- Müşteri: Final onay/red
- Thread: Pending/Approved'da görünür
- Badge: 3 tarafta da güncellenir
- Bildirimler: AppointmentCreated → StoreApprovedSelection/StoreRejectedSelection → CustomerApprovedFinal/CustomerRejectedFinal

---

## 6. Thread Görünürlük Kuralları

### Randevu Thread'leri:
- Görünür: `Status = Pending OR Status = Approved`
- Görünmez: Diğer durumlar
- Thread kaldırıldığında: Unread count'lar sıfırlanır, badge count güncellenir

### Favori Thread'leri:
- Görünür: En az 1 favori aktif (`IsActive = true`)
- Görünmez: Her iki kullanıcı da favoriyi kaldırdı
- Thread kaldırıldığında: Unread count'lar sıfırlanır, badge count güncellenir

---

## 7. Transaction Yönetimi

### TransactionScopeAspect:
- Transaction commit sonrası `BadgeUpdateService.ProcessScheduledBadgeUpdatesAsync()` çağrılır
- Background task olarak çalışır (100ms delay + retry mekanizması)

### BadgeUpdateService:
- Singleton service
- `ScheduleBadgeUpdate(userId)` ile userId'ler toplanır
- `ProcessScheduledBadgeUpdatesAsync()` ile tüm userId'ler için badge count hesaplanır ve SignalR ile gönderilir

---

## 8. Frontend Entegrasyonu

### SignalR Hook:
- `badge.updated` event'i dinlenir
- RTK Query cache'i güncellenir
- UI anlık olarak güncellenir

### Badge Count Gösterimi:
- UnreadNotifications + UnreadMessages
- Her tab'da (Customer/Store/FreeBarber) gösterilir
- Anlık güncellenir

### Notification Handling:
- Yeni bildirim geldiğinde: Badge count artar, bildirim listesine eklenir
- Bildirim okunduğunda: Badge count azalır
- Karar verildiğinde: Bildirim otomatik okunur

### Chat Handling:
- Yeni mesaj geldiğinde: Thread listesi güncellenir, badge count artar
- Thread açıldığında: Thread okundu işaretlenir, badge count azalır
- Thread kaldırıldığında: Thread listesinden kaldırılır, badge count azalır

---

## 9. Performans Optimizasyonları

1. **Badge Count**: Database-level count/sum (GetAll().Count yerine)
2. **Thread Listesi**: Sadece görünür thread'ler çekilir
3. **Paralel İşleme**: BadgeUpdateService'te paralel badge update'ler
4. **Caching**: RTK Query cache kullanımı
5. **Batch Updates**: Transaction commit sonrası batch badge update'ler

---

## 10. Hata Yönetimi

1. **SignalR Bağlantı Hatası**: Otomatik yeniden bağlanma
2. **Badge Update Hatası**: Sessizce devam et (kritik değil)
3. **Transaction Hatası**: Rollback, badge update yapılmaz
4. **Notification Hatası**: Log, ama işlem devam eder

---

## Sonraki Adımlar

1. ✅ Tasarım dokümanı oluşturuldu
2. ⏭️ BadgeService'i baştan yaz
3. ⏭️ RealTimePublisher'ı baştan yaz
4. ⏭️ NotificationService'i baştan yaz
5. ⏭️ ChatService'i baştan yaz
6. ⏭️ AppointmentManager entegrasyonu
7. ⏭️ TransactionScopeAspect optimizasyonu
8. ⏭️ Frontend SignalR hook
9. ⏭️ Frontend badge count gösterimi
10. ⏭️ Test ve doğrulama
