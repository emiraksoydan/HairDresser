# SecuredOperation Kullanım Rehberi

Bu doküman, HairDresser projesinde `SecuredOperation` attribute'ünün nerede ve nasıl kullanılacağını açıklar.

## SecuredOperation Nedir?

`SecuredOperation` attribute'ü, metod seviyesinde rol bazlı yetkilendirme kontrolü yapar. JWT token'dan kullanıcının rollerini alır ve belirtilen rollerden birine sahip olup olmadığını kontrol eder.

### Kullanım Şekli

```csharp
[SecuredOperation("Role1,Role2")]
public async Task<IResult> SomeMethod()
{
    // Metod çalışır
}
```

**Not:** `IHttpContextAccessor` parametresini vermeye gerek yoktur. `Program.cs`'de otomatik olarak ayarlanır.

## Mevcut Roller

- **Admin**: Sistem yöneticisi
- **User**: Genel kullanıcı (tüm kullanıcılara otomatik atanır)
- **Customer**: Müşteri
- **FreeBarber**: Serbest berber
- **BarberStore**: Kuaför dükkanı sahibi

## SecuredOperation Kullanım Önerileri

### 1. Admin İşlemleri

Sadece admin rolüne sahip kullanıcıların erişebileceği işlemler:

#### CategoryManager
```csharp
// Kategori ekleme - Sadece Admin
[SecuredOperation("Admin")]
public async Task<IResult> AddCategory(Category category)

// Kategori silme - Sadece Admin
[SecuredOperation("Admin")]
public async Task<IResult> DeleteCategory(Guid id)
```

#### OperationClaimManager
```csharp
// Tüm rollerı getir - Sadece Admin
[SecuredOperation("Admin")]
public async Task<IDataResult<List<OperationClaim>>> GetAllOperationClaim()
```

#### UserOperationClaimManager
```csharp
// Kullanıcıya rol atama - Sadece Admin
[SecuredOperation("Admin")]
public async Task<IDataResult<List<UserOperationClaim>>> AddUserOperationsClaim(List<UserOperationClaim> userOperationClaims)
```

### 2. BarberStore İşlemleri

Sadece BarberStore rolüne sahip kullanıcıların erişebileceği işlemler:

#### BarberStoreManager
```csharp
// Dükkan ekleme - BarberStore rolü
[SecuredOperation("BarberStore")]
public async Task<IResult> Add(BarberStoreCreateDto dto, Guid currentUserId)

// Dükkan güncelleme - BarberStore rolü
[SecuredOperation("BarberStore")]
public async Task<IResult> Update(BarberStoreUpdateDto dto, Guid currentUserId)

// Dükkan silme - BarberStore rolü
[SecuredOperation("BarberStore")]
public async Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId)

// Kendi dükkanlarını getir - BarberStore rolü
[SecuredOperation("BarberStore")]
public async Task<IDataResult<List<BarberStoreMineDto>>> GetByCurrentUserAsync(Guid currentUserId)
```

### 3. FreeBarber İşlemleri

Sadece FreeBarber rolüne sahip kullanıcıların erişebileceği işlemler:

#### FreeBarberManager
```csharp
// Serbest berber paneli ekleme - FreeBarber rolü
[SecuredOperation("FreeBarber")]
public async Task<IResult> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId)

// Serbest berber güncelleme - FreeBarber rolü
[SecuredOperation("FreeBarber")]
public async Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto, Guid currentUserId)

// Serbest berber silme - FreeBarber rolü
[SecuredOperation("FreeBarber")]
public async Task<IResult> DeleteAsync(Guid storeId)

// Konum güncelleme - FreeBarber rolü
[SecuredOperation("FreeBarber")]
public async Task<IResult> UpdateLocationAsync(UpdateLocationDto dto, Guid currentUserId)

// Kendi panelini getir - FreeBarber rolü
[SecuredOperation("FreeBarber")]
public async Task<IDataResult<FreeBarberMinePanelDto>> GetMyPanel(Guid currentUserId)

// Kendi panel detayını getir - FreeBarber rolü
[SecuredOperation("FreeBarber")]
public async Task<IDataResult<FreeBarberMinePanelDetailDto>> GetMyPanelDetail(Guid panelId)
```

### 4. Customer İşlemleri

Customer rolüne sahip kullanıcıların erişebileceği işlemler:

#### AppointmentManager (Customer için)
```csharp
// Randevu oluşturma - Customer, FreeBarber, BarberStore rolleri
[SecuredOperation("Customer,FreeBarber,BarberStore")]
public async Task<IDataResult<bool>> CreateAppointment(...)

// Randevu iptal etme - Customer rolü (veya randevu sahibi)
[SecuredOperation("Customer")]
public async Task<IDataResult<bool>> CancelAppointment(...)
```

### 5. Çoklu Rol Kontrolü

Birden fazla rolden birine sahip olanların erişebileceği işlemler:

