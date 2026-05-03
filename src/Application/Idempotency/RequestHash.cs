using System.Security.Cryptography;
using System.Text;

namespace CaseGig.Application.Idempotency;

public static class RequestHash
{
    public static string ComputeSha256Hex(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

