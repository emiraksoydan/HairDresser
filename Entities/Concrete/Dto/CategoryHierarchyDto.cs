using Entities.Abstract;
using System;
using System.Collections.Generic;

namespace Entities.Concrete.Dto
{
    public class CategoryHierarchyDto : IDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<CategoryHierarchyDto> Children { get; set; } = new();
    }
}
