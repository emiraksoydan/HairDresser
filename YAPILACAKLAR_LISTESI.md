# 📋 HairDresser Projeleri - Detaylı Yapılacaklar Listesi

**Oluşturulma Tarihi:** 2025-01-07  
**Son Güncelleme:** 2025-01-07  
**Durum:** Aktif geliştirme

---

## 🎯 Öncelik Sıralaması

- 🔴 **KRİTİK**: Hemen yapılmalı, sistem güvenilirliği ve performansı etkiliyor
- 🟡 **YÜKSEK**: Yakın zamanda yapılmalı, kullanıcı deneyimi ve kod kalitesi için önemli
- 🟢 **ORTA**: Planlanabilir, iyileştirme ve optimizasyon için

---

## 🔴 KRİTİK ÖNCELİKLİ GÖREVLER

### 1. ✅ Background Service Transaction Eksikliği - TAMAMLANDI
**Dosya:** `Api/BackgroundServices/AppointmentTimeoutWorker.cs`  
**Öncelik:** 🔴 KRİTİK  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ Her appointment için transaction eklendi (satır 100-251)
- ✅ Error handling ve rollback mekanizması eklendi
- ✅ ILogger ile error logging eklendi
- ✅ Notification transaction commit sonrası gönderiliyor

**Çözüm:**
```csharp
foreach (var appt in expired)
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    try
    {
        // Appointment status update
        appt.Status = AppointmentStatus.Unanswered;
        // ... diğer değişiklikler
        
        // FreeBarber release
        if (appt.FreeBarberUserId.HasValue)
        {
            var fb = await freeBarberDal.Get(...);
            if (fb != null)
            {
                fb.IsAvailable = true;
                await freeBarberDal.Update(fb);
            }
        }
        
        await db.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync();
        
        // Notification transaction dışında (commit sonrası)
        await notifySvc.NotifyAsync(...);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to process expired appointment {AppointmentId}", appt.Id);
    }
}
```

**Etki:** Data tutarlılığı garantisi, hata durumunda rollback

---

### 2. ✅ N+1 Query Problemleri - TAMAMLANDI
**Dosyalar:** 
- `Business/Concrete/AppointmentNotifyManager.cs`
- `Business/Concrete/ChatManager.cs`

