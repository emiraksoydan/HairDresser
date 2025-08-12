using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using MapsterMapper;

namespace Business.Concrete
{
    public class CategoryManager(ICategoriesDal categoriesDal) : ICategoryService
    {
        public async Task<IResult> AddCategory(Category category)
        {
            await categoriesDal.Add(category);
            return new SuccessResult("Kategori Eklendi");
        }

        public async Task<IResult> DeleteCategory(Guid id)
        {
            var getCat = await categoriesDal.Get(x => x.Id == id);
            await categoriesDal.Remove(getCat);
            return new SuccessResult("Kategori Silindi");
        }

        public async Task<IDataResult<List<Category>>> GetAllCategories()
        {
            var categories = await categoriesDal.GetAll();
            return new SuccessDataResult<List<Category>>(categories);
        }
    }
}
