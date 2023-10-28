namespace Jaguar.Extensions;

internal static class StringExtensions
{
    internal static IEnumerable<string> ChunksUpTo(this string str, int maxChunkSize)
    {
        for (var i = 0; i < str.Length; i += maxChunkSize)
        {
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }
    }
}