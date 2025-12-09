using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

public class UserSummaryManager(
    IUserDal userDal,
    IFreeBarberDal freeBarberDal,
    IImageDal imageDal
) : IUserSummaryService
{
    public async Task<IDataResult<UserNotifyDto?>> TryGetAsync(Guid userId)
    {
        var u = await userDal.Get(x => x.Id == userId);

        if (u is null) return new SuccessDataResult<UserNotifyDto?>((UserNotifyDto?)null);

        // Eğer kullanıcı FreeBarber ise, detayları FreeBarber tablosundan alalım
        if (u.UserType == UserType.FreeBarber)
        {
            var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
            if (fb is not null)
            {
                return new SuccessDataResult<UserNotifyDto?>(new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(fb.FirstName, fb.LastName, "Serbest Berber"),
                    AvatarUrl = await TryGetFreeBarberAvatarAsync(fb.Id), // Berber fotoları
                    RoleHint = "freebarber"
                });
            }
        }

        return new SuccessDataResult<UserNotifyDto?>(new UserNotifyDto
        {
            UserId = u.Id,
            DisplayName = BuildName(u.FirstName, u.LastName, "Kullanıcı"),
            AvatarUrl = await TryGetUserAvatarAsync(u.Id),
            RoleHint = "user"
        });
    }

    public async Task<IDataResult<Dictionary<Guid, UserNotifyDto>>> GetManyAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var dict = new Dictionary<Guid, UserNotifyDto>();
        var users = await userDal.GetAll(u => ids.Contains(u.Id));

        var freeBarberUserIds = users
            .Where(u => u.UserType == UserType.FreeBarber)
            .Select(u => u.Id)
            .ToList();

        List<FreeBarber> freeBarbers = new();
        if (freeBarberUserIds.Count > 0)
        {
            freeBarbers = await freeBarberDal.GetAll(fb => freeBarberUserIds.Contains(fb.FreeBarberUserId));
        }
        var imageOwnerIds = new HashSet<Guid>();
        foreach (var u in users)
        {
            var fbDetail = freeBarbers.FirstOrDefault(f => f.FreeBarberUserId == u.Id);
            if (fbDetail != null)
            {
                imageOwnerIds.Add(fbDetail.Id); 
            }
            else
            {
                imageOwnerIds.Add(u.Id);
            }
        }
        var allImages = await imageDal.GetAll(img => imageOwnerIds.Contains(img.ImageOwnerId));
        var imageLookup = allImages
            .GroupBy(img => img.ImageOwnerId) 
            .ToDictionary(
                g => g.Key, 
                g => g.OrderByDescending(img => img.CreatedAt).First().ImageUrl // Value = En son resmin URL'i
            );

        string? GetAvatarUrlFromCache(Guid ownerId) =>
            imageLookup.TryGetValue(ownerId, out var url) ? url : null;
        foreach (var u in users)
        {
            var fbDetail = freeBarbers.FirstOrDefault(f => f.FreeBarberUserId == u.Id);

            if (fbDetail is not null)
            {
                dict[u.Id] = new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(fbDetail.FirstName, fbDetail.LastName, "Serbest Berber"),
                    AvatarUrl = GetAvatarUrlFromCache(fbDetail.Id),
                    RoleHint = "freebarber"
                };
            }
            else
            {
                dict[u.Id] = new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(u.FirstName, u.LastName, "Kullanıcı"),
                    AvatarUrl = GetAvatarUrlFromCache(u.Id),
                    RoleHint = "user"
                };
            }
        }

        return new SuccessDataResult<Dictionary<Guid, UserNotifyDto>>(dict);
    }

    private static string BuildName(string? first, string? last, string fallback)
    {
        var full = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(full) ? fallback : full;
    }

    private async Task<string?> TryGetUserAvatarAsync(Guid userId)
    {
        var imgs = await imageDal.GetAll(x => x.ImageOwnerId == userId);
        return imgs.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.ImageUrl;
    }

    private async Task<string?> TryGetFreeBarberAvatarAsync(Guid freeBarberPanelId)
    {
        var imgs = await imageDal.GetAll(x => x.ImageOwnerId == freeBarberPanelId);
        return imgs.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.ImageUrl;
    }
}
