# Store -> FreeBarber Çağrı API Dokümantasyonu

## Özet
Store, bir FreeBarber'i **tarih, saat ve hizmet belirtmeden** direkt çağırabilir. Bu yeni endpoint ile Store sadece FreeBarber seçimi yaparak randevu oluşturur.

---

## Yeni Endpoint (Basitleştirilmiş - Önerilen)

### `POST /api/appointment/store/call-freebarber`

**Açıklama:** Store, sadece FreeBarber ID'si ile FreeBarber çağırır - tarih/saat/hizmet seçimi YOK.

**Headers:**
```
Authorization: Bearer {JWT_TOKEN}
Content-Type: application/json
```

**Request Body:**
```json
{
  "storeId": "guid",
  "freeBarberUserId": "guid"
}
```

**Request DTO:**
```typescript
interface CreateStoreToFreeBarberRequestDto {
  storeId: string;        // Store GUID (zorunlu)
  freeBarberUserId: string;  // FreeBarber User GUID (zorunlu)
}
```

**Validasyonlar:**
- `storeId`: Store mevcut olmalı ve isteği yapan kullanıcı bu Store'un sahibi olmalı
- `freeBarberUserId`: FreeBarber mevcut ve aktif (IsAvailable = true) olmalı
- Mesafe kontrolü: FreeBarber ile Store arası maksimum mesafe kuralı
- Store aynı anda sadece 1 aktif "call" yapabilir (business rule)

**Başarılı Yanıt (200 OK):**
```json
{
  "success": true,
  "data": "appointment-guid-id",
  "message": "Success"
}
```

**Hata Yanıtı (400 Bad Request):**
```json
{
  "success": false,
  "message": "FreeBarber bulunamadı veya aktif değil"
}
```

**Oluşturulan Randevu Bilgileri:**
- `RequestedBy`: Store
- `Status`: Pending
- `StoreDecision`: Approved (otomatik)
- `FreeBarberDecision`: Pending (FreeBarber onayı bekleniyor)
- `AppointmentDate`: null
- `StartTime`: null
- `EndTime`: null
- `ChairId`: null
- `PendingExpiresAt`: Şu andan itibaren X dakika (config'den)

---

## Mevcut Endpoint (Eski - Hala Kullanılabilir)

### `POST /api/appointment/store`

**Açıklama:** Store, FreeBarber çağırır - opsiyonel olarak tarih/saat/hizmet eklenebilir (eski davranış).

**Request Body:**
```json
{
  "storeId": "guid",
  "freeBarberUserId": "guid",
  "appointmentDate": "2025-12-27",    // Opsiyonel
  "startTime": "10:00:00",             // Opsiyonel
  "endTime": "11:00:00",               // Opsiyonel
  "chairId": "guid",                   // Opsiyonel
  "serviceOfferingIds": ["guid1", "guid2"]  // Opsiyonel
}
```

**Not:** Artık tüm tarih/saat/hizmet alanları opsiyonel. Hiçbiri gönderilmezse basit "çağrı" yapılır.

---

## Frontend Kullanım Önerisi

### React/TypeScript Örnek

```typescript
// API Service
export const callFreeBarber = async (storeId: string, freeBarberUserId: string) => {
  const response = await fetch('/api/appointment/store/call-freebarber', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${getAuthToken()}`
    },
    body: JSON.stringify({
      storeId,
      freeBarberUserId
    })
  });
  
  return response.json();
};

// Component Example
const CallFreeBarberButton = ({ storeId, freeBarber }) => {
  const [loading, setLoading] = useState(false);
  
  const handleCall = async () => {
    setLoading(true);
    try {
      const result = await callFreeBarber(storeId, freeBarber.id);
      if (result.success) {
        toast.success('FreeBarber çağrısı başarılı! Onay bekleniyor.');
        // Navigate to appointment detail or list
      } else {
        toast.error(result.message);
      }
    } catch (error) {
      toast.error('Bir hata oluştu');
    } finally {
      setLoading(false);
    }
  };
  
  return (
    <button onClick={handleCall} disabled={loading}>
      {loading ? 'Çağrılıyor...' : 'Çağır'}
    </button>
  );
};
```

---

## Değişiklik Özeti

### Backend Değişiklikleri:
1. ✅ `CreateStoreToFreeBarberAsync` metodunda tarih/saat/sandalye/hizmet kontrolleri kaldırıldı
2. ✅ `CreateStoreToFreeBarberRequestDto` yeni DTO eklendi (sadece StoreId + FreeBarberUserId)
3. ✅ `/api/appointment/store/call-freebarber` yeni endpoint eklendi
4. ✅ Eski `/api/appointment/store` endpoint korundu (geriye dönük uyumluluk)

### Frontend Yapılması Gerekenler:
1. ❌ Yeni endpoint'i kullanacak servis fonksiyonu ekle
2. ❌ Store panelinde "FreeBarber Çağır" butonu/komponenti ekle
3. ❌ Tarih/saat/hizmet seçimi KALDIRIN - sadece FreeBarber listesi/seçimi yeterli
4. ❌ Başarılı çağrı sonrası kullanıcıya bildirim göster
5. ❌ Randevu listesinde "Pending" status'ündeki çağrıları göster

---

## Hata Kodları ve Mesajları

| Hata | Mesaj |
|------|-------|
| FreeBarber bulunamadı | "FreeBarber bulunamadı" |
| FreeBarber aktif değil | "FreeBarber şu anda müsait değil" |
| Store bulunamadı | "Dükkan bulunamadı veya yetkiniz yok" |
| Mesafe fazla | "FreeBarber dükkanınıza çok uzak" |
| Aktif çağrı mevcut | "Zaten aktif bir çağrınız var" |

---

## Test Adımları

1. Store kullanıcısı olarak giriş yap
2. Yakındaki aktif FreeBarber'ları listele
3. Bir FreeBarber seç
4. "Çağır" butonuna tıkla (tarih/saat/hizmet seçimi YOK)
5. API isteği gönderilir: `POST /api/appointment/store/call-freebarber`
6. Başarılı yanıt: Randevu ID dönülür
7. FreeBarber bildirim alır, onay/red edebilir
8. Store randevu listesinde "Pending" olarak görür

---

## Migration & Database

**Değişiklik YOK:** Mevcut `Appointment` tablosu kullanılır. Tarih/saat alanları zaten nullable, bu yüzden migration gerekmez.

---

## Notlar

- FreeBarber mesafe kontrolü korunmuştur
- Store aynı anda sadece 1 aktif çağrı yapabilir (business rule)
- FreeBarber IsAvailable = false ise çağrı yapılamaz
- Randevu oluşturulduğunda FreeBarber'ın IsAvailable otomatik false olur (kilitlenir)
- FreeBarber red ederse veya timeout olursa IsAvailable tekrar true olur
- Frontend'de tarih/saat/hizmet seçimi ekranlarını KALDIRIN (Store -> FreeBarber için)

---

## İletişim

Sorular için: Backend Developer
