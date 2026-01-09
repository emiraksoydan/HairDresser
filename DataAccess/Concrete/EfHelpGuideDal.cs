using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfHelpGuideDal : EfEntityRepositoryBase<HelpGuide, DatabaseContext>, IHelpGuideDal
    {
        private readonly DatabaseContext _context;

        public EfHelpGuideDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<HelpGuide>> GetByUserTypeAsync(int userType)
        {
            return await _context.HelpGuides
                .Where(hg => hg.UserType == userType)
                .OrderBy(hg => hg.Order)
                .ThenBy(hg => hg.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<HelpGuide>> GetActiveByUserTypeAsync(int userType)
        {
            return await _context.HelpGuides
                .Where(hg => hg.UserType == userType && hg.IsActive)
                .OrderBy(hg => hg.Order)
                .ThenBy(hg => hg.CreatedAt)
                .ToListAsync();
        }
    }
}
