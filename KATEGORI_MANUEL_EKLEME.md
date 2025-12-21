# Kategori Sistemi - Manuel Ekleme Rehberi

## 📋 Özet

Kategori sistemi güncellendi. Ana kategoriler:
1. **Erkek Berber** (Saç kesimi + Sakal işlemleri)
2. **Bayan Kuaför** (Saç kesimi + Boyama + Şekillendirme)
3. **Güzellik Salonu** (Cilt bakımı, Lazer, Kaş-Kirpik, Masaj, vb.)

## 🗂️ Kategori Yapısı

### 1. Erkek Berber (17 alt kategori)
- **Kesim**: Döz, Asimetrik, Silme, Kısa, Uzun, Orta, Düzgün, Fade, Undercut
- **Sakal**: Düzeltme, Kesim, Boyama, Şekillendirme
- **Bıyık**: Kesim
- **Bakım**: Cilt Bakımı, Yüz Masajı, Ağda

### 2. Bayan Kuaför (25 alt kategori)
- **Kesim**: Kısa, Orta, Uzun, Wolf Cut, Shag, Pixie, Bob, Lob, Katmanlı, Küt
- **Boyama**: Dip Boya, Komple, Ombre, Sombre, Balayaj, Gölge, Shadow Root, Highlights, Lowlights, Röfle, Platin, Pastel
- **Şekillendirme**: Fön, Maşa, Düzleştirme

### 3. Güzellik Salonu (80+ alt kategori)
Fotoğraflardaki tüm hizmetler (Cilt Bakımı, Lazer Epilasyon, Kaş-Kirpik, Dudak, El-Ayak, Masaj, Saç Bakımı, Vücut Bakımı, vb.)

## 🚀 Manuel Ekleme Adımları

### Yöntem 1: SQL Server Management Studio (SSMS)

1. **SSMS'i Aç**
   - SQL Server Management Studio'yu başlat
   - Veritabanı sunucunuza bağlan

2. **SQL Dosyasını Aç**
   ```
   File → Open → File...
   Dosya: C:\Users\yazilimciemir\source\repos\HairDresser\DataAccess\Migrations\SeedCategories.sql
   ```

3. **Veritabanını Seç**
   - Üstteki dropdown'dan `HairDresser` veritabanını seçin

4. **Çalıştır**
   - `F5` tuşuna basın veya `Execute` butonuna tıklayın

5. **Sonuç**
   ```
   Kategoriler başarıyla eklendi!
   Toplam: 3 ana kategori (Erkek Berber, Bayan Kuaför, Güzellik Salonu) + 100+ alt kategori
   ```

### Yöntem 2: Azure Data Studio

1. **Azure Data Studio'yu Aç**
   - Uygulamayı başlat
   - Sunucunuza bağlan

2. **Yeni Query**
   - `Ctrl+N` veya `New Query` butonuna tıkla

3. **SQL Kodunu Kopyala**
   - `SeedCategories.sql` dosyasını aç
   - Tüm içeriği kopyala (`Ctrl+A`, `Ctrl+C`)
   - Query penceresine yapıştır (`Ctrl+V`)

4. **Veritabanını Seç**
   - Üstteki dropdown'dan veritabanınızı seçin

5. **Çalıştır**
   - `F5` veya `Run` butonuna tıkla

### Yöntem 3: Visual Studio SQL Server Object Explorer

1. **SQL Server Object Explorer'ı Aç**
   ```
   View → SQL Server Object Explorer
   ```

2. **Veritabanınıza Git**
   ```
   Sunucunuzu genişlet → Databases → HairDresser
   ```

3. **New Query**
   - Veritabanına sağ tıkla → `New Query...`

4. **SQL'i Yapıştır ve Çalıştır**
   - `SeedCategories.sql` içeriğini yapıştır
   - `Execute` butonuna tıkla veya `Ctrl+Shift+E`

