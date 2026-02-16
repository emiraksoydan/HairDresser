using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IMessageReadReceiptDal : IEntityRepository<MessageReadReceipt>
    {
        /// <summary>
        /// Thread'deki okunmamış mesajları (userId tarafından gönderilmemiş ve henüz okunmamış)
        /// okundu olarak işaretler. Yeni eklenen receipt ID'lerini döner.
        /// </summary>
        Task<List<Guid>> MarkThreadMessagesReadAsync(Guid threadId, Guid userId);

        /// <summary>
        /// Verilen mesajlardan tüm katılımcılar tarafından okunmuş olanları döner.
        /// Bir mesaj "tam okundu" sayılır; gönderici dışındaki tüm katılımcılar okuduysa.
        /// </summary>
        Task<HashSet<Guid>> GetFullyReadMessageIdsAsync(Guid threadId, List<Guid> allParticipantIds);
    }
}
