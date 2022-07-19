namespace Jaguar.Core;

[Serializable]
public struct Range
{
    public uint End;
    public uint Start;

    /// <summary>
    /// Represents a range that has start and end indexes.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    public Range(uint start, uint end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Represents a range that start and end are equals.
    /// </summary>
    /// <param name="fix"></param>
    public Range(uint fix)
    {
        Start = End = fix;
    }

    /// <summary>
    /// return true if 'i' is between start and end.
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public readonly bool InRange(uint i) => i >= Start && i <= End;
}