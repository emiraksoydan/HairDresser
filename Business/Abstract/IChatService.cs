using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IChatService
    {
        // Randevu thread'i için mesaj gönderme (AppointmentId ile)
        Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text);
        
        // Favori thread için mesaj gönderme (ThreadId ile)
        Task<IDataResult<ChatMessageDto>> SendFavoriteMessageAsync(Guid senderUserId, Guid threadId, string text);
        
        // Thread okundu işaretleme (ThreadId ile - hem randevu hem favori için)
        Task<IDataResult<bool>> MarkThreadReadAsync(Guid userId, Guid threadId);
        
        // Randevu thread'i için okundu işaretleme (geriye dönük uyumluluk için)
        Task<IDataResult<bool>> MarkThreadReadByAppointmentAsync(Guid userId, Guid appointmentId);

        Task<IDataResult<List<ChatThreadListItemDto>>> GetThreadsAsync(Guid userId);
        
        // Mesajları getir (ThreadId ile - hem randevu hem favori için)
        Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesByThreadAsync(Guid userId, Guid threadId, DateTime? beforeUtc);
        
        // Randevu thread'i için mesajları getir (geriye dönük uyumluluk için)
        Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesAsync(Guid userId, Guid appointmentId, DateTime? beforeUtc);

        Task<IDataResult<int>> GetUnreadTotalAsync(Guid userId);
        
        // Favori thread oluştur veya güncelle
        Task<IDataResult<Guid>> EnsureFavoriteThreadAsync(Guid fromUserId, Guid toUserId);
        
        // Typing indicator gönder
        Task<IDataResult<bool>> NotifyTypingAsync(Guid userId, Guid threadId, bool isTyping);
    }
}
