﻿using System.Numerics;
using Jaguar.Core;

namespace Jaguar.Manager;

public static class UsersManager
{
    /// <summary>
    /// count of clients how connected
    /// </summary>
    // public static int OnlineClientsCounts => Server.GetClients().Count(c => c.Value.User is { IsOnline: true });

    // public static T?[] GetAllUser<T>() where T : User => Server.GetClients().Where(c => c.Value.User is { IsOnline: true }).Select(c => c.Value.User as T).ToArray();
    // public static ClientData[] GetAllClients() => Server.GetClients().Where(c => c.Value.User is { IsOnline: true }).Select(c => c.Value).ToArray();

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

    // public static bool AnyUser(IPEndPoint client) => Server.GetClients().ContainsKey(client.ConvertToKey());

    /// <summary>
    /// search for a connected user with 'UniqueId'.
    /// </summary>
    /// <param name="id">unique id of user.</param>
    /// <returns>return search for a connected user with 'UniqueId'.</returns>
    public static User? FindUser(long id) => Jaguar.Core.WebSocket.WebSocket.Clients.Values
        .SingleOrDefault(c => c.User?.UniqueId == id)?.User;
}