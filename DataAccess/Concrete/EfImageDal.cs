using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfImageDal : EfEntityRepositoryBase<Image, DatabaseContext>, IImageDal
    {
        public EfImageDal(DatabaseContext context) : base(context)
        {
        }

        public async Task<Image?> GetLatestImageAsync(Guid ownerId, ImageOwnerType ownerType)
        {
            return await Context.Images
                .AsNoTracking()
                .Where(x => x.ImageOwnerId == ownerId && x.OwnerType == ownerType)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}
