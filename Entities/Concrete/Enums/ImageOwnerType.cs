using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Enums
{
    public enum ImageOwnerType
    {
        User = 1,        // Only for profile images (User.ImageId)
        Store = 2,
        ManuelBarber = 3,
        FreeBarber = 4
    }
}
