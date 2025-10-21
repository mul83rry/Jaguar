using Jaguar.Core;
using Newtonsoft.Json;

namespace Jaguar.Helpers;

public static class Extensions
{
    public static IEnumerable<byte> ToBytes(this string value)
    {
        return Server.Encoding.GetBytes(value);
    }
    
    public static string ToJson(this object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }
    
    public static T? FromJson<T>(this string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }
}