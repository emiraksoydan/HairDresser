# 3'lü Randevu Sistemi - Detaylı Tasarım Dokümanı

## 1. Genel Akış

### 1.1. Müşteri → FreeBarber (Dükkan Seç)
```
Müşteri → FreeBarber (istek gönder)
  ↓ (30 dakika toplam süre başlar)
FreeBarber → Red veya Dükkan Seç
  ├─ Red → Müşteri'ye bildirim (randevu reddedildi)
  └─ Dükkan Seç → Store'a istek (5 dakika süre başlar)
      ├─ Store Red → FreeBarber'e bildirim, slot boş, FreeBarber tekrar seçebilir
      ├─ Store Timeout (5dk) → FreeBarber'e bildirim, slot boş, FreeBarber tekrar seçebilir
      └─ Store Onay → Müşteri'ye bildirim (5 dakika kaldı, toplam 30dk içinde)
          ├─ Müşteri Red → FreeBarber+Store'a bildirim, FreeBarber tekrar seçebilir, slot boş
          ├─ Müşteri Timeout → Herkes'e bildirim (randevu iptal)
          └─ Müşteri Onay → Randevu Approved, herkes'e bildirim
```

### 1.2. Zaman Aşımı Detayları

| Aşama | Süre | Başlangıç | Timeout Sonrası |
|-------|------|-----------|-----------------|
| FreeBarber İlk Red | 30 dakika | Müşteri istek gönderdiğinde | Randevu iptal, FreeBarber müsait |
| Store Onay | 5 dakika | FreeBarber dükkan seçtiğinde | Store NoAnswer, FreeBarber tekrar seçebilir, slot boş |
| Müşteri Son Onay | Kalan süre (toplam 30dk) | Store onayladığında | Randevu iptal, herkes'e bildirim |

### 1.3. Önemli Notlar

1. **30 dakikalık toplam süre**: Müşteri istek gönderdiğinde başlar, hiç sıfırlanmaz
2. **FreeBarber red hakkı**: Sadece 30 dakika dolmadan reddedebilir, sonra buton pasif
3. **Store onay süresi**: 5 dakika, toplam 30 dakikalık süreye dahil
4. **FreeBarber meşguliyet**: Müşteri'den pending istek varken de dükkan alabilmeli (şu an alamıyor - düzeltilecek)
5. **Slot rezervasyon**: FreeBarber dükkan seçtiğinde slot kitlenir, red/timeout'ta boşalır

## 2. Appointment Entity Değişiklikleri

### 2.1. Yeni Alanlar (Zaten var)
```csharp
public DateTime? FreeBarberDecisionDeadline { get; set; } // FreeBarber'ın red edebileceği son zaman (CreatedAt + 30dk)
public DateTime? StoreDecisionDeadline { get; set; } // Store'un onaylayabileceği son zaman (FreeBarber seçtiğinde + 5dk)
public DateTime? CustomerFinalDecisionDeadline { get; set; } // Müşteri'nin final onay verebileceği son zaman (toplam 30dk - store süresi)
```

### 2.2. Mevcut Alanlar Kullanımı
```csharp
PendingExpiresAt // Toplam 30 dakikalık süre için (değişmez)
StoreDecision // Store'un kararı (Pending, Approved, Rejected, NoAnswer)
FreeBarberDecision // FreeBarber'ın kararı (Pending, Approved, Rejected, NoAnswer)
CustomerDecision // Müşteri'nin kararı (Pending, Approved, Rejected, NoAnswer)
```

## 3. Notification Types

### 3.1. Yeni Bildirim Tipleri
```csharp
public enum NotificationType
{
    // Mevcut
    AppointmentCreated,       
    AppointmentApproved,
    AppointmentRejected,
    AppointmentCancelled,
    AppointmentCompleted,
    AppointmentUnanswered,
    AppointmentDecisionUpdated,
    
    // Yeni eklenecekler
    FreeBarberRejectedInitial,      // FreeBarber ilk isteği reddetti (Müşteri'ye)
    StoreRejectedSelection,          // Store seçimi reddetti (FreeBarber+Müşteri'ye)
    StoreApprovedSelection,          // Store onayladı (FreeBarber+Müşteri'ye)
    StoreSelectionTimeout,           // Store 5dk cevap vermedi (FreeBarber+Müşteri'ye)
    CustomerRejectedFinal,           // Müşteri final red verdi (FreeBarber+Store'a)
    CustomerApprovedFinal,           // Müşteri final onay verdi (FreeBarber+Store'a)
    CustomerFinalTimeout,            // Müşteri 30dk içinde cevap vermedi (Herkes'e)
}
```

