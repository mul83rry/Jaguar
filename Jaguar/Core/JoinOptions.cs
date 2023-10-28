namespace Jaguar.Core;

public record JoinOptions
{
    internal uint? Level { get; private init; }
    internal long? RoomId { get; private init; }
    internal string? Password { get; private init; }
    internal uint? TypeId { get; private init; }

    public JoinOptions UseLevel(uint level)
    {
        if (RoomId.HasValue) throw new ArgumentException("Can not use room id and level together");
        return this with { Level = level };
    }

    public JoinOptions UseRoomId(long id) => this with { RoomId = id };
    public JoinOptions UsePassword(string password)
    {
        if (!RoomId.HasValue) throw new ArgumentException("room id most be specified");
        return this with { Password = password };
    }

    public JoinOptions UseTypeId(uint id)
    {
        if (RoomId.HasValue) throw new ArgumentException("Can not use room id and type id together");
        return this with { TypeId = id };
    }
}