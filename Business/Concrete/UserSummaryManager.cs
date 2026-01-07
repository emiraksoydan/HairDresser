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
                    RoleHint = "freebarber",
                    CustomerNumber = u.CustomerNumber
                });
            }
        }

        return new SuccessDataResult<UserNotifyDto?>(new UserNotifyDto
        {
            UserId = u.Id,
            DisplayName = BuildName(u.FirstName, u.LastName, "Kullanıcı"),
            AvatarUrl = await TryGetUserAvatarAsync(u.Id),
            RoleHint = "user",
            CustomerNumber = u.CustomerNumber
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
        // Performance: Dictionary kullanarak O(1) lookup
        var freeBarberDict = freeBarbers.ToDictionary(f => f.FreeBarberUserId);
        
        // Her owner için en son eklenen image'i al (GetLatestImageAsync kullanarak)
        var imageLookup = new Dictionary<Guid, string?>();
        foreach (var u in users)
        {
            if (freeBarberDict.TryGetValue(u.Id, out var fbDetail))
            {
                // FreeBarber panel image'ı
                var image = await imageDal.GetLatestImageAsync(fbDetail.Id, ImageOwnerType.FreeBarber);
                if (image != null)
                {
                    imageLookup[fbDetail.Id] = image.ImageUrl;
                }
            }
            else
            {
                // User image'ı
                var image = await imageDal.GetLatestImageAsync(u.Id, ImageOwnerType.User);
                if (image != null)
                {
                    imageLookup[u.Id] = image.ImageUrl;
                }
            }
        }

        foreach (var u in users)
        {
            freeBarberDict.TryGetValue(u.Id, out var fbDetail);

            if (fbDetail is not null)
            {
                dict[u.Id] = new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(fbDetail.FirstName, fbDetail.LastName, "Serbest Berber"),
                    AvatarUrl = imageLookup.TryGetValue(fbDetail.Id, out var fbImgUrl) ? fbImgUrl : null,
                    RoleHint = "freebarber",
                    CustomerNumber = u.CustomerNumber
                };
            }
            else
            {
                dict[u.Id] = new UserNotifyDto
                {
                    UserId = u.Id,
                    DisplayName = BuildName(u.FirstName, u.LastName, "Kullanıcı"),
                    AvatarUrl = imageLookup.TryGetValue(u.Id, out var userImgUrl) ? userImgUrl : null,
                    RoleHint = "user",
                    CustomerNumber = u.CustomerNumber
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
        var image = await imageDal.GetLatestImageAsync(userId, ImageOwnerType.User);
        return image?.ImageUrl;
    }

    private async Task<string?> TryGetFreeBarberAvatarAsync(Guid freeBarberPanelId)
    {
        var image = await imageDal.GetLatestImageAsync(freeBarberPanelId, ImageOwnerType.FreeBarber);
        return image?.ImageUrl;
    }
}