```csharp
// Hem FreeBarber hem de BarberStore rolü olanlar erişebilir
[SecuredOperation("FreeBarber,BarberStore")]
public async Task<IResult> SomeMethod()

// Hem Admin hem de User rolü olanlar erişebilir
[SecuredOperation("Admin,User")]
public async Task<IResult> SomeMethod()
```

### 6. Herkes Erişebilen İşlemler (SecuredOperation Gerektirmez)

Aşağıdaki işlemler herkes tarafından erişilebilir, `SecuredOperation` gerekmez:

- **Listeleme/Görüntüleme İşlemleri:**
  - `GetNearbyStoresAsync` - Yakındaki dükkanları listele
  - `GetFilteredStoresAsync` - Filtrelenmiş dükkanları listele
  - `GetNearbyFreeBarberAsync` - Yakındaki serbest berberleri listele
  - `GetFilteredFreeBarbersAsync` - Filtrelenmiş serbest berberleri listele
  - `GetByIdAsync` - Dükkan detayı görüntüle
  - `GetBarberStoreForUsers` - Kullanıcılar için dükkan bilgisi
  - `GetFreeBarberForUsers` - Kullanıcılar için serbest berber bilgisi
  - `GetAllCategories` - Tüm kategorileri listele
  - `GetParentCategories` - Ana kategorileri listele
  - `GetChildCategories` - Alt kategorileri listele

- **Auth İşlemleri:**
  - `SendOtpAsync` - OTP gönder (kayıt/giriş)
  - `VerifyOtpAsync` - OTP doğrula (kayıt/giriş)
  - `LoginWithPassword` - Şifre ile giriş
  - `RefreshAsync` - Token yenile

## Kullanım Örnekleri

### Örnek 1: Admin İşlemi
```csharp
using Business.BusinessAspect.Autofac;

public class CategoryManager
{
    [SecuredOperation("Admin")]
    [LogAspect]
    public async Task<IResult> AddCategory(Category category)
    {
        await categoriesDal.Add(category);
        return new SuccessResult("Kategori Eklendi");
    }
}
```

### Örnek 2: Çoklu Rol Kontrolü
```csharp
[SecuredOperation("FreeBarber,BarberStore")]
[LogAspect]
public async Task<IResult> Update(SomeDto dto, Guid currentUserId)
{
    // FreeBarber VEYA BarberStore rolüne sahip kullanıcılar erişebilir
}
```

### Örnek 3: User Rolü (Genel Erişim)
```csharp
[SecuredOperation("User")]
public async Task<IDataResult<UserProfileDto>> GetMe(Guid userId)
{
    // Tüm kullanıcılar erişebilir (User rolü herkese atanır)
}
```

## Önemli Notlar

1. **HttpContextAccessor:** `IHttpContextAccessor` parametresini vermeye **gerek yoktur**. `Program.cs`'de otomatik olarak ayarlanır ve static property olarak saklanır.

2. **Program.cs Kurulumu:** `IHttpContextAccessor` CoreModule'de zaten kayıtlı. Sadece static property'ye atama yapılır:
   ```csharp
   // CoreModule'de zaten kayıtlı: serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
   // Static property'ye atama:
   var httpContextAccessor = app.Services.GetService<IHttpContextAccessor>();
   if (httpContextAccessor != null)
   {
       Business.BusinessAspect.Autofac.SecuredOperation.HttpContextAccessor = httpContextAccessor;
   }
   ```

3. **Rol İsimleri:** Rol isimleri tam olarak veritabanındaki `OperationClaims` tablosundaki `Name` kolonuyla eşleşmelidir (büyük/küçük harf duyarlı).

4. **OR Mantığı:** Birden fazla rol belirtildiğinde (virgülle ayrılmış), kullanıcının bu rollerden **birine** sahip olması yeterlidir.

5. **Yetki Kontrolü Sırası:** `SecuredOperation` attribute'ü metodun çalışmasından **önce** kontrol edilir (`OnBefore`).

6. **Exception:** Yetki kontrolü başarısız olursa `UnauthorizedOperationException` fırlatılır ve metod çalışmaz.

## Test Senaryoları

1. **Admin Test:** Admin rolüne sahip kullanıcı admin işlemlerine erişebilir.
2. **Rol Yok Test:** Hiç rolü olmayan kullanıcı korumalı metodlara erişemez.
3. **Yanlış Rol Test:** BarberStore rolüne sahip kullanıcı FreeBarber metodlarına erişemez.
4. **Çoklu Rol Test:** FreeBarber ve User rolüne sahip kullanıcı FreeBarber metodlarına erişebilir.

## Gelecek İyileştirmeler

- [ ] Resource bazlı yetkilendirme (ör: sadece kendi dükkanını güncelleyebilir)
- [ ] Action bazlı yetkilendirme (ör: Create, Update, Delete, Read)
- [ ] Daha detaylı rol hiyerarşisi
- [ ] Permission tablosu (OperationClaim yerine)
