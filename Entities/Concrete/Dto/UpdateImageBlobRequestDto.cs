using Entities.Abstract;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class UpdateImageBlobRequestDto : IDto
    {
        public Guid ImageId { get; set; }
        public IFormFile File { get; set; }
    }
}
