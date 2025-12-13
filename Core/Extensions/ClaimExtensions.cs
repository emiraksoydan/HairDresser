using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Entities.Concrete.Enums;

namespace Core.Extensions
{
    public static class ClaimExtensions
    {
        public static void AddEmail(this ICollection<Claim> claims, string email)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        }

        public static void AddLastName(this ICollection<Claim> claims, string lastName)
        {
            claims.Add(new Claim("lastName", lastName));
        }

        public static void AddName(this ICollection<Claim> claims, string name)
        {
            claims.Add(new Claim("name", name));
        }
        public static void AddNameIdentifier(this ICollection<Claim> claims, string nameIdentifier)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        }

        public static void AddRoles(this ICollection<Claim> claims, string[] roles)
        {
            roles.ToList().ForEach(role => claims.Add(new Claim(ClaimTypes.Role, role)));
        }
        public static void AddUserType(this ICollection<Claim> claims, UserType userType)
        {
            // Enum.ToString() "Customer", "FreeBarber", "BarberStore" döndürür
            var userTypeString = userType.ToString();
            claims.Add(new Claim("userType", userTypeString));
            
            // Debug: claim'in eklendiğini doğrula
            Debug.WriteLine($"[ClaimExtensions] Added userType claim: \"userType\" = \"{userTypeString}\"");
        }
      
    }
}
