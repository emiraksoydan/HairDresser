using System;
using System.Collections.Generic;
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

        public static void AddFullName(this ICollection<Claim> claims, string fullName)
        {
            claims.Add(new Claim("fullName", fullName));
        }

        public static void AddName(this ICollection<Claim> claims, string name)
        {
            claims.Add(new Claim("name", name));
        }
        public static void AddNameIdentifier(this ICollection<Claim> claims, string nameIdentifier)
        {
            claims.Add(new Claim("identifier", nameIdentifier));
        }

        public static void AddRoles(this ICollection<Claim> claims, string[] roles)
        {
            roles.ToList().ForEach(role => claims.Add(new Claim(ClaimTypes.Role, role)));
        }
        public static void AddUserType(this ICollection<Claim> claims, UserType userType)
        {
            claims.Add(new Claim("userType", userType.ToString()));
        }
    }
}
