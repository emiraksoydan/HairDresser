using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class SettingManager : ISettingService
    {
        private readonly ISettingDal _settingDal;
        private readonly IUserDal _userDal;

        public SettingManager(ISettingDal settingDal, IUserDal userDal)
        {
            _settingDal = settingDal;
            _userDal = userDal;
        }

        public async Task<IDataResult<SettingGetDto>> GetByUserIdAsync(Guid userId)
        {
            var setting = await _settingDal.GetByUserIdAsync(userId);
            if (setting == null)
            {
                // Varsayılan ayarları oluştur
                await InitializeDefaultAsync(userId);
                setting = await _settingDal.GetByUserIdAsync(userId);
            }

            var dto = new SettingGetDto
            {
                Id = setting.Id,
                UserId = setting.UserId,
                ShowImageAnimation = setting.ShowImageAnimation
                // NotificationSoundUrl kaldırıldı - artık backend'deki varsayılan ses dosyası kullanılıyor
            };

            return new SuccessDataResult<SettingGetDto>(dto);
        }

        public async Task<IResult> UpdateAsync(Guid userId, SettingUpdateDto dto)
        {
            var setting = await _settingDal.GetByUserIdAsync(userId);
            if (setting == null)
            {
                // Varsayılan ayarları oluştur
                await InitializeDefaultAsync(userId);
                setting = await _settingDal.GetByUserIdAsync(userId);
            }

            setting.ShowImageAnimation = dto.ShowImageAnimation;
            // NotificationSoundUrl güncellenmez - kullanıcı ayarlardan seçemez, backend'deki varsayılan ses kullanılır
            setting.UpdatedAt = DateTime.UtcNow;

            await _settingDal.Update(setting);

            return new SuccessResult("Ayarlar başarıyla güncellendi.");
        }

        public async Task<IResult> InitializeDefaultAsync(Guid userId)
        {
            // Zaten varsa oluşturma
            var existing = await _settingDal.GetByUserIdAsync(userId);
            if (existing != null)
            {
                return new SuccessResult("Ayarlar zaten mevcut.");
            }

            // FIX: UserId foreign key constraint validation
            // Settings tablosu User tablosuna foreign key ile bağlı
            // User varlığını kontrol et, yoksa error döndür
            var user = await _userDal.Get(u => u.Id == userId);
            if (user == null)
            {
                return new ErrorResult($"Kullanıcı bulunamadı. UserId: {userId}");
            }
            
            var setting = new Setting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShowImageAnimation = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _settingDal.Add(setting);

            return new SuccessResult("Varsayılan ayarlar oluşturuldu.");
        }
    }
}