## ✅ Kontrol Sorguları

SQL çalıştıktan sonra kontrol için:

```sql
-- Toplam kategori sayısı
SELECT COUNT(*) FROM Categories;
-- Sonuç: 120+ olmalı

-- Ana kategoriler
SELECT * FROM Categories WHERE ParentId IS NULL;
-- Sonuç: 3 satır (Erkek Berber, Bayan Kuaför, Güzellik Salonu)

-- Alt kategoriler (ana kategori bazında)
SELECT 
    c.Name as 'Ana Kategori', 
    COUNT(sub.Id) as 'Alt Kategori Sayısı'
FROM Categories c
LEFT JOIN Categories sub ON sub.ParentId = c.Id
WHERE c.ParentId IS NULL
GROUP BY c.Name;

-- Tüm kategorileri hiyerarşik göster
SELECT 
    CASE WHEN c.ParentId IS NULL THEN c.Name ELSE '  └─ ' + c.Name END as 'Kategori',
    CASE WHEN c.ParentId IS NULL THEN 'ANA KATEGORİ' ELSE p.Name END as 'Üst Kategori'
FROM Categories c
LEFT JOIN Categories p ON c.ParentId = p.Id
ORDER BY ISNULL(p.Name, c.Name), c.ParentId, c.Name;
```

## 🔧 Frontend Değişiklikleri

### FilterDrawer Props Güncellendi

**Eski:**
```typescript
selectedCategory: string;
onChangeCategory: (category: string) => void;
```

**Yeni:**
```typescript
selectedMainCategory: string;
onChangeMainCategory: (category: string) => void;
selectedServices: string[];
onChangeServices: (services: string[]) => void;
selectedRating: number;
onChangeRating: (rating: number) => void;
showFavoritesOnly: boolean;
onChangeFavoritesOnly: (value: boolean) => void;
```

### Ana Kategori İsimleri Güncellendi

**Eski:**
- Kadın Kuaför
- Erkek Kuaför
- Güzellik Salonu

**Yeni:**
- Erkek Berber
- Bayan Kuaför
- Güzellik Salonu

## 📝 Notlar

- ✅ FreeBarber panelinde sadece Erkek Berber ve Bayan Kuaför görünür
- ✅ Store panelinde tüm kategoriler görünür
- ✅ Kategoriler dinamik olarak API'den yüklenir
- ✅ Alt kategoriler ana kategori seçildiğinde yüklenir
- ✅ Pricing validation düzeltildi (rent/percent)
- ✅ Panel index dosyalarındaki hatalar giderildi

## 🎯 Test Adımları

1. **Backend'i Başlat**
   ```bash
   dotnet run
   ```

2. **SQL'i Çalıştır**
   - Yukarıdaki yöntemlerden birini kullanarak `SeedCategories.sql` dosyasını çalıştır

3. **Kontrol Et**
   ```sql
   SELECT COUNT(*) FROM Categories;
   ```

4. **Frontend'i Test Et**
   - FreeBarber panelinde kategori seçimi
   - Store panelinde kategori seçimi
   - FilterDrawer'daki yeni filtreleri test et

## 🐛 Sorun Giderme

### Hata: "Cannot insert duplicate key"
**Çözüm**: Önce mevcut kategorileri temizleyin
```sql
DELETE FROM Categories;
```

### Hata: "Invalid column name 'ParentId'"
**Çözüm**: Migration'ları çalıştırın
```bash
dotnet ef database update
```

### Frontend'de kategoriler görünmüyor
**Çözüm**: 
1. Backend çalışıyor mu kontrol et
2. API endpoint'lerini test et: `/api/Categories/parents`
3. Browser console'da hata var mı kontrol et

## 📞 İletişim

Sorun yaşarsanız:
1. Backend loglarını kontrol edin
2. Frontend console'u kontrol edin
3. SQL sorgu sonuçlarını kontrol edin