## 4. Backend Metodlar

### 4.1. FreeBarber Reject
```csharp
[TransactionScopeAspect]
public async Task<IDataResult<bool>> RejectByFreeBarberAsync(Guid freeBarberUserId, Guid appointmentId)
{
    // 1. Appointment kontrolü
    // 2. FreeBarber'ın bu randevunun sahibi olduğunu kontrol et
    // 3. 30 dakikalık süre dolmadığını kontrol et
    // 4. FreeBarberDecision = Rejected
    // 5. Status = Rejected
    // 6. FreeBarber'ı müsait yap (IsAvailable = true)
    // 7. Müşteri'ye bildirim: FreeBarberRejectedInitial
    // 8. Thread'i pasif yap veya sil
}
```

### 4.2. FreeBarber Select Store
```csharp
[TransactionScopeAspect]
public async Task<IDataResult<bool>> SelectStoreForAppointmentAsync(
    Guid freeBarberUserId, 
    Guid appointmentId, 
    Guid storeId, 
    Guid chairId,
    DateOnly appointmentDate,
    TimeSpan startTime,
    TimeSpan endTime)
{
    // 1. Appointment kontrolü (Status = Pending, StoreSelectionType = StoreSelection)
    // 2. FreeBarber'ın bu randevunun sahibi olduğunu kontrol et
    // 3. 30 dakikalık toplam süre dolmadığını kontrol et
    // 4. Store ve Chair kontrolü
    // 5. Slot müsaitlik kontrolü
    // 6. Slot'u rezerve et (Appointment'a ekle)
    // 7. BarberStoreUserId, ChairId, AppointmentDate, StartTime, EndTime set et
    // 8. StoreDecision = Pending
    // 9. PendingExpiresAt = şimdi + 5 dakika (Store onay süresi)
    // 10. Thread'e Store'u ekle (StoreOwnerUserId)
    // 11. Store'a bildirim: StoreSelectionPending
    // 12. SignalR: Thread güncelleme, slot güncelleme
}
```

### 4.3. Customer Final Decision
```csharp
[TransactionScopeAspect]
public async Task<IDataResult<bool>> GiveFinalDecisionByCustomerAsync(
    Guid customerUserId, 
    Guid appointmentId, 
    bool approve)
{
    // 1. Appointment kontrolü
    // 2. Müşteri'nin bu randevunun sahibi olduğunu kontrol et
    // 3. StoreDecision = Approved olmalı (Store onaylamış olmalı)
    // 4. 30 dakikalık toplam süre dolmadığını kontrol et
    // 5. CustomerDecision = approve ? Approved : Rejected
    
    // Onay:
    // - Status = Approved
    // - ApprovedAt = şimdi
    // - PendingExpiresAt = null
    // - FreeBarber+Store'a bildirim: CustomerApprovedFinal
    
    // Red:
    // - CustomerDecision = Rejected
    // - Slot'u boşalt (ClearStoreSelectionSlot)
    // - StoreDecision = Pending'e döndür
    // - Thread'den Store'u çıkar
    // - FreeBarber tekrar dükkan seçebilir
    // - FreeBarber+Store'a bildirim: CustomerRejectedFinal
}
```

## 5. AppointmentTimeoutWorker Güncellemeleri

### 5.1. Store Onay Timeout (5dk)
```csharp
// Store seçildi ama 5 dakika içinde cevap vermedi
if (appt.StoreSelectionType == StoreSelectionType.StoreSelection &&
    appt.BarberStoreUserId.HasValue &&
    appt.StoreDecision == DecisionStatus.Pending &&
    appt.PendingExpiresAt <= now)
{
    var overallExpiresAt = appt.CreatedAt.AddMinutes(30);
    if (now < overallExpiresAt)
    {
        // Store timeout
        appt.StoreDecision = DecisionStatus.NoAnswer;
        ClearStoreSelectionSlot(appt);
        appt.PendingExpiresAt = overallExpiresAt; // 30dk toplam süreye geri dön
        
        // Thread'den store'u çıkar
        // Notification: StoreSelectionTimeout
        // FreeBarber tekrar seçebilir
    }
    else
    {
        // 30dk toplam süre de doldu
        appt.Status = AppointmentStatus.Unanswered;
        // Notification: CustomerFinalTimeout
    }
}
```

