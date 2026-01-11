# Log Aspect Kullanım Kılavuzu

## LogAspect Nedir?
`LogAspect`, metod çağrılarını otomatik olarak loglayan bir aspect sınıfıdır. Metodların başlangıcını, bitişini, hatalarını ve başarılı sonuçlarını loglar.

## Özellikler
- ✅ Metod başlangıcını loglar (OnBefore)
- ✅ Metod bitişini loglar (OnAfter)
- ✅ Hataları loglar (OnException)
- ✅ Başarılı sonuçları loglar (OnSuccess)
- ✅ Parametreleri loglar (opsiyonel)
- ✅ Return değerlerini loglar (opsiyonel)
- ✅ Kullanıcı bilgilerini loglar (User ID, Email, Name, Roles, UserType)
- ✅ Hassas verileri korur (password, token vb. loglamaz)
- ✅ Dosyaya loglama (günlük dosya)
- ✅ Thread-safe logging

## Log Dosyası Konumu
- Varsayılan: `{AppContext.BaseDirectory}/Logs/log_yyyyMMdd.txt`
- Development: `bin/Debug/net9.0/Logs/log_yyyyMMdd.txt` (veya çalışma dizinine göre)
- Production: Published uygulamanın root dizininde `Logs/log_yyyyMMdd.txt`
- Log klasörü otomatik oluşturulur
- **Önemli**: Log dosyaları her zaman uygulamanın root dizinine göre kaydedilir (AppContext.BaseDirectory kullanılır)

## Kullanım

### Basit Kullanım (Sadece metod adları loglanır)
```csharp
using Core.Aspect.Autofac.Logging;

public class UserManager : IUserService
{
    [LogAspect]
    public async Task<IDataResult<User>> GetById(Guid id)
    {
        // Metod işlemleri
        return new SuccessDataResult<User>(user);
    }
}
```

### Parametreleri Logla (Varsayılan)
```csharp
[LogAspect(logParameters: true)]  // varsayılan true
public async Task<IResult> UpdateUser(UpdateUserDto dto)
{
    // Metod işlemleri
}
```

### Parametreleri Loglama
```csharp
[LogAspect(logParameters: false)]
public async Task<IResult> UpdateUser(UpdateUserDto dto)
{
    // Parametreler loglanmayacak
}
```

### Return Değerini Logla
```csharp
[LogAspect(logReturnValue: true)]
public async Task<IDataResult<List<User>>> GetAll()
{
    // Return değeri loglanacak
    return new SuccessDataResult<List<User>>(users);
}
```

### Özel Log Klasörü
```csharp
[LogAspect(logDirectory: "CustomLogs")]
public async Task<IResult> SomeMethod()
{
    // Loglar CustomLogs klasörüne yazılacak
}
```

### Diğer Aspect'lerle Birlikte Kullanım
```csharp
[LogAspect]
[ValidationAspect(typeof(CreateUserDtoValidator))]
[TransactionScopeAspect]
public async Task<IResult> CreateUser(CreateUserDto dto)
{
    // Önce validation, sonra transaction, sonra loglama
}
```

### SecuredOperation ile Birlikte
```csharp
[LogAspect]
[SecuredOperation("Admin,User", httpContextAccessor)]
public async Task<IDataResult<User>> GetMe(Guid userId)
{
    // Önce yetki kontrolü, sonra loglama
}
```

## Log Formatı

### Başarılı Metod
```
[2025-01-15 14:30:45.123] [INFO] Method started: UserManager.GetById | User: Id: 123e4567-e89b-12d3-a456-426614174000, Email: user@example.com, Name: John, Roles: Customer, Type: Customer | Parameters: {"id":"guid-value"}
[2025-01-15 14:30:45.456] [INFO] Method succeeded: UserManager.GetById | User: Id: 123e4567-e89b-12d3-a456-426614174000, Email: user@example.com
[2025-01-15 14:30:45.457] [INFO] Method completed: UserManager.GetById | User: Id: 123e4567-e89b-12d3-a456-426614174000
```

### Hatalı Metod
```
[2025-01-15 14:30:45.123] [INFO] Method started: UserManager.UpdateUser | User: Id: 123e4567-e89b-12d3-a456-426614174000, Email: user@example.com | Parameters: {"dto":{...}}
[2025-01-15 14:30:45.456] [ERROR] Method failed: UserManager.UpdateUser | User: Id: 123e4567-e89b-12d3-a456-426614174000 | Parameters: {"dto":{...}} | Error: Entity not found | StackTrace: ...
[2025-01-15 14:30:45.457] [INFO] Method completed: UserManager.UpdateUser | User: Id: 123e4567-e89b-12d3-a456-426614174000
```

## Güvenlik
- Password, Token, Secret, Key, Credential, Auth içeren parametreler otomatik olarak `***REDACTED***` olarak loglanır
- Hassas veriler korunur

## Performans
- Thread-safe logging (lock kullanılır)
- Dosya yazma hatalarında metod çalışmaya devam eder
- Günlük log dosyası rotasyonu (her gün yeni dosya)

## Örnek Kullanım Senaryoları

### Senaryo 1: Kritik Metodları Logla
```csharp
[LogAspect]
[TransactionScopeAspect]
public async Task<IResult> CreateAppointment(CreateAppointmentDto dto)
{
    // Kritik işlemler - hem loglanır hem transaction içinde
}
```

### Senaryo 2: Sadece Hataları İzle
```csharp
[LogAspect(logParameters: false)]
public async Task<IDataResult<User>> GetUser(Guid id)
{
    // Parametreler loglanmaz, sadece metod adı ve hatalar loglanır
}
```

### Senaryo 3: Debug için Detaylı Loglama
```csharp
[LogAspect(logParameters: true, logReturnValue: true)]
public async Task<IDataResult<List<User>>> GetAllUsers()
{
    // Hem parametreler hem return değerleri loglanır
}
```

## Kullanıcı Bilgileri
LogAspect, metod çağrılarında kullanıcı bilgilerini otomatik olarak loglar:
- **User ID**: Kullanıcının benzersiz ID'si (Guid)
- **Email**: Kullanıcının e-posta adresi
- **Name**: Kullanıcının adı
- **Roles**: Kullanıcının rolleri (virgülle ayrılmış)
- **UserType**: Kullanıcı tipi (Customer, FreeBarber, BarberStore)

Kullanıcı bilgileri sadece authenticated isteklerde loglanır. Eğer kullanıcı authenticate olmamışsa, kullanıcı bilgisi loglanmaz.

**Not**: Program.cs'de `AddHttpContextAccessor()` çağrısı yapılmalıdır (zaten yapılmış).

## Notlar
- Aspect'ler metod seviyesinde veya class seviyesinde kullanılabilir
- Class seviyesinde kullanılırsa, tüm metodlar loglanır
- Multiple aspect'ler kullanılabilir (priority ile sıralama mümkün)
- Async metodlar desteklenir
- Task return değerleri özel olarak işlenir (loglanmaz)
- Kullanıcı bilgileri sadece authenticated isteklerde loglanır
