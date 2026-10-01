using System.Security.Cryptography;
using System.Text;

namespace Handball.Belgium.RefTestManagement.Domain.Security;

public static class TokenService
{
    public static string GenerateHex(int characterCount, bool lowercase = false) =>
        RandomNumberGenerator.GetHexString(characterCount, lowercase);

    public static string GenerateBase64Url(int byteCount)
    {
        var base64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] HashBytes(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static string Hash(string token) =>
        Convert.ToHexString(HashBytes(token));
}