### 5.2. Müşteri Final Onay Timeout (30dk toplam)
```csharp
// Store onayladı ama müşteri 30dk toplam süre dolmadan cevap vermedi
if (appt.StoreSelectionType == StoreSelectionType.StoreSelection &&
    appt.StoreDecision == DecisionStatus.Approved &&
    appt.CustomerDecision == DecisionStatus.Pending &&
    appt.PendingExpiresAt <= now)
{
    // 30dk toplam süre doldu
    appt.Status = AppointmentStatus.Unanswered;
    appt.CustomerDecision = DecisionStatus.NoAnswer;
    ClearStoreSelectionSlot(appt);
    
    // FreeBarber'ı müsait yap
    // Thread'i pasif yap
    // Notification: CustomerFinalTimeout (Herkes'e)
}
```

## 6. Business Rules Güncellemeleri

### 6.1. FreeBarber Meşguliyet Kontrolü
```csharp
// ❌ ESKİ (Yanlış): FreeBarber pending randevusu varken dükkan alamıyor
public async Task<IResult> CheckFreeBarberAvailable(Guid freeBarberUserId)
{
    var fb = await _freeBarberDal.Get(x => x.FreeBarberUserId == freeBarberUserId);
    if (fb == null) return new ErrorResult(Messages.FreeBarberNotFound);
    
    // IsAvailable kontrolü yeterli - pending randevusu olsa bile dükkan alabilmeli
    if (!fb.IsAvailable) return new ErrorResult(Messages.FreeBarberNotAvailable);
    
    return new SuccessResult();
}

// ✅ YENİ: CheckActiveAppointmentRules güncellenecek
// Customer -> FreeBarber (Dükkan Seç) randevusu varken:
// - FreeBarber başka müşteriden istek alamaz (meşgul)
// - FAKAT FreeBarber kendi panel'inden dükkan randevusu alabilir
```

### 6.2. Mesaj Gönderme Kontrolü
```csharp
public async Task<IResult> CheckCanSendMessage(Guid fromUserId, Guid toUserId, Guid? appointmentId)
{
    // 1. Appointment bazlı: Pending veya Approved randevu varsa mesaj gönderilebilir
    if (appointmentId.HasValue)
    {
        var appt = await _appointmentDal.Get(x => x.Id == appointmentId.Value);
        if (appt != null && (appt.Status == AppointmentStatus.Pending || appt.Status == AppointmentStatus.Approved))
            return new SuccessResult();
    }
    
    // 2. Favori bazlı: En az 1 aktif favori varsa mesaj gönderilebilir
    var hasFavorite = await _favoriteDal.AnyAsync(x => 
        ((x.FavoritedFromId == fromUserId && x.FavoritedToId == toUserId) ||
         (x.FavoritedFromId == toUserId && x.FavoritedToId == fromUserId)) &&
        x.IsActive);
    
    if (hasFavorite) return new SuccessResult();
    
    return new ErrorResult("Mesaj göndermek için aktif randevu veya favori gereklidir.");
}
```

## 7. Thread Görünürlük Mantığı

### 7.1. Thread Katılımcı Değişimleri
```csharp
// Müşteri -> FreeBarber istek gönderdiğinde:
// - CustomerUserId, FreeBarberUserId set edilir
// - StoreOwnerUserId = null

// FreeBarber dükkan seçtiğinde:
// - StoreOwnerUserId set edilir
// - Thread anlık görünür hale gelir (3 taraf)

// Store red/timeout:
// - StoreOwnerUserId = null
// - Thread'de sadece Customer+FreeBarber

// Randevu Approved:
// - Thread'de 3 taraf da var (Customer+FreeBarber+Store)

// Randevu iptal/red/timeout:
// - Thread pasif (görünmez)
```

### 7.2. Frontend Thread Listesi
```typescript
// Thread görünür ise:
// - Status = Pending && (CustomerUserId+FreeBarberUserId) → 2 taraf görür
// - Status = Pending && (CustomerUserId+FreeBarberUserId+StoreOwnerUserId) → 3 taraf görür
// - Status = Approved → 3 taraf görür
// - Status = Cancelled/Rejected/Unanswered/Completed → Görünmez
```

## 8. Slot Rezervasyon Mantığı

