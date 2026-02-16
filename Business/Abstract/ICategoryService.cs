using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface ICategoryService
    {
        Task<IDataResult<List<Category>>> GetAllCategories();
        Task<IDataResult<List<Category>>> GetParentCategories();
        Task<IDataResult<List<Category>>> GetChildCategories(Guid parentId);
        Task<IDataResult<List<CategoryHierarchyDto>>> GetCategoryHierarchyAsync();
        Task<IResult> AddCategory(Category category);
        Task<IResult> DeleteCategory(Guid id);
    }
}
