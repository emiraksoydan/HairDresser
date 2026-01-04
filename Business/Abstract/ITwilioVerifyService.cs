using Core.Utilities.Results;
using System;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface ITwilioVerifyService
    {
        Task<IResult> SendAsync(string e164);
        Task<IResult> CheckAsync(string e164, string code);
    }
}