### 8.1. Slot Dolu/Boş Kontrolü
```csharp
// FreeBarber dükkan seçtiğinde:
// - ChairId + AppointmentDate + StartTime + EndTime slot'u kitlenir
// - Diğer kullanıcılar bu slot'u görmez (dolu)

// Store red ederse:
// - ClearStoreSelectionSlot() → Slot boşalır

// Store timeout (5dk):
// - ClearStoreSelectionSlot() → Slot boşalır

// Müşteri red ederse:
// - ClearStoreSelectionSlot() → Slot boşalır

// Randevu Approved:
// - Slot kilitli kalır

// Randevu Cancelled:
// - Slot boşalır

// Randevu Completed:
// - Slot boşalır
```

## 9. Frontend Değişiklikler

### 9.1. Notification Detail Screen
```typescript
// FreeBarber için:
// - "Dükkan Ekle" butonu YOK
// - Sadece "Reddet" butonu (30dk dolmadıysa)
// - Reddet butonu pasif (30dk dolduysa veya decision verildi ise)

// Store için:
// - "Onayla" / "Reddet" butonları (5dk dolmadıysa)
// - Butonlar pasif (5dk dolduysa veya decision verildi ise)

// Müşteri Final Onay için:
// - "Onayla" / "Reddet" butonları (30dk dolmadıysa ve Store onayladıysa)
// - Butonlar pasif (30dk dolduysa veya decision verildi ise)
```

### 9.2. API Endpoints
```typescript
// FreeBarber
POST /api/appointment/{appointmentId}/reject-by-freebarber

// FreeBarber Store Seçimi
POST /api/appointment/{appointmentId}/select-store
Body: { storeId, chairId, appointmentDate, startTime, endTime }

// Müşteri Final Onay
POST /api/appointment/{appointmentId}/customer-final-decision
Body: { approve: boolean }
```

## 10. Uygulama Senaryoları

### Senaryo 1: Başarılı Akış
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → Store seçer (5dk) ✅
3. Store → Onaylar (1dk içinde) ✅
4. Müşteri → Onaylar (10dk içinde) ✅
5. Randevu Approved 🎉

### Senaryo 2: FreeBarber Red
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → Reddeder (5dk içinde) ❌
3. Müşteri'ye bildirim: "Berber randevu talebinizi reddetti"
4. FreeBarber müsait hale gelir

### Senaryo 3: Store Red
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → Store seçer ✅
3. Store → Reddeder ❌
4. Slot boşalır, FreeBarber tekrar seçebilir
5. FreeBarber → Yeni store seçer ✅
6. (Döngü devam eder)

### Senaryo 4: Store Timeout
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → Store seçer ✅
3. Store → 5dk cevap vermez ⏰
4. Worker: StoreDecision = NoAnswer
5. Slot boşalır, FreeBarber tekrar seçebilir
6. (Senaryo 3 gibi devam eder)

### Senaryo 5: Müşteri Red
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → Store seçer ✅
3. Store → Onaylar ✅
4. Müşteri → Reddeder ❌
5. Slot boşalır, FreeBarber tekrar seçebilir
6. (Senaryo 3 gibi devam eder)

### Senaryo 6: Müşteri Timeout
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → Store seçer (20dk geçti) ✅
3. Store → Onaylar (22dk geçti) ✅
4. Müşteri → 30dk doldu ⏰
5. Worker: Status = Unanswered
6. Herkes'e bildirim: "Randevu cevaplanmadı"

### Senaryo 7: FreeBarber Timeout (30dk)
1. Müşteri → FreeBarber (Dükkan Seç) ✅
2. FreeBarber → 30dk hiç cevap vermez ⏰
3. Worker: Status = Unanswered
4. FreeBarber müsait hale gelir
5. Müşteri'ye bildirim: "Randevu cevaplanmadı"

## 11. İmplementasyon Öncelikleri

1. ✅ Backend Entity ve Enum kontrolü (tamamlandı)
2. 🔄 Backend: RejectByFreeBarberAsync
3. 🔄 Backend: SelectStoreForAppointmentAsync
4. 🔄 Backend: GiveFinalDecisionByCustomerAsync
5. 🔄 Backend: AppointmentTimeoutWorker güncellemeleri
6. 🔄 Backend: Business Rules güncellemeleri
7. 🔄 Backend: Thread görünürlük mantığı
8. 🔄 Backend: Notification types ekleme
9. 🔄 Frontend: API endpoint'leri
10. 🔄 Frontend: Notification detail screen güncellemeleri
11. 🔄 Test: Tüm senaryolar
