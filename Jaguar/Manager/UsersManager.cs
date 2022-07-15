using System.Net;
using Jaguar.Core;
using Jaguar.Extensions;

namespace Jaguar.Manager
{
    public static class UsersManager
    {
        /// <summary>
        /// count of clients how connected
        /// </summary>
        public static int OnlineClientsCounts => Server.GetClients().Count(c => c.Value.User is { IsOnline: true });

        public static T?[] GetAllUser<T>() where T : User => Server.GetClients().Where(c => c.Value.User is { IsOnline: true }).Select(c => c.Value.User as T).ToArray();
        public static ClientDic[] GetAllClients() => Server.GetClients().Where(c => c.Value.User is { IsOnline: true }).Select(c => c.Value).ToArray();

        /// <summary>
        /// search for a connected user with 'Sender'.
        /// </summary>
        /// <param name="client">Sender of user.</param>
        /// <returns>return search for a connected user with Sender.</returns>
        public static User? FindUser(IPEndPoint? client)
        {
            if (client == null) return null;
            var clients = Server.GetClients();
            
            return !clients.ContainsKey(client.ConvertToKey())
                    ? null
                    : clients[client.ConvertToKey()].User;
        }

        public static bool AnyUser(IPEndPoint client) => Server.GetClients().ContainsKey(client.ConvertToKey());

        /// <summary>
        /// search for a connected user with 'UniqueId'.
        /// </summary>
        /// <param name="id">unique id of user.</param>
        /// <returns>return search for a connected user with 'UniqueId'.</returns>
        public static User? FindUser(long id) => Server.GetClients().Values.SingleOrDefault(c => c.User?.UniqueId == id)?.User;
    }
}