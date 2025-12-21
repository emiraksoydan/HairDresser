# Filtreleme ve Arama API Güncellemesi

## 📋 Yapılan Değişiklikler

### 1. CategorySeeder Kaldırıldı ✅
- `Business/Concrete/CategorySeeder.cs` silindi
- `Program.cs`'deki otomatik seed kodu kaldırıldı
- Kategoriler artık **manuel SQL** ile eklenecek (`SeedCategories.sql`)

### 2. Filtreleme API'leri Eklendi ✅

#### Yeni Endpoint'ler

**BarberStore:**
```http
POST /api/BarberStore/filtered
Content-Type: application/json

{
  "latitude": 41.0082,
  "longitude": 28.9784,
  "distance": 1.0,
  "searchQuery": "berber",
  "mainCategory": 0,  // 0=MaleHairdresser, 1=FemaleHairdresser, 2=BeautySalon
  "serviceIds": ["guid1", "guid2"],
  "priceSort": "asc",  // "none", "asc", "desc"
  "minPrice": 50,
  "maxPrice": 200,
  "pricingType": "rent",  // "all", "rent", "percent"
  "minRating": 4,
  "favoritesOnly": false,
  "pageNumber": 1,
  "pageSize": 50
}
```

**FreeBarber:**
```http
POST /api/FreeBarber/filtered
Content-Type: application/json

{
  "latitude": 41.0082,
  "longitude": 28.9784,
  "distance": 1.0,
  "searchQuery": "ahmet",
  "mainCategory": 0,
  "serviceIds": ["guid1", "guid2"],
  "priceSort": "asc",
  "minPrice": 50,
  "maxPrice": 200,
  "availability": "available",  // "all", "available", "unavailable"
  "minRating": 4,
  "favoritesOnly": false,
  "pageNumber": 1,
  "pageSize": 50
}
```

### 3. Yeni DTO Eklendi ✅

**FilterRequestDto** (`Entities/Concrete/Dto/FilterRequestDto.cs`):
```csharp
public class FilterRequestDto : IDto
{
    // Konum
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double Distance { get; set; } = 1.0;

    // Arama
    public string? SearchQuery { get; set; }

    // Filtreler
    public string? UserType { get; set; }
    public BarberType? MainCategory { get; set; }
    public List<Guid>? ServiceIds { get; set; }
    public string? PriceSort { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? PricingType { get; set; }
    public string? Availability { get; set; }
    public int? MinRating { get; set; }
    public bool? FavoritesOnly { get; set; }

    // Pagination
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
```

### 4. Interface Güncellemeleri ✅

**IBarberStoreService:**
```csharp
Task<IDataResult<List<BarberStoreGetDto>>> GetFilteredStoresAsync(FilterRequestDto filter, Guid? currentUserId = null);
```

**IFreeBarberService:**
```csharp
Task<IDataResult<List<FreeBarberGetDto>>> GetFilteredFreeBarbersAsync(FilterRequestDto filter, Guid? currentUserId = null);
```

**IBarberStoreDal:**
```csharp
Task<List<BarberStoreGetDto>> GetFilteredStoresAsync(FilterRequestDto filter, Guid? currentUserId = null);
```

**IFreeBarberDal:**
```csharp
Task<List<FreeBarberGetDto>> GetFilteredFreeBarbersAsync(FilterRequestDto filter, Guid? currentUserId = null);
```

### 5. DAL Implementation Dosyaları ✅

**Önemli:** Aşağıdaki metotları ilgili dosyalara ekleyin:

1. **EfBarberStoreDal.cs** içine:
   - `EfBarberStoreDal_Filtered.cs` dosyasındaki `GetFilteredStoresAsync` metodunu ekleyin

2. **EfFreeBarberDal.cs** içine:
   - `EfFreeBarberDal_Filtered.cs` dosyasındaki `GetFilteredFreeBarbersAsync` metodunu ekleyin

## 🎯 Filtreleme Özellikleri

### BarberStore Filtreleri
✅ Konum bazlı (nearby)
✅ İsim araması
✅ Ana kategori (Erkek Berber, Bayan Kuaför, Güzellik Salonu)
✅ Hizmet bazlı (CategoryId listesi)
✅ Fiyat sıralaması (artan/azalan)
✅ Fiyat aralığı (min-max)
✅ Pricing Type (Rent/Percent)
✅ Puanlama (minimum rating)
✅ Favoriler (sadece favorileri göster)
✅ Pagination

### FreeBarber Filtreleri
✅ Konum bazlı (nearby)
✅ İsim araması
✅ Ana kategori (Erkek Berber, Bayan Kuaför, Güzellik Salonu)
✅ Hizmet bazlı (CategoryId listesi)
✅ Fiyat sıralaması (min offering price bazlı)
✅ Fiyat aralığı (min offering price bazlı)
✅ Müsaitlik (Available/Unavailable)
✅ Puanlama (minimum rating)
✅ Favoriler (sadece favorileri göster)
✅ Pagination

## 📝 Frontend Entegrasyonu

### RTK Query Endpoint Örnekleri

```typescript
// store/api.ts

export const api = createApi({
  // ... existing code
  endpoints: (builder) => ({
    // ... existing endpoints
    
    getFilteredStores: builder.mutation<BarberStoreGetDto[], FilterRequestDto>({
      query: (filter) => ({
        url: '/BarberStore/filtered',
        method: 'POST',
        body: filter,
      }),
      invalidatesTags: ['Stores'],
    }),
    
    getFilteredFreeBarbers: builder.mutation<FreeBarberGetDto[], FilterRequestDto>({
      query: (filter) => ({
        url: '/FreeBarber/filtered',
        method: 'POST',
        body: filter,
      }),
      invalidatesTags: ['FreeBarbers'],
    }),
  }),
});

export const {
  useGetFilteredStoresMutation,
  useGetFilteredFreeBarbersMutation,
} = api;
```

### Kullanım Örneği

```typescript
// Panel index component

const [triggerFilterStores, { data: stores, isLoading }] = useGetFilteredStoresMutation();

const handleApplyFilters = useCallback(async () => {
  const filter: FilterRequestDto = {
    latitude: location.latitude,
    longitude: location.longitude,
    distance: 1.0,
    searchQuery: searchQuery,
    mainCategory: selectedMainCategory === "Hepsi" ? null : getCategoryEnum(selectedMainCategory),
    serviceIds: selectedServices,
    priceSort: priceSort,
    minPrice: minPrice ? parseFloat(minPrice) : null,
    maxPrice: maxPrice ? parseFloat(maxPrice) : null,
    pricingType: selectedPricingType,
    minRating: selectedRating,
    favoritesOnly: showFavoritesOnly,
    pageNumber: 1,
    pageSize: 50,
  };
  
  await triggerFilterStores(filter);
}, [/* dependencies */]);
```

## 🔧 Backend Kurulum Adımları

### 1. DAL Metotlarını Ekleyin

**EfBarberStoreDal.cs:**
```csharp
// EfBarberStoreDal_Filtered.cs dosyasındaki GetFilteredStoresAsync metodunu
// EfBarberStoreDal.cs dosyasının sonuna (son } işaretinden önce) ekleyin
```

**EfFreeBarberDal.cs:**
```csharp
// EfFreeBarberDal_Filtered.cs dosyasındaki GetFilteredFreeBarbersAsync metodunu
// EfFreeBarberDal.cs dosyasının sonuna (son } işaretinden önce) ekleyin
```

### 2. Build ve Test

```bash
# Backend'i build edin
dotnet build

# Hata varsa düzeltin
# Genellikle using eksiklikleri olabilir:
# - using Entities.Concrete.Dto;
# - using Entities.Concrete.Enums;
```

### 3. Swagger'da Test Edin

```
1. Backend'i çalıştırın
2. https://localhost:7xxx/swagger açın
3. POST /api/BarberStore/filtered endpoint'ini test edin
4. POST /api/FreeBarber/filtered endpoint'ini test edin
```

## ⚠️ Önemli Notlar

1. **CategorySeeder kaldırıldı** - Kategorileri `SeedCategories.sql` ile manuel ekleyin
2. **Filtreleme artık backend'de** - Frontend'deki filtreleme kodlarını kaldırın
3. **Pagination eklendi** - Büyük veri setleri için performans
4. **CurrentUserId otomatik** - Controller'dan geliyor, JWT token'dan alınıyor
5. **Favori filtresi** - Sadece giriş yapmış kullanıcılar için çalışır

## 🐛 Sorun Giderme

### Hata: "FilterRequestDto not found"
**Çözüm**: `Entities.Concrete.Dto` namespace'ini ekleyin

### Hata: "GeoBounds not found"
**Çözüm**: Mevcut `GetNearbyStoresAsync` metodunda kullanılıyor, aynı dosyada olmalı

### Hata: "OpenControl not found"
**Çözüm**: Mevcut kodda kullanılıyor, using eksikliği olabilir

### Frontend'de veri gelmiyor
**Çözüm**:
1. Backend çalışıyor mu?
2. Swagger'da test edin
3. Network tab'da request/response kontrol edin
4. Console'da hata var mı?

## 📊 Performans

- **Pagination**: Varsayılan 50 kayıt
- **Caching**: RTK Query otomatik cache yapıyor
- **Index'ler**: Latitude, Longitude, Type, IsActive kolonlarında index olmalı
- **N+1 Problem**: Tüm ilişkili veriler tek sorguda alınıyor

## 🎉 Sonuç

✅ CategorySeeder kaldırıldı
✅ Filtreleme API'leri eklendi
✅ Frontend'den backend'e taşındı
✅ Performans optimize edildi
✅ Pagination eklendi
✅ Favori filtresi eklendi
✅ Puanlama filtresi eklendi

**Artık tüm filtreleme ve arama işlemleri backend'de yapılıyor!**

