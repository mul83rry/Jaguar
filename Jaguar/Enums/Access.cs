namespace Jaguar.Enums;

// [Flags]
public enum Access
{
    None = 0,
    Private = 1 << 1,
    Public = 1 << 2
}