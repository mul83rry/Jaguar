using System.Numerics;
using Jaguar.Core.Entity;

namespace Jaguar.Manager;

public static class UsersManager
{
    public static List<User?> GetAll()
    {
        var clients = Jaguar.Core.WebSocket.WebSocket.Clients;
        return clients.Values.Select(c => c.User).ToList();
    }
    /// <summary>
    /// search for a connected user with 'Sender'.
    /// </summary>
    /// <param name="client">Sender of user.</param>
    /// <returns>return search for a connected user with Sender.</returns>
    public static User? FindUser(BigInteger? client)
    {
        if (client == null) return null;
        var clients = Jaguar.Core.WebSocket.WebSocket.Clients;

        clients.TryGetValue(client.Value, out var clientDic);

        return clientDic?.User;
    }
    

    /// <summary>
    /// search for a connected user with 'UniqueId'.
    /// </summary>
    /// <param name="id">unique id of user.</param>
    /// <returns>return search for a connected user with 'UniqueId'.</returns>
    public static User? FindUser(long id) => Core.WebSocket.WebSocket.Clients.Values
        .SingleOrDefault(c => c.User?.UniqueId == id)?.User;

    public static T? FindUser<T>(Func<T, bool> predicate) where T : class
    {
        return Core.WebSocket.WebSocket.Clients.Select(pair => pair.Value.User).OfType<T>().FirstOrDefault(predicate);
    }

    public static int GetUsersCount()
    {
        return Core.WebSocket.WebSocket.Clients.Count;
    }
}