using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using MapsterMapper;

namespace Business.Concrete
{
    public class CategoryManager(ICategoriesDal categoriesDal) : ICategoryService
    {
        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> AddCategory(Category category)
        {
            await categoriesDal.Add(category);
            return new SuccessResult(Messages.CategoryAddedSuccess);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IResult> DeleteCategory(Guid id)
        {
            var getCat = await categoriesDal.Get(x => x.Id == id);
            await categoriesDal.Remove(getCat);
            return new SuccessResult(Messages.CategoryDeletedSuccess);
        }

        public async Task<IDataResult<List<Category>>> GetAllCategories()
        {
            var categories = await categoriesDal.GetAll();
            return new SuccessDataResult<List<Category>>(categories);
        }

        public async Task<IDataResult<List<Category>>> GetParentCategories()
        {
            var categories = await categoriesDal.GetAll(x => x.ParentId == null);
            return new SuccessDataResult<List<Category>>(categories, Messages.MainCategoriesRetrieved);
        }

        public async Task<IDataResult<List<Category>>> GetChildCategories(Guid parentId)
        {
            var categories = await categoriesDal.GetAll(x => x.ParentId == parentId);
            return new SuccessDataResult<List<Category>>(categories, Messages.SubCategoriesRetrieved);
        }

        public async Task<IDataResult<List<CategoryHierarchyDto>>> GetCategoryHierarchyAsync()
        {
            var allCategories = await categoriesDal.GetAll();
            var hierarchy = BuildHierarchy(allCategories);
            return new SuccessDataResult<List<CategoryHierarchyDto>>(hierarchy, Messages.CategoriesRetrieved);
        }

        private List<CategoryHierarchyDto> BuildHierarchy(List<Category> categories)
        {
            var lookup = categories.ToLookup(c => c.ParentId);

            List<CategoryHierarchyDto> BuildLevel(Guid? parentId)
            {
                return lookup[parentId].Select(c => new CategoryHierarchyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Children = BuildLevel(c.Id)
                }).ToList();
            }

            return BuildLevel(null);
        }
    }
}
