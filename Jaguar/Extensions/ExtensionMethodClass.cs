using System.Net;

namespace Jaguar.Extensions;

internal static class IpEndPointExtension
{
    internal static string ConvertToKey(this IPEndPoint? ipEndPoint) => ipEndPoint == null ? string.Empty : $"{ipEndPoint.Address}:{ipEndPoint.Port}";

    internal static string ToReadableString(this TimeSpan span)
    {
        var formatted =
            $"{(span.Duration().Days > 0 ? $"{span.Days:0}d{(span.Days == 1 ? string.Empty : "s")}:" : string.Empty)}{(span.Duration().Hours > 0 ? $"{span.Hours:0}h{(span.Hours == 1 ? string.Empty : "s")}:" : string.Empty)}{(span.Duration().Minutes > 0 ? $"{span.Minutes:0}m{(span.Minutes == 1 ? string.Empty : "")}:" : string.Empty)}{(span.Duration().Seconds > 0 ? $"{span.Seconds:0}s{(span.Seconds == 1 ? string.Empty : "")}" : string.Empty)}";

        if (formatted.EndsWith(", ")) formatted = formatted[..^2];

        if (string.IsNullOrEmpty(formatted)) formatted = "0 s";

        return formatted;
    }
}