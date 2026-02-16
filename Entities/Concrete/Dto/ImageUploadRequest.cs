using Entities.Abstract;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ImageUploadRequest : IDto
    {
        public IFormFile File { get; set; } = default!;
        public ImageOwnerType OwnerType { get; set; }
        public Guid OwnerId { get; set; }
        public bool IsProfileImage { get; set; } = true;
    }
}
