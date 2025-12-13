# 🔴 KALAN SORUNLAR LİSTESİ

**Son Güncelleme:** 2025-01-XX  
**Durum:** Çözülen sorunlar işaretlendi ✅

---

## ✅ ÇÖZÜLEN SORUNLAR

1. ✅ **AppointmentManager.cs** - SetFreeBarberAvailabilityAsync null check eklendi
2. ✅ **DatabaseContext.cs** - Performans index'leri eklendi (CustomerUserId+Status, FreeBarberUserId+Status, BarberStoreUserId+Status, FreeBarberUserId unique, Rating index)
3. ✅ **AppointmentManager.cs** - Gereksiz exact match kontrolü kaldırıldı

---

## 🚨 YÜKSEK ÖNCELİKLİ SORUNLAR

### 1. **AppointmentNotifyManager.cs - N+1 Query Problemleri**

**Dosya:** `Business/Concrete/AppointmentNotifyManager.cs`  
**Satırlar:** 112, 158, 191, 201

**Sorun:**
```csharp
// Her notification için ayrı sorgu yapılıyor
var storeImages = await imageDal.GetAll(x => x.ImageOwnerId == store.Id && x.OwnerType == ImageOwnerType.Store);
var manuelBarberImages = await imageDal.GetAll(x => x.ImageOwnerId == mb.Id && x.OwnerType == ImageOwnerType.ManuelBarber);
var appointmentServiceOfferings = await appointmentServiceOfferingDal.GetAll(x => x.AppointmentId == appt.Id);
```

**Etki:** Her notification gönderiminde 3+ ekstra sorgu yapılıyor. Çoklu notification gönderimlerinde performans sorunu.

