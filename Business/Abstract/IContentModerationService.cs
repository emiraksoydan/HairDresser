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
    }
}
