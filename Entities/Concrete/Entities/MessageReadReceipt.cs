using Entities.Abstract;
using System;

namespace Entities.Concrete.Entities
{
    public class MessageReadReceipt : IEntity
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }   // FK -> ChatMessage.Id
        public Guid ThreadId { get; set; }    // Denormalized for efficient thread-level queries
        public Guid UserId { get; set; }      // Who read it
        public DateTime ReadAt { get; set; }
    }
}
