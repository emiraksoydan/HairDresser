using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class HelpGuideGetDto : IDto
    {
        public Guid Id { get; set; }
        public int UserType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }
}