**Öncelik:** 🔴 KRİTİK  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ `GetLatestImageAsync` metodu eklendi ve kullanılıyor
- ✅ ChatManager'da batch query'ler kullanılıyor (appointment, store, user batch'leri)
- ✅ Image query'leri batch olarak yapılıyor

**Çözüm (AppointmentNotifyManager):**
```csharp
// Batch image query
var storeIds = notifications
    .Select(n => n.StoreId)
    .Where(id => id.HasValue)
    .Select(id => id!.Value)
    .Distinct()
    .ToList();

var storeImages = await imageDal.GetAll(x => 
    storeIds.Contains(x.ImageOwnerId) && 
    x.OwnerType == ImageOwnerType.Store);

var storeImageDict = storeImages
    .GroupBy(x => x.ImageOwnerId)
    .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First().ImageUrl);
```

**Çözüm (ChatManager):**
```csharp
var appointmentIds = threads.Select(t => t.AppointmentId).ToList();
var appointments = await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id));
var apptDict = appointments.ToDictionary(a => a.Id);

var storeOwnerIds = appointments
    .Where(a => a.BarberStoreUserId.HasValue)
    .Select(a => a.BarberStoreUserId!.Value)
    .Distinct()
    .ToList();
    
var stores = await barberStoreDal.GetAll(x => storeOwnerIds.Contains(x.BarberStoreOwnerId));
var storeDict = stores.ToDictionary(s => s.BarberStoreOwnerId);
```

**Etki:** Query sayısı N'den 2-3'e düşer, performans artışı

---

### 3. ✅ Badge Count Transaction Timing - TAMAMLANDI
**Dosya:** `Business/Concrete/NotificationManager.cs`  
**Öncelik:** 🔴 KRİTİK  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ Badge count hesaplama `badgeService.GetCountsAsync` ile yapılıyor (database'den direkt hesaplanıyor)
- ✅ Manual +1 ekleme kaldırıldı
- ✅ Badge count güncellemesi notification commit sonrası yapılıyor

**Çözüm:**
```csharp
// Notification transaction commit edildikten SONRA badge count hesapla
// NotificationManager transaction dışında çağrılmalı VEYA
// Badge count hesaplama transaction commit'ten sonra yapılmalı

// Önerilen yaklaşım:
// 1. Notification'ı transaction içinde kaydet
// 2. Transaction commit olsun
// 3. Badge count'u güncel haliyle hesapla ve gönder
```

**Etki:** Data tutarlılığı, race condition koruması

---

## 🟡 YÜKSEK ÖNCELİKLİ GÖREVLER

### 4. ✅ Magic Numbers → Configuration - TAMAMLANDI
**Dosyalar:**
- `Business/Concrete/AppointmentManager.cs`
- `Api/BackgroundServices/AppointmentTimeoutWorker.cs`

**Öncelik:** 🟡 YÜKSEK  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ `appsettings.json`'a `AppointmentSettings` eklendi (PendingTimeoutMinutes, MaxDistanceKm, SlotMinutes)
- ✅ `BackgroundServices` section eklendi (AppointmentTimeoutWorkerIntervalSeconds)
- ✅ Configuration'dan değerler okunuyor

**Çözüm:**
```json
// appsettings.json
{
  "AppointmentSettings": {
    "PendingTimeoutMinutes": 5,
    "MaxDistanceKm": 1.0,
    "SlotMinutes": 60,
    "StoreSelectionTotalMinutes": 30,
    "StoreSelectionStepMinutes": 5
  },
  "BackgroundServices": {
    "AppointmentTimeoutWorkerIntervalSeconds": 30
  }
}
```

**Etki:** Maintainability, test edilebilirlik

---

### 5. ✅ GetAll → GetLatestImageAsync - TAMAMLANDI
**Dosyalar:**
- `Business/Concrete/AppointmentNotifyManager.cs`
- `Business/Concrete/ChatManager.cs`
- `Business/Concrete/RatingManager.cs`

**Öncelik:** 🟡 YÜKSEK  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ `IImageDal` interface'ine `GetLatestImageAsync` metodu eklendi
- ✅ `EfImageDal`'da implementasyon yapıldı
- ✅ Tüm manager'larda `GetAll` yerine `GetLatestImageAsync` kullanılıyor

**Çözüm:**
```csharp
// IImageDal'a ekle:
Task<Image?> GetLatestImageAsync(Guid ownerId, ImageOwnerType ownerType);

// EfImageDal'da implement et:
public async Task<Image?> GetLatestImageAsync(Guid ownerId, ImageOwnerType ownerType)
{
    return await _context.Set<Image>()
        .Where(x => x.ImageOwnerId == ownerId && x.OwnerType == ownerType)
        .OrderByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync();
}
```

**Etki:** Performans artışı, gereksiz veri transferi azalır

---

### 6. ✅ BadgeManager In-Memory Sum - TAMAMLANDI
**Dosya:** `Business/Concrete/BadgeManager.cs`  
**Öncelik:** 🟡 YÜKSEK  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ `IChatThreadDal`'a `GetUnreadMessageCountAsync` metodu eklendi
- ✅ Database'de sum yapılıyor (in-memory sum kaldırıldı)
- ✅ `CountAsync` kullanılıyor (GetAll().Count yerine)

**Çözüm:**
```csharp
// IChatThreadDal'a ekle:
Task<int> GetUnreadMessageCountAsync(Guid userId);

// EfChatThreadDal'da implement et:
public async Task<int> GetUnreadMessageCountAsync(Guid userId)
{
    return await _context.Set<ChatThread>()
        .Where(t => t.CustomerUserId == userId || 
                   t.StoreOwnerUserId == userId || 
                   t.FreeBarberUserId == userId)
        .SumAsync(t =>
            t.CustomerUserId == userId ? t.CustomerUnreadCount :
            t.StoreOwnerUserId == userId ? t.StoreUnreadCount :
            t.FreeBarberUserId == userId ? t.FreeBarberUnreadCount : 0);
}
```

**Etki:** Database'de sum, memory kullanımı azalır

---

### 7. Exception Logging
**Dosyalar:** Tüm catch blokları  
**Öncelik:** 🟡 YÜKSEK  
**Tahmini Süre:** 2-3 saat

**Sorun:**
- Exception'lar yakalanıyor ama loglanmıyor
- Debug zorluğu

**Çözüm:**
```csharp
// Tüm manager'lara ILogger inject et
private readonly ILogger<AppointmentManager> _logger;

// Catch bloklarında:
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process appointment {AppointmentId}", appointmentId);
    // ... error handling
}
```

**Etki:** Debug kolaylığı, production'da hata takibi

---

### 8. Frontend Console.log Temizliği
**Dosyalar:**
- `app/hook/useNotificationSound.tsx`
- `app/components/store/storebooking.tsx`
- `app/hook/useFcmToken.tsx`
- `app/(barberstoretabs)/(panel)/index.tsx`

**Öncelik:** 🟡 YÜKSEK  
**Tahmini Süre:** 30 dakika

**Sorun:**
- Production build'de console.log'lar görünüyor
- Performance overhead

**Çözüm:**
```typescript
// utils/logger.ts oluştur
const isDev = __DEV__;

export const logger = {
  log: (...args: any[]) => isDev && console.log(...args),
  error: (...args: any[]) => isDev && console.error(...args),
  warn: (...args: any[]) => isDev && console.warn(...args),
};

// Tüm console.log'ları logger.log ile değiştir
```

**Etki:** Production build temizliği, performance

---

### 9. Frontend Type Safety
**Dosyalar:**
- `app/store/api.tsx` (satır 530, 533, 538, 560, 562, 589, 630, 635, 645, 730, 735, 740)
- `app/store/baseQuery.tsx` (satır 13, 35, 54, 56, 65)

**Öncelik:** 🟡 YÜKSEK  
**Tahmini Süre:** 2-3 saat

**Sorun:**
- Çok fazla `any` kullanımı
- Type safety eksikliği

**Çözüm:**
```typescript
// Proper types tanımla
type ApiState = {
  api: {
    queries: Record<string, QueryState>;
    mutations: Record<string, MutationState>;
  };
};

// any yerine proper types kullan
const state = getState() as RootState;
const apiState = state.api;
```

**Etki:** Type safety, compile-time error detection

---

### 10. API Transform Basitleştirme
**Dosya:** `app/store/api.tsx`  
**Öncelik:** 🟡 YÜKSEK  
**Tahmini Süre:** 30 dakika

**Sorun:**
- Backend camelCase döndüğü için PascalCase kontrolleri gereksiz
- Karmaşık transform logic

**Çözüm:**
```typescript
// Backend'den her zaman { success, data, message } formatında dön
// Frontend'deki PascalCase kontrollerini kaldır
// Transform'ları basitleştir
```

**Etki:** Kod temizliği, bakım kolaylığı

---

### 11. Rate Limiting
**Dosya:** `Api/Program.cs`  
**Öncelik:** 🟡 YÜKSEK  
**Tahmini Süre:** 1-2 saat

**Sorun:**
- API endpoint'lere rate limiting yok
- Brute force saldırılarına açık

**Çözüm:**
```csharp
// AspNetCoreRateLimit paketi ekle
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options => {
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100
        }
    };
});
```

**Etki:** Güvenlik, DDoS koruması

---

## 🟢 ORTA ÖNCELİKLİ GÖREVLER

### 12. Commented Out Code
**Dosya:** `DataAccess/Concrete/DatabaseContext.cs` (satır 16-31)  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 5 dakika

**Sorun:**
- Yorum satırları kod karmaşıklığı yaratıyor

**Çözüm:**
- Ya aktif et ya da kaldır

---

### 13. ✅ Database Index Optimizasyonları - TAMAMLANDI
**Dosya:** `DataAccess/Concrete/DatabaseContext.cs`  
**Öncelik:** 🟢 ORTA  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ Notification, ChatThread, ChatMessage, Appointment tablolarına index'ler eklendi
- ✅ FreeBarber, Favorite, Rating tablolarına performans index'leri eklendi
- ✅ Composite index'ler ve filtered index'ler kullanılıyor

**Eklenen Index'ler:**
```csharp
// Notification tablosu
modelBuilder.Entity<Notification>()
    .HasIndex(n => new { n.UserId, n.IsRead })
    .HasDatabaseName("IX_Notification_UserId_IsRead");

modelBuilder.Entity<Notification>()
    .HasIndex(n => n.AppointmentId)
    .HasDatabaseName("IX_Notification_AppointmentId");

modelBuilder.Entity<Notification>()
    .HasIndex(n => n.CreatedAt)
    .HasDatabaseName("IX_Notification_CreatedAt");

// ChatThread tablosu
modelBuilder.Entity<ChatThread>()
    .HasIndex(t => t.AppointmentId)
    .HasDatabaseName("IX_ChatThread_AppointmentId");

// ChatMessage tablosu
modelBuilder.Entity<ChatMessage>()
    .HasIndex(m => new { m.ThreadId, m.CreatedAt })
    .HasDatabaseName("IX_ChatMessage_ThreadId_CreatedAt");

// Appointment tablosu
modelBuilder.Entity<Appointment>()
    .HasIndex(a => new { a.Status, a.AppointmentDate })
    .HasDatabaseName("IX_Appointment_Status_Date");
```

**Etki:** Query performansı artışı

---

### 14. ✅ SignalR Error Handling - TAMAMLANDI
**Dosya:** `Api/RealTime/SignalRRealtimePublisher.cs`  
**Öncelik:** 🟢 ORTA  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ Tüm SignalR push metodlarına try-catch eklendi
- ✅ Hatalar yakalanıyor ve exception throw edilmiyor (notification zaten DB'de)

**Çözüm:**
```csharp
public async Task PushNotificationAsync(Guid userId, NotificationDto dto)
{
    try
    {
        await hub.Clients.Group($"user:{userId}").SendAsync("notification.received", dto);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to push notification to user {UserId}", userId);
        // Don't throw - notification is already in DB
    }
}
```

**Etki:** Hata takibi, sistem kararlılığı

---

### 15. ✅ AppHub OnDisconnectedAsync - TAMAMLANDI
**Dosya:** `Api/Hubs/AppHub.cs`  
**Öncelik:** 🟢 ORTA  
**Durum:** ✅ TAMAMLANDI

**Yapılan Düzeltmeler:**
- ✅ `OnDisconnectedAsync` metodu eklendi
- ✅ Disconnected kullanıcılar group'tan kaldırılıyor
- ✅ Memory leak önlendi

**Çözüm:**
```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    var userIdStr = Context?.User?.GetUserIdOrThrow();
    if (Guid.TryParse(userIdStr?.ToString(), out var userId))
    {
        await Groups.RemoveFromGroupAsync(Context?.ConnectionId!, $"user:{userId}");
    }
    await base.OnDisconnectedAsync(exception);
}
```

**Etki:** Memory leak önleme, best practice

---

### 16. SignalR Hook Refactoring
**Dosya:** `app/hook/useSignalR.tsx`  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 2-3 saat

**Sorun:**
- Çok fazla transform logic
- Karmaşık payload update logic

**Çözüm:**
```typescript
// Helper fonksiyonlara ayır
const transformNotificationPayload = (data: any): NotificationDto => {
  // Transform logic
};

const updateBadgeCount = (data: any) => {
  // Badge update logic
};
```

**Etki:** Kod okunabilirliği, bakım kolaylığı

---

### 17. Error Boundary
**Dosya:** Yeni dosya oluştur  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 1-2 saat

**Sorun:**
- Global error handling yok
- Crash'ler yakalanmıyor

**Çözüm:**
```typescript
// components/common/ErrorBoundary.tsx
class ErrorBoundary extends React.Component {
  // Error boundary implementation
  // Crash reporting entegrasyonu (Sentry, etc.)
}
```

**Etki:** Kullanıcı deneyimi, crash reporting

---

### 18. Performance Monitoring
**Dosya:** Tüm component'ler  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 2-3 saat

**Sorun:**
- Slow component'ler tespit edilmiyor

**Çözüm:**
- React DevTools Profiler kullan
- Slow component'leri tespit et
- Memoization ekle (React.memo, useMemo, useCallback)

**Etki:** Performance artışı

---

### 19. CORS Configuration Review
**Dosya:** `Api/Program.cs`  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 30 dakika

**Sorun:**
- Production CORS ayarları kontrol edilmeli

**Çözüm:**
```csharp
// Production'da sadece gerekli origin'leri ekle
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
    ?? Array.Empty<string>();

options.AddDefaultPolicy(policy =>
{
    policy.WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
});
```

**Etki:** Güvenlik

---

### 20. Input Sanitization
**Dosya:** Frontend form component'leri  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 1-2 saat

**Sorun:**
- XSS koruması kontrol edilmeli

**Çözüm:**
- DOMPurify veya benzeri library kullan
- User input'ları sanitize et

**Etki:** Güvenlik, XSS koruması

---

### 21. Logging Infrastructure
**Dosya:** `Api/Program.cs`  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 3-4 saat

**Sorun:**
- Structured logging yok
- Monitoring yok

**Çözüm:**
```csharp
// Serilog ekle
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
    config.WriteTo.Console();
    config.WriteTo.File("logs/app.log");
    // Application Insights veya ELK Stack entegrasyonu
});
```

**Etki:** Debug kolaylığı, production monitoring

---

### 22. Caching Strategy
**Dosya:** Yeni infrastructure  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 4-5 saat

**Sorun:**
- Cache mekanizması yok

**Çözüm:**
- Redis cache ekle
- Badge count, user summaries için cache kullan
- Cache invalidation stratejisi belirle

**Etki:** Performans artışı, database yükü azalır

---

### 23. UnitOfWork Pattern
**Dosya:** Yeni infrastructure  
**Öncelik:** 🟢 ORTA  
**Tahmini Süre:** 4-5 saat

**Sorun:**
- Transaction yönetimi merkezi değil

**Çözüm:**
```csharp
public interface IUnitOfWork : IDisposable
{
    IAppointmentDal Appointments { get; }
    INotificationDal Notifications { get; }
    // ... diğer DAL'lar
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
```

**Etki:** Transaction yönetimi merkezileşir, kod daha temiz

---

## 📊 ÖZET TABLO

| # | Görev | Öncelik | Tahmini Süre | Durum |
|---|-------|---------|--------------|-------|
| 1 | Background Service Transaction | 🔴 KRİTİK | 1-2 saat | ✅ TAMAMLANDI |
| 2 | N+1 Query Problemleri | 🔴 KRİTİK | 2-3 saat | ✅ TAMAMLANDI |
| 3 | Badge Count Transaction Timing | 🔴 KRİTİK | 1 saat | ✅ TAMAMLANDI |
| 4 | Magic Numbers → Configuration | 🟡 YÜKSEK | 1 saat | ✅ TAMAMLANDI |
| 5 | GetAll → GetLatestImageAsync | 🟡 YÜKSEK | 30 dk | ✅ TAMAMLANDI |
| 6 | BadgeManager In-Memory Sum | 🟡 YÜKSEK | 1 saat | ✅ TAMAMLANDI |
| 7 | Exception Logging | 🟡 YÜKSEK | 2-3 saat | ⚠️ KISMEN |
| 8 | Frontend Console.log Temizliği | 🟡 YÜKSEK | 30 dk | ❌ Açık |
| 9 | Frontend Type Safety | 🟡 YÜKSEK | 2-3 saat | ❌ Açık |
| 10 | API Transform Basitleştirme | 🟡 YÜKSEK | 30 dk | ❌ Açık |
| 11 | Rate Limiting | 🟡 YÜKSEK | 1-2 saat | ❌ Açık |
| 12 | Commented Out Code | 🟢 ORTA | 5 dk | ❌ Açık |
| 13 | Database Index Optimizasyonları | 🟢 ORTA | 1 saat | ✅ TAMAMLANDI |
| 14 | SignalR Error Handling | 🟢 ORTA | 30 dk | ✅ TAMAMLANDI |
| 15 | AppHub OnDisconnectedAsync | 🟢 ORTA | 15 dk | ✅ TAMAMLANDI |
| 16 | SignalR Hook Refactoring | 🟢 ORTA | 2-3 saat | ❌ Açık |
| 17 | Error Boundary | 🟢 ORTA | 1-2 saat | ❌ Açık |
| 18 | Performance Monitoring | 🟢 ORTA | 2-3 saat | ❌ Açık |
| 19 | CORS Configuration Review | 🟢 ORTA | 30 dk | ❌ Açık |
| 20 | Input Sanitization | 🟢 ORTA | 1-2 saat | ❌ Açık |
| 21 | Logging Infrastructure | 🟢 ORTA | 3-4 saat | ❌ Açık |
| 22 | Caching Strategy | 🟢 ORTA | 4-5 saat | ❌ Açık |
| 23 | UnitOfWork Pattern | 🟢 ORTA | 4-5 saat | ❌ Açık |

**Tamamlanan Görevler:** 9/23 (39%)  
**Kalan Tahmini Süre:** ~15-20 saat (tamamlanan görevler çıkarıldı)

---

## 🎯 ÖNERİLEN ÇALIŞMA SIRASI

### Faz 1: Kritik Düzeltmeler (Hemen) - ✅ TAMAMLANDI
1. ✅ Background Service Transaction
2. ✅ N+1 Query Problemleri
3. ✅ Badge Count Transaction Timing

### Faz 2: Yüksek Öncelikli İyileştirmeler (Yakın Zamanda) - ⚠️ KISMEN TAMAMLANDI
4. ✅ Magic Numbers → Configuration
5. ✅ GetAll → GetLatestImageAsync
6. ✅ BadgeManager In-Memory Sum
7. ⚠️ Exception Logging (AppointmentTimeoutWorker'da var, diğer yerlerde eksik)
8. ❌ Frontend Console.log Temizliği
9. ❌ Frontend Type Safety
10. ❌ API Transform Basitleştirme
11. ❌ Rate Limiting

### Faz 3: Orta Öncelikli İyileştirmeler (Planlanabilir)
12-23. Diğer tüm görevler

---

## ⚠️ ÖNEMLİ NOTLAR

1. **Database Migration:** Yeni index'ler için migration oluştur ve çalıştır
2. **Test:** Her düzeltmeden sonra test et
3. **Backup:** Production'a geçmeden önce database backup al
4. **Code Review:** Her değişiklikten sonra code review yap
5. **Documentation:** Değişiklikleri dokümante et

---

## 📝 GÜNCELLEME NOTLARI

- **2025-01-07**: İlk yapılacaklar listesi oluşturuldu
- **2025-01-07**: Backend revize durumu kontrol edildi - 9 görev tamamlandı olarak işaretlendi
  - ✅ Background Service Transaction
  - ✅ N+1 Query Problemleri
  - ✅ Badge Count Transaction Timing
  - ✅ Magic Numbers → Configuration
  - ✅ GetAll → GetLatestImageAsync
  - ✅ BadgeManager In-Memory Sum
  - ✅ Database Index Optimizasyonları
  - ✅ SignalR Error Handling
  - ✅ AppHub OnDisconnectedAsync

---

**Not:** Bu liste sürekli güncellenecektir. Yeni sorunlar tespit edildikçe listeye eklenecektir.

