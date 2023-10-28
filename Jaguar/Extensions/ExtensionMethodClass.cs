using System.Net;

namespace Jaguar.Extensions;

internal static class IpEndPointExtension
{
    internal static string ConvertToKey(this IPEndPoint? ipEndPoint) =>
        ipEndPoint == null ? string.Empty : $"{ipEndPoint.Address}:{ipEndPoint.Port}";
}