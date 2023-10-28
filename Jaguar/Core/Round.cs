namespace Jaguar.Core;

[Serializable]
public sealed class Round
{
    /// <summary>
    /// index of round
    /// </summary>
    public ushort Index { get; private set; }

    internal bool Completed { get; private set; }

    internal Room Room { get; private set; }

    internal Dictionary<long, double> UsersScore { get; private set; } = new();

    public void RoundComplete() => Completed = true;

    public DateTime CreationTime { get; private set; }

    internal Round(ushort index, Room room)
    {
        CreationTime = DateTime.UtcNow;
        Index = index;
        Room = room;

    }

    internal bool TryAddScore(User? user)
    {
        if (user == null) return false;
        UsersScore[user.UniqueId]++;
        return true;
    }

    internal bool TryAddScore(User? user, double count)
    {
        if (user == null) return false;
        UsersScore[user.UniqueId] += count;
        return true;
    }

    internal bool TryGetScore(User? user, out double score)
    {
        score = 0;
        if (user == null) return false;
        score = UsersScore[user.UniqueId];
        return true;
    }

    public void ResetScore(User? user) => UsersScore[Room.Users.IndexOf(user)] = 0;
}