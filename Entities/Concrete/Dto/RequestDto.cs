using System;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// İstek oluşturma DTO - gumusmakastr@gmail.com adresine mail gönderilecek
    /// </summary>
    public class CreateRequestDto
    {
        public string RequestTitle { get; set; } = string.Empty;
        public string RequestMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// İstek görüntüleme DTO
    /// </summary>
    public class RequestGetDto
    {
        public Guid Id { get; set; }
        public Guid RequestFromUserId { get; set; }
        public string RequestTitle { get; set; } = string.Empty;
        public string RequestMessage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsProcessed { get; set; }
    }
}
