-- Category Seed Data
-- Bu SQL dosyasını veritabanında çalıştırarak kategorileri ekleyebilirsiniz

-- Önce mevcut kategorileri temizle (isteğe bağlı)
-- DELETE FROM Categories;

-- 1. ERKEK BERBER (Parent)
DECLARE @ErkekBerberId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Categories (Id, Name, ParentId) VALUES (@ErkekBerberId, N'Erkek Berber', NULL);

-- Erkek Berber Alt Kategorileri
INSERT INTO Categories (Id, Name, ParentId) VALUES 
    (NEWID(), N'Döz Kesim', @ErkekBerberId),
    (NEWID(), N'Asimetrik Kesim', @ErkekBerberId),
    (NEWID(), N'Silme Kesim', @ErkekBerberId),
    (NEWID(), N'Kısa Kesim', @ErkekBerberId),
    (NEWID(), N'Uzun Kesim', @ErkekBerberId),
    (NEWID(), N'Düzgün Kesim', @ErkekBerberId),
    (NEWID(), N'Orta Kesim', @ErkekBerberId),
    (NEWID(), N'Fade Kesim', @ErkekBerberId),
    (NEWID(), N'Undercut', @ErkekBerberId),
    (NEWID(), N'Sakal Düzeltme', @ErkekBerberId),
    (NEWID(), N'Sakal Kesim', @ErkekBerberId),
    (NEWID(), N'Bıyık Kesim', @ErkekBerberId),
    (NEWID(), N'Sakal Boyama', @ErkekBerberId),
    (NEWID(), N'Sakal Şekillendirme', @ErkekBerberId),
    (NEWID(), N'Cilt Bakımı', @ErkekBerberId),
    (NEWID(), N'Yüz Masajı', @ErkekBerberId),
    (NEWID(), N'Ağda', @ErkekBerberId);

-- 2. BAYAN KUAFÖR (Parent)
DECLARE @BayanKuaforId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Categories (Id, Name, ParentId) VALUES (@BayanKuaforId, N'Bayan Kuaför', NULL);

-- Bayan Kuaför Alt Kategorileri (Kesim + Boyama)
INSERT INTO Categories (Id, Name, ParentId) VALUES 
    -- Kesim
    (NEWID(), N'Kısa Kesim', @BayanKuaforId),
    (NEWID(), N'Orta Kesim', @BayanKuaforId),
    (NEWID(), N'Uzun Kesim', @BayanKuaforId),
    (NEWID(), N'Wolf Cut', @BayanKuaforId),
    (NEWID(), N'Shag Kesim', @BayanKuaforId),
    (NEWID(), N'Pixie Kesim', @BayanKuaforId),
    (NEWID(), N'Bob Kesim', @BayanKuaforId),
    (NEWID(), N'Lob Kesim', @BayanKuaforId),
    (NEWID(), N'Katmanlı Kesim', @BayanKuaforId),
    (NEWID(), N'Küt Kesim', @BayanKuaforId),
    -- Boyama
    (NEWID(), N'Dip Boya', @BayanKuaforId),
    (NEWID(), N'Komple Boya', @BayanKuaforId),
    (NEWID(), N'Ombre', @BayanKuaforId),
    (NEWID(), N'Sombre', @BayanKuaforId),
    (NEWID(), N'Balayaj', @BayanKuaforId),
    (NEWID(), N'Gölge Boya', @BayanKuaforId),
    (NEWID(), N'Shadow Root', @BayanKuaforId),
    (NEWID(), N'Highlights', @BayanKuaforId),
    (NEWID(), N'Lowlights', @BayanKuaforId),
    (NEWID(), N'Röfle', @BayanKuaforId),
    (NEWID(), N'Platin Boya', @BayanKuaforId),
    (NEWID(), N'Pastel Boya', @BayanKuaforId),
    (NEWID(), N'Fön', @BayanKuaforId),
    (NEWID(), N'Maşa', @BayanKuaforId),
    (NEWID(), N'Düzleştirme', @BayanKuaforId);

-- 3. GÜZELLİK SALONU (Parent)
DECLARE @GuzellikSalonuId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Categories (Id, Name, ParentId) VALUES (@GuzellikSalonuId, N'Güzellik Salonu', NULL);

