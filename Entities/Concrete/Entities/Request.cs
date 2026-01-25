using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    /// <summary>
    /// Kullanıcı istekleri - gumusmakastr@gmail.com adresine mail gönderilecek
    /// </summary>
    public class Request : IEntity
    {
        public Guid Id { get; set; }
        public Guid RequestFromUserId { get; set; }
        public string RequestTitle { get; set; } = string.Empty;
        public string RequestMessage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsProcessed { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
