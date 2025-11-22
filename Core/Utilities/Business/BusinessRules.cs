using Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Business
{
    public class BusinessRules
    {
        public static IResult Run(params IResult[] logics)
        {
            foreach (var result in logics)
            {
                if (!result.Success)
                {
                    return result;
                }
            }
            return null;
        }

        public static async Task<IResult?> RunAsync(params Func<Task<IResult>>[] rules)
        {
            foreach (var ruleFunc in rules)
            {
                var result = await ruleFunc();   // kuralı çalıştır
                if (result != null && !result.Success)
                    return result;              // ilk hata döneni geri ver
            }

            return null;                        // hepsi başarılıysa null
        }
    }
}
