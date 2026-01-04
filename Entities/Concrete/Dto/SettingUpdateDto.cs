using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class SettingUpdateDto : IDto
    {
        public bool ShowImageAnimation { get; set; }
    }
}

