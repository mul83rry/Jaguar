using System.Reflection;

namespace Jaguar.Core;

public class JaguarTask
{
    public Type? RequestType { get; init; }
    public Type? ResponseType { get; init; }
    public MethodInfo? Method;
    public Object? @object;
    public Type? SenderType;
}