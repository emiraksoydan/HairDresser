using Core.Utilities.Results;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IContentModerationService
    {
        /// <summary>
        /// Verilen metni OpenAI Moderation API ile kontrol eder.
        /// </summary>
        /// <param name="text">Kontrol edilecek metin</param>
        /// <returns>Metin uygunsuzsa hata mesajı, uygunsa başarı döner</returns>
        Task<IResult> CheckContentAsync(string text);

        /// <summary>
        /// Birden fazla metni kontrol eder.
        /// </summary>
        Task<IResult> CheckContentsAsync(params string[] texts);

        /// <summary>
        /// Verilen görseli OpenAI Moderation API (omni-moderation-latest) ile kontrol eder.
        /// Uygunsuz içerik (cinsel, şiddet, nefret vb.) tespit edilirse hata döner.
        /// </summary>
        /// <param name="file">Kontrol edilecek görsel dosyası</param>
        /// <returns>Görsel uygunsuzsa hata mesajı, uygunsa başarı döner</returns>
        Task<IResult> CheckImageContentAsync(Microsoft.AspNetCore.Http.IFormFile file);

        /// <summary>
        /// Verilen görsel byte dizisini OpenAI Moderation API ile kontrol eder.
        /// Background task'lardan IFormFile kullanılamadığı durumlarda kullanılır.
        /// </summary>
        Task<IResult> CheckImageContentAsync(byte[] imageBytes, string contentType, string fileName = "image");
    }
}