-- Güzellik Salonu Alt Kategorileri
INSERT INTO Categories (Id, Name, ParentId) VALUES 
    -- Cilt Bakımı
    (NEWID(), N'Cilt Bakımı - Dermabrazyon', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Aloe Vera', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Anti Aging', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Hydrafacial', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Altın Maske', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Nem Bakımı', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Karbon Peeling', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Kimyasal Peeling', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Mikrodermabrazyon', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Dermapen', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - PRP', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Mezoterapi', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Botox', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - Dolgu', @GuzellikSalonuId),
    (NEWID(), N'Cilt Bakımı - İplik', @GuzellikSalonuId),
    
    -- Lazer Epilasyon
    (NEWID(), N'Lazer Epilasyon - Kadın (Tüm Vücut)', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Erkek (Tüm Vücut)', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Yüz', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Kol', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Bacak', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Bikini', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Koltuk Altı', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Sırt', @GuzellikSalonuId),
    (NEWID(), N'Lazer Epilasyon - Göğüs', @GuzellikSalonuId),
    
    -- Kaş
    (NEWID(), N'Kaş - Kaş Alma', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Kaş Boyama', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Kaş Laminasyonu', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Microblading', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Kaş Lifting', @GuzellikSalonuId),
    (NEWID(), N'Kaş - İpek Kirpik', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Kirpik Lifting', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Kirpik Boyama', @GuzellikSalonuId),
    (NEWID(), N'Kaş - Kirpik Perming', @GuzellikSalonuId),
    
    -- Kirpik
    (NEWID(), N'Kirpik - Kirpik Lifting', @GuzellikSalonuId),
    (NEWID(), N'Kirpik - Dipline Kirpik', @GuzellikSalonuId),
    (NEWID(), N'Kirpik - İpek Kirpik', @GuzellikSalonuId),
    (NEWID(), N'Kirpik - Kirpik Boyama', @GuzellikSalonuId),
    (NEWID(), N'Kirpik - Kirpik Serum', @GuzellikSalonuId),
    (NEWID(), N'Kirpik - Kirpik Perming', @GuzellikSalonuId),
    
    -- Dudak
    (NEWID(), N'Dudak - Dudak Dolgu', @GuzellikSalonuId),
    (NEWID(), N'Dudak - Dudak Renklendirme', @GuzellikSalonuId),
    (NEWID(), N'Dudak - Dudak Nemlendir', @GuzellikSalonuId),
    
    -- El Ayak
    (NEWID(), N'El Ayak - Manikür', @GuzellikSalonuId),
    (NEWID(), N'El Ayak - Pedikür', @GuzellikSalonuId),
    (NEWID(), N'El Ayak - Protez Tırnak', @GuzellikSalonuId),
    (NEWID(), N'El Ayak - Jel Tırnak', @GuzellikSalonuId),
    (NEWID(), N'El Ayak - Kalıcı Oje', @GuzellikSalonuId),
    (NEWID(), N'El Ayak - Spa Manikür', @GuzellikSalonuId),
    (NEWID(), N'El Ayak - Spa Pedikür', @GuzellikSalonuId),
    
    -- Masaj
    (NEWID(), N'Masaj - Klasik Masaj', @GuzellikSalonuId),
    (NEWID(), N'Masaj - Aromaterapi Masajı', @GuzellikSalonuId),
    (NEWID(), N'Masaj - Taş Terapisi', @GuzellikSalonuId),
    (NEWID(), N'Masaj - Lenfdrene Masajı', @GuzellikSalonuId),
    (NEWID(), N'Masaj - Refleksoloji', @GuzellikSalonuId),
    (NEWID(), N'Masaj - Sıcak Taş Masajı', @GuzellikSalonuId),
    
    -- Saç Bakımı
    (NEWID(), N'Saç Bakımı - Keratin', @GuzellikSalonuId),
    (NEWID(), N'Saç Bakımı - Botox', @GuzellikSalonuId),
    (NEWID(), N'Saç Bakımı - Protein Bakımı', @GuzellikSalonuId),
    (NEWID(), N'Saç Bakımı - Onarım Bakımı', @GuzellikSalonuId),
    (NEWID(), N'Saç Bakımı - Nem Bakımı', @GuzellikSalonuId),
    (NEWID(), N'Saç Bakımı - Saç Maskesi', @GuzellikSalonuId),
    (NEWID(), N'Saç Bakımı - Saç Spa', @GuzellikSalonuId),
    
    -- Vücut Bakımı
    (NEWID(), N'Vücut Bakımı - Peeling', @GuzellikSalonuId),
    (NEWID(), N'Vücut Bakımı - Kese', @GuzellikSalonuId),
    (NEWID(), N'Vücut Bakımı - Parafin', @GuzellikSalonuId),
    (NEWID(), N'Vücut Bakımı - Çamur Banyosu', @GuzellikSalonuId),
    (NEWID(), N'Vücut Bakımı - Deniz Yosunu', @GuzellikSalonuId),
    
    -- Epilasyon
    (NEWID(), N'Epilasyon - Ağda', @GuzellikSalonuId),
    (NEWID(), N'Epilasyon - İpek İplik', @GuzellikSalonuId),
    (NEWID(), N'Epilasyon - Sir Ağda', @GuzellikSalonuId),
    
    -- Solaryum
    (NEWID(), N'Solaryum - Tüm Vücut', @GuzellikSalonuId),
    (NEWID(), N'Solaryum - Kısmi', @GuzellikSalonuId);

PRINT 'Kategoriler başarıyla eklendi!';
PRINT 'Toplam: 3 ana kategori (Erkek Berber, Bayan Kuaför, Güzellik Salonu) + 100+ alt kategori';

