using System.Security.Cryptography;
using System.Text;

namespace LCDPC.Infrastructure.Users.Auth;

public static class TokenHashing
{
    public static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}