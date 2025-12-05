using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;

public class UserSummaryManager(
    IUserDal userDal,
    IFreeBarberDal freeBarberDal,
    IImageDal imageDal
) : IUserSummaryService
{
    public async Task<IDataResult<UserNotifyDto?>> TryGetAsync(Guid userId)
    {
        var u = await userDal.Get(x => x.Id == userId);
        if (u is not null)
        {
            return new SuccessDataResult<UserNotifyDto?>(new UserNotifyDto
            {
                UserId = u.Id,
                DisplayName = BuildName(u.FirstName, u.LastName, "Kullanıcı"),
                AvatarUrl = await TryGetUserAvatarAsync(u.Id),
                RoleHint = "user",
            });
        }

        var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == userId);
        if (fb is not null)
        {
            return new SuccessDataResult<UserNotifyDto?>(new UserNotifyDto
            {
                UserId = fb.FreeBarberUserId, // payload’larda userId mantığı
                DisplayName = BuildName(fb.FirstName, fb.LastName, "Serbest Berber"),
                AvatarUrl = await TryGetFreeBarberAvatarAsync(fb.Id), // 🔥 ImageOwnerId = panelId
                RoleHint = "freebarber",
            });
        }

        // Ambiguous ctor hatasını önlemek için:
        return new SuccessDataResult<UserNotifyDto?>((UserNotifyDto?)null);
    }

    public async Task<IDataResult<Dictionary<Guid, UserNotifyDto>>> GetManyAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var dict = new Dictionary<Guid, UserNotifyDto>();

        var users = await userDal.GetAll(u => ids.Contains(u.Id));
        foreach (var u in users)
        {
            dict[u.Id] = new UserNotifyDto
            {
                UserId = u.Id,
                DisplayName = BuildName(u.FirstName, u.LastName, "Kullanıcı"),
                AvatarUrl = await TryGetUserAvatarAsync(u.Id),
                RoleHint = "user",
            };
        }

        var missing = ids.Where(id => !dict.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var fbs = await freeBarberDal.GetAll(f => missing.Contains(f.FreeBarberUserId));
            foreach (var fb in fbs)
            {
                dict[fb.FreeBarberUserId] = new UserNotifyDto
                {
                    UserId = fb.FreeBarberUserId,
                    DisplayName = BuildName(fb.FirstName, fb.LastName, "Serbest Berber"),
                    AvatarUrl = await TryGetFreeBarberAvatarAsync(fb.Id), // 🔥 panelId
                    RoleHint = "freebarber",
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