**Çözüm:**
1. `IImageDal`'a `GetLatestImageAsync` metodu ekle
2. Batch query kullan (tüm store ID'leri topla, tek sorguda çek)
3. AppointmentServiceOffering için transaction commit sonrası al veya Include kullan

**Tahmini Süre:** 1-2 saat

---

### 2. **NotificationManager.cs - Optimistic Badge Update Sorunu**

**Dosya:** `Business/Concrete/NotificationManager.cs`  
**Satırlar:** 79-100

**Sorun:**
```csharp
// Transaction içinde badge count'a +1 ekleniyor
// Ancak transaction commit edilmeden önce yapılıyor
badges.Data.UnreadNotifications += 1;
await realtime.PushBadgeAsync(userId, badges.Data);
```

**Etki:** Transaction commit edilmeden önce badge güncelleniyor. Eğer transaction rollback olursa, badge yanlış değerde kalır.

**Çözüm:**
1. Transaction commit sonrası badge'i tekrar hesapla ve push et
2. Veya event-based yaklaşım kullan (transaction commit sonrası event fırlat)

**Tahmini Süre:** 1 saat

---

### 3. **Frontend - API Response Transform Karmaşıklığı**

**Dosya:** `app/store/api.tsx`  
**Satırlar:** 202-243, 248-258, 351-356

**Sorun:**
```typescript
// Backend zaten camelCase dönüyor ama frontend'de hem data hem Data hem direkt array kontrolü yapılıyor
transformResponse: (response: any) => {
    if (Array.isArray(response)) return response;
    if (Array.isArray(response?.data)) return response.data;
    if (Array.isArray(response?.Data)) return response.Data; // Gereksiz
    // ... çok fazla fallback
}
```

**Etki:** Gereksiz kod karmaşıklığı, bakım zorluğu.

**Çözüm:**
1. Backend'den her zaman `{ success, data, message }` formatında dön
2. Frontend'deki PascalCase kontrollerini kaldır
3. Transform'ları basitleştir

**Tahmini Süre:** 30 dakika

---

## 🟡 ORTA ÖNCELİKLİ SORUNLAR

### 4. **Magic Numbers - Configuration'a Taşınmalı**

**Dosyalar:**
- `Business/Concrete/AppointmentManager.cs` (satır 168, 289, 393, 874)
- `Api/BackgroundServices/AppointmentTimeoutWorker.cs` (satır 87)
- `DataAccess/Concrete/EfAppointmentDal.cs` (satır 27)

**Sorun:**
```csharp
// Magic numbers kod içinde hard-coded
PendingExpiresAt = DateTime.UtcNow.AddMinutes(5); // ⚠️
await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // ⚠️
const int slotMinutes = 60; // ⚠️
private const double MaxDistanceKm = 1.0; // ⚠️ (bu constant olarak tanımlı ama config'den okunmalı)
```

**Etki:** Değerleri değiştirmek için kod değişikliği gerekiyor, test zorluğu.

**Çözüm:**
1. `appsettings.json`'a ekle:
```json
{
  "AppointmentSettings": {
    "PendingTimeoutMinutes": 5,
    "MaxDistanceKm": 1.0,
    "SlotMinutes": 60
  },
  "BackgroundServices": {
    "AppointmentTimeoutWorkerIntervalSeconds": 30
  }
}
```

2. Configuration class oluştur ve inject et
3. Magic number'ları config'den oku

**Tahmini Süre:** 1 saat

---

### 5. **GetAll Kullanımı - Get ile Değiştirilebilir**

**Dosyalar:**
- `Business/Concrete/AppointmentNotifyManager.cs` (satır 112, 158)
- `Business/Concrete/NotificationManager.cs` (satır 132, 151, 175)

**Sorun:**
```csharp
// Tek kayıt beklenen yerlerde GetAll kullanılıyor
var storeImages = await imageDal.GetAll(...);
// Sadece en son image lazım, GetAll gereksiz
var firstImage = storeImages.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
```

**Etki:** Gereksiz veri çekiliyor, performans kaybı.

**Çözüm:**
1. `GetLatestImageAsync` gibi özel metod ekle
2. Veya `FirstOrDefaultAsync` kullan

**Tahmini Süre:** 30 dakika

---

### 6. **DatabaseContext.cs - Commented Out Code**

**Dosya:** `DataAccess/Concrete/DatabaseContext.cs`  
**Satırlar:** 16-31

**Sorun:**
```csharp
// Commented out code
//b.Property(u => u.PhoneEncrypted)
//    .IsRequired();
// Bu kodlar kaldırılmalı veya aktif edilmeli
```

**Etki:** Kod karmaşıklığı, karışıklık.

**Çözüm:** Ya aktif et ya da kaldır.

**Tahmini Süre:** 5 dakika

---

## 🟢 DÜŞÜK ÖNCELİKLİ SORUNLAR

### 7. **Error Handling - Exception Logging**

**Sorun:** Bazı yerlerde exception'lar yakalanıyor ama loglanmıyor.

**Örnek:**
```csharp
catch (Exception)
{
    // Log error if logger is available
    // Ancak logger kullanılmıyor
}
```

**Çözüm:** ILogger inject et ve exception'ları logla.

**Tahmini Süre:** 1-2 saat (tüm catch bloklarını güncelle)

---

### 8. **Code Duplication - Appointment Create Metodları**

**Dosya:** `Business/Concrete/AppointmentManager.cs`

**Sorun:**
- `CreateCustomerToStoreAndFreeBarberControlAsync`
- `CreateFreeBarberToStoreAsync`
- `CreateStoreToFreeBarberAsync`

Bu üç metod benzer logic içeriyor ama her birinin kendine özgü validasyonları var.

**Not:** Bu duplication kabul edilebilir çünkü her metodun farklı business rule'ları var. Ancak ortak helper metodlar zaten var (EnsureStoreIsOpenAsync, EnsureChairNoOverlapAsync, vb.)

**Öncelik:** Düşük (kod çalışıyor, sadece refactoring için)

---

### 9. **Frontend - SignalR Hook Karmaşıklığı**

**Dosya:** `app/hook/useSignalR.tsx`

**Sorun:** Çok fazla transform logic var, payload update logic karmaşık.

**Not:** Çalışıyor ama bakımı zor. Refactoring için düşünülebilir.

**Öncelik:** Düşük

---

## 📊 ÖZET TABLO

| # | Sorun | Öncelik | Tahmini Süre | Durum |
|---|-------|---------|--------------|-------|
| 1 | AppointmentNotifyManager N+1 Queries | 🔴 Yüksek | 1-2 saat | ❌ Açık |
| 2 | NotificationManager Badge Update | 🔴 Yüksek | 1 saat | ❌ Açık |
| 3 | Frontend API Transform | 🔴 Yüksek | 30 dk | ❌ Açık |
| 4 | Magic Numbers → Config | 🟡 Orta | 1 saat | ❌ Açık |
| 5 | GetAll → GetLatestImageAsync | 🟡 Orta | 30 dk | ❌ Açık |
| 6 | Commented Out Code | 🟡 Orta | 5 dk | ❌ Açık |
| 7 | Exception Logging | 🟢 Düşük | 1-2 saat | ❌ Açık |
| 8 | Code Duplication | 🟢 Düşük | - | ❌ Açık |
| 9 | SignalR Hook Refactoring | 🟢 Düşük | - | ❌ Açık |

---

## 🎯 ÖNERİLEN ÇALIŞMA SIRASI

1. **Önce Yüksek Öncelikli:**
   - AppointmentNotifyManager N+1 Queries (en kritik performans sorunu)
   - NotificationManager Badge Update (data tutarlılığı)
   - Frontend API Transform (kod temizliği)

2. **Sonra Orta Öncelikli:**
   - Magic Numbers → Config (maintainability)
   - GetAll → GetLatestImageAsync (performans)
   - Commented Out Code (kod temizliği)

3. **Son Olarak Düşük Öncelikli:**
   - Exception Logging
   - Code Duplication (opsiyonel)
   - SignalR Hook Refactoring (opsiyonel)

---

## ⚠️ ÖNEMLİ NOTLAR

1. **Database Migration:** Yeni eklenen index'ler için migration oluştur ve çalıştır:
   ```bash
   dotnet ef migrations add AddPerformanceIndexes
   dotnet ef database update
   ```

2. **Test:** Her düzeltmeden sonra test et:
   - Appointment oluşturma
   - Notification gönderimi
   - Badge güncellemeleri
   - Frontend API çağrıları

3. **Backup:** Production'a geçmeden önce database backup al.

---

**Toplam Tahmini Süre:** 5-7 saat (tüm sorunlar için)

