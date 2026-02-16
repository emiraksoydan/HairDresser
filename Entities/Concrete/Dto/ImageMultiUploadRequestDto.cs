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
    public class ImageMultiUploadRequestDto : IDto
    {
        public List<IFormFile> Files { get; set; } = new();
        public ImageOwnerType OwnerType { get; set; }
        public Guid OwnerId { get; set; }
    }
}
