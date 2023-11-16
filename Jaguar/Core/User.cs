using System.Net;
using System.Net.WebSockets;
using System.Numerics;
using Jaguar.Extensions;

namespace Jaguar.Core;

[Serializable]
public abstract class User
{
    public const double RoomNotFoundCode = -1;
    public const double RoundNotFoundCode = -2;
    public const double RoomNotStartedCode = -3;

    /// <summary>
    /// it return current room witch user joined.
    /// </summary>
    [NonSerialized] internal WebSocketContext? Client;

    public BigInteger SocketId { get; set; }
    
    //public bool InRoom { get; internal set; }

    public WebSocketContext SocketContext { get; set; }
    
    public bool IsOnline { get; internal set; }

    // public DateTime LastActivateTime => Server.GetClients()[Client.ConvertToKey()]!.LastActivateTime;

    /// <summary>
    /// user rooms
    /// </summary>
    public List<Room?> Rooms { get; } = new();


    /// <summary>
    /// user current room
    /// </summary>
    public Room? CurrentRoom { get; private set; }

    public bool SetAsCurrentRoom(Room? room)
    {
        if (room == null) return false;
        if (!Rooms.Contains(room)) return false;
        if (room.Users.All(u => u?.UniqueId != UniqueId)) return false;

        CurrentRoom = room;

        return true;
    }

    public void SetCurrentRoomToNull() => CurrentRoom = null;

    /// <summary>
    /// unique id of user
    /// </summary>
    public long UniqueId { get; }

    #region constructor

    /// <summary>
    /// there is two kind of constructor for user.
    /// this constructor is for bot or some thing like that
    /// </summary>
    protected User()
    {
        UniqueId = Server.GenerateUniqueUserId();
    }

    /// <summary>
    /// there is two kind of constructor for user.
    /// this constructor is for real users, witch needs Sender variable as an argument
    /// </summary>
    protected User(WebSocketContext? ipEndPoint)
    {
        if (ipEndPoint == null)
            throw new NullReferenceException("Sender can not be null");

        Server.UpdateClient(this, ipEndPoint);

        UniqueId = Server.GenerateUniqueUserId();
        IsOnline = true;
        Client = ipEndPoint;
    }

    #endregion constructor

    /*public void DestroyOtherClients()
    {
        if (Client == null) return;
        UDPServer.ClientsList.RemoveAll(u => u.Client == Client && u.User != null && u.User.UniqueId != UniqueId);
    }*/

    /// <summary>
    /// add one score to this user in current round
    /// </summary>
    /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null.</exception>
    public bool AddScore()
    {
        if (CurrentRoom == null) return false;
        if (CurrentRoom.CurrentRound == null) return false;

        CurrentRoom.CurrentRound.TryAddScore(this);
        return true;
    }

    /// <summary>
    /// add specified score to this user in specified round
    /// </summary>
    /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null.</exception>
    public bool AddScore(double count, int roundIndex)
    {
        if (CurrentRoom == null) return false;
        if (roundIndex >= CurrentRoom.Rounds.Length) return false;

        CurrentRoom.Rounds[roundIndex].TryAddScore(this, count);
        return true;
    }

    /// <summary>
    /// add specified score to this user in current round
    /// </summary>
    /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null.</exception>
    public bool AddScore(double count)
    {
        if (CurrentRoom == null) return false;
        if (CurrentRoom.CurrentRound == null) return false;

        CurrentRoom.CurrentRound.TryAddScore(this, count);
        return true;
    }

    public bool TryGetTotalScore(out double result, out int roundsCount)
    {
        roundsCount = 0;
        result = 0;
        if (CurrentRoom is not {GameStarted: true}) return false;

        //if (_rooms != null) roundsCount = _rooms.Rounds.Length;
        foreach (var round in CurrentRoom.Rounds)
        {
            roundsCount++;
            round.TryGetScore(this, out var roundScore);
            result += roundScore;
        }

        return true;
    }

    /// <summary>
    /// score of user in specified round
    /// </summary>
    /// <param name="roundIndex"></param>
    /// <returns>return score of user in specified round</returns>
    /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null or round index is out of range.</exception>
    public double Score(int roundIndex)
    {
        if (CurrentRoom == null) return RoomNotFoundCode; // throw new Exception("room not found 6");
        if (roundIndex > CurrentRoom.Rounds.Length) throw new IndexOutOfRangeException("Index out of range");
        if (CurrentRoom.Rounds[roundIndex] == null) throw new Exception("Round not started");

        CurrentRoom.Rounds[roundIndex].TryGetScore(this, out var score);
        return score;
    }

    /// <summary>
    /// this is for real users, witch get Sender variable for update.
    /// </summary>
    /// <param name="client">Sender of user</param>
    public void UpdateClient(WebSocketContext? client)
    {
        Server.UpdateClient(this, client);
        Client = client;
    }

    internal User ShallowCopy() => (User) MemberwiseClone();

    public abstract void Dispose();
}