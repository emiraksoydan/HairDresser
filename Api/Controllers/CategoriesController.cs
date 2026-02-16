using Business.Abstract;
using Entities.Concrete.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class CategoriesController : BaseApiController
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] Category category)
        {
            return await HandleResultAsync(_categoryService.AddCategory(category));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleResultAsync(_categoryService.DeleteCategory(id));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCategory()
        {
            return await HandleDataResultAsync(_categoryService.GetAllCategories());
        }

        [HttpGet("parents")]
        public async Task<IActionResult> GetParentCategories()
        {
            return await HandleDataResultAsync(_categoryService.GetParentCategories());
        }

        [HttpGet("children/{parentId}")]
        public async Task<IActionResult> GetChildCategories(Guid parentId)
        {
            return await HandleDataResultAsync(_categoryService.GetChildCategories(parentId));
        }

        [HttpGet("hierarchy")]
        public async Task<IActionResult> GetCategoryHierarchy()
        {
            return await HandleDataResultAsync(_categoryService.GetCategoryHierarchyAsync());
        }
    }
}
