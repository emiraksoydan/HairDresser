using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfServiceOfferingDal : EfEntityRepositoryBase<ServiceOffering, DatabaseContext>, IServiceOfferingDal
    {
        private readonly DatabaseContext _context;
        public EfServiceOfferingDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<ServiceOfferingGetDto>> GetServiceOfferingsByIdAsync(Guid Id)
        {
            var offerings = await _context.ServiceOfferings
               .Where(s => s.OwnerId == Id)
               .Select(s => new ServiceOfferingGetDto
               {
                   Id = s.Id,
                   Price = s.Price,
                   ServiceName = s.ServiceName
               }).ToListAsync();

            return offerings;
        }
        public async Task<List<ServiceOffering>> GetServiceOfferingsByIdsAsync(IEnumerable<Guid> ids)
        {
            return await _context.ServiceOfferings
                .Where(o => ids.Contains(o.Id))
                .ToListAsync();
        }


    }
}
