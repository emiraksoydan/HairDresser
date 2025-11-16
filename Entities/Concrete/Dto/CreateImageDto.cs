using Entities.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class CreateImageDto : IDto
    {

        public string ImageUrl { get; set; }

    }
}

