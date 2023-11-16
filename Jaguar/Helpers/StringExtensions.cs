using Jaguar.Core;

namespace Jaguar.Helpers;

public static class StringExtensions
{
    public static byte[] ToBytes(this string value)
    {
        return Server.Encoding.GetBytes(value);
    }
}