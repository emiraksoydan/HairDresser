using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.Hashing
{
    public class HashingHelper
    {
        public static void CreateHash(string value,out byte[] valueHash,out byte[] valueSalt)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                valueSalt = hmac.Key;
                valueHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        public static bool verifyValueHash(string value, byte[] valueHash, byte[] valueSalt) {
            using (var hmac = new System.Security.Cryptography.HMACSHA512(valueSalt))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != valueHash[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
