# ExceptionHandlingAspect Kullanım Kılavuzu

## Genel Bakış

`ExceptionHandlingAspect`, method'larda oluşan exception'ları otomatik olarak yakalayıp, uygun `IResult` veya `IDataResult<T>` döndüren bir aspect'tir. Bu sayede manuel try-catch bloklarına ihtiyaç kalmaz.

## Özellikler

- ✅ Exception'ları otomatik yakalar ve handle eder
- ✅ Exception tipine göre uygun error message döndürür
- ✅ `IResult` ve `IDataResult<T>` return type'larını destekler
- ✅ Async method'ları (`Task<T>`) destekler
- ✅ LogAspect ile birlikte çalışır (exception'lar loglanır)
- ✅ Özel exception tiplerini destekler (BusinessRuleException, EntityNotFoundException, vb.)

## Kullanım

### Temel Kullanım

```csharp
[ExceptionHandlingAspect]
[LogAspect]
public async Task<IResult> MyMethod()
{
    // Exception oluşursa otomatik olarak ErrorResult döndürülür
    throw new BusinessRuleException("Bir hata oluştu");
    // return new SuccessResult(); // Buraya asla gelmez
}
```

### IDataResult ile Kullanım

```csharp
[ExceptionHandlingAspect]
[LogAspect]
public async Task<IDataResult<List<MyDto>>> GetDataAsync()
{
    // Exception oluşursa otomatik olarak ErrorDataResult<List<MyDto>> döndürülür
    throw new EntityNotFoundException("User", userId);
    // return new SuccessDataResult<List<MyDto>>(data);
}
```

### Özel Hata Mesajı ile Kullanım

```csharp
[ExceptionHandlingAspect(customErrorMessage: "Özel hata mesajı")]
[LogAspect]
public async Task<IResult> MyMethod()
{
    throw new Exception("Bu mesaj görünmez, 'Özel hata mesajı' görünür");
}
```

### Exception'ı Tekrar Fırlatma

```csharp
[ExceptionHandlingAspect(rethrowException: true)]
[LogAspect]
public async Task<IResult> MyMethod()
{
    // Exception handle edilir ama tekrar fırlatılır
    throw new Exception("Hata");
}
```

## Desteklenen Exception Tipleri

- `BusinessRuleException` → Exception message kullanılır
- `EntityNotFoundException` → Exception message kullanılır
- `UnauthorizedOperationException` → Exception message kullanılır
- `ArgumentException` → Exception message kullanılır
- `ArgumentNullException` → Exception message veya "Geçersiz parametre"
- `InvalidOperationException` → Exception message kullanılır
- Diğer exception'lar → Exception message veya "Bir hata oluştu. Lütfen tekrar deneyin."

## Örnek: Try-Catch Kaldırma

### Önce (Manuel Try-Catch)

```csharp
[LogAspect]
public async Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text)
{
    try
    {
        // ... kod ...
        await realtime.PushBadgeUpdateAsync(userId);
    }
    catch (Exception ex)
    {
        return new ErrorDataResult<ChatMessageDto>(ex.Message);
    }
    
    return new SuccessDataResult<ChatMessageDto>(dto);
}
```

### Sonra (ExceptionHandlingAspect ile)

```csharp
[ExceptionHandlingAspect]
[LogAspect]
public async Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text)
{
    // ... kod ...
    await realtime.PushBadgeUpdateAsync(userId);
    // Exception oluşursa otomatik olarak ErrorDataResult döndürülür
    
    return new SuccessDataResult<ChatMessageDto>(dto);
}
```

## Örnek: Exception'ı Ignore Etme

Bazı durumlarda exception'ı ignore etmek isteyebilirsiniz. Bu durumda aspect kullanmak yerine, sadece exception'ı yakalayıp ignore edebilirsiniz:

```csharp
// Exception'ı ignore etmek için
try 
{ 
    await realtime.PushBadgeUpdateAsync(userId); 
} 
catch 
{ 
    // ignore in system ops 
}
```

Ancak, eğer bir result döndürmeniz gerekiyorsa, aspect kullanabilirsiniz:

```csharp
[ExceptionHandlingAspect]
public async Task<IResult> UpdateBadgeAsync(Guid userId)
{
    await realtime.PushBadgeUpdateAsync(userId);
    return new SuccessResult();
    // Exception oluşursa ErrorResult döndürülür
}
```

## Aspect Sırası

Aspect'lerin sırası önemlidir. Genellikle şu sırayı kullanın:

1. `SecuredOperation` - Yetki kontrolü
2. `ValidationAspect` - Validasyon
3. `LogAspect` - Loglama
4. `ExceptionHandlingAspect` - Exception handling
5. `TransactionScopeAspect` - Transaction (en son)

```csharp
[SecuredOperation("Customer,FreeBarber,BarberStore")]
[ValidationAspect(typeof(MyDtoValidator))]
[LogAspect]
[ExceptionHandlingAspect]
[TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
public async Task<IResult> MyMethod(MyDto dto)
{
    // ...
}
```

## Notlar

- `ExceptionHandlingAspect` sadece `IResult` veya `IDataResult<T>` döndüren method'larda çalışır
- Exception'lar `LogAspect` tarafından otomatik olarak loglanır
- Aspect, exception'ı yakaladıktan sonra method'un geri kalanını çalıştırmaz
- `RethrowException = true` kullanılırsa, exception handle edilir ama tekrar fırlatılır
