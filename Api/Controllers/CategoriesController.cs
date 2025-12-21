using Business.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] Category category)
        {
            var result = await _categoryService.AddCategory(category);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _categoryService.DeleteCategory(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetAllCategory()
        {
            var result = await _categoryService.GetAllCategories();
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpGet("parents")]
        public async Task<IActionResult> GetParentCategories()
        {
            var result = await _categoryService.GetParentCategories();
            return result.Success ? Ok(result) : NotFound(result);
        }

        [HttpGet("children/{parentId}")]
        public async Task<IActionResult> GetChildCategories(Guid parentId)
        {
            var result = await _categoryService.GetChildCategories(parentId);
            return result.Success ? Ok(result) : NotFound(result);
        }

    }
}
