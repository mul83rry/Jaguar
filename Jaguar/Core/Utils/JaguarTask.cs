using System.Reflection;

namespace Jaguar.Core.Utils;

public class JaguarTask
{
    public Type? RequestType { get; init; }
    public Type? ResponseType { get; init; }
    public MethodInfo? Method;
    public object? Object;
    public Type? SenderType;
}