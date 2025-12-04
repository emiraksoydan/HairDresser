using Business.Abstract;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class UserSummaryManager(
        IUserDal userDal,              // senin ana user tablon
        IFreeBarberDal freeBarberDal,  // FreeBarber tablon
        IImageDal imageDal             // varsa: kullanıcı avatarı / store foto vs.
    ) : IUserSummaryService
    {
        public async Task<UserNotifyDto?> GetAsync(Guid userId)
        {
            // 1) Normal user
            var u = await userDal.Get(x => x.Id == userId);
            if (u is not null)
            {
                return new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(u.FirstName, u.LastName),
                    AvatarUrl = await TryGetUserAvatarAsync(u.Id),
                    RoleHint = "user"
                };
            }

            // 2) FreeBarber fallback (FreeBarberUserId ile eşleşir)
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
            if (fb is not null)
            {
                return new UserNotifyDto
                {
                    UserId = fb.FreeBarberUserId, // dikkat: payload'larda hep "userId" mantığıyla kalsın
                    DisplayName = BuildName(fb.FirstName, fb.LastName, fallback: "Serbest Berber"),
                    AvatarUrl = await TryGetFreeBarberAvatarAsync(fb.Id), // veya fb.BarberCertificate vs.
                    RoleHint = "freebarber",
                };
            }

            // Bulunamadı
            return null;
        }

        public async Task<Dictionary<Guid, UserNotifyDto>> GetManyAsync(IEnumerable<Guid> userIds)
        {
            var ids = userIds.Distinct().ToList();
            var dict = new Dictionary<Guid, UserNotifyDto>();

            // Toplu almak istersen DAL’da GetAll + Contains kullan
            var users = await userDal.GetAll(u => ids.Contains(u.Id));
            foreach (var u in users)
            {
                dict[u.Id] = new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(u.FirstName, u.LastName),
                    AvatarUrl = await TryGetUserAvatarAsync(u.Id),
                    RoleHint = "user"
                };
            }

            // Users’da bulunmayanları FreeBarber’dan tamamla
            var missing = ids.Where(id => !dict.ContainsKey(id)).ToList();
            if (missing.Count > 0)
            {
                var fbs = await freeBarberDal.GetAll(f => missing.Contains(f.FreeBarberUserId));
                foreach (var fb in fbs)
                {
                    dict[fb.FreeBarberUserId] = new UserNotifyDto
                    {
                        UserId = fb.FreeBarberUserId,
                        DisplayName = BuildName(fb.FirstName, fb.LastName, fallback: "Serbest Berber"),
                        AvatarUrl = await TryGetFreeBarberAvatarAsync(fb.Id),
                        RoleHint = "freebarber",
                    };
                }
            }

            return dict;
        }

        private static string BuildName(string? first, string? last, string? fallback = null )
        {
            var full = $"{first} {last}".Trim();
            return string.IsNullOrWhiteSpace(full) ? (fallback ?? "") : full;

        }

        // Bunlar sende image modeli nasıl bilmiyorum diye "örnek"
        private async Task<string?> TryGetUserAvatarAsync(Guid userId)
        {
            var imgs = await imageDal.GetAll(x => x.ImageOwnerId == userId);
            return imgs.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.ImageUrl;
        }

        private async Task<string?> TryGetFreeBarberAvatarAsync(Guid freeBarberId)
        {
            // FreeBarber’ın image owner’ı farklıysa burayı uyarlarsın
            var imgs = await imageDal.GetAll(x => x.ImageOwnerId == freeBarberId);
            return imgs.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.ImageUrl;
        }
    }
}
