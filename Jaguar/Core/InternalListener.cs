using System.Net;
using Jaguar.Extensions;
using Jaguar.Manager;

namespace Jaguar.Core
{
    internal class InternalListener : ListenersManager
    {
        [Attributes.Listener("PRC")]
        public static Task PacketReceivedCallBack(IPEndPoint sender, uint packetIndex)
        {
            var clients = Server.GetClients();

            if (!clients.Values.Any(c => c.Client.ConvertToKey()!.Equals(sender.ConvertToKey())))
                return Task.CompletedTask;

            if (!clients.ContainsKey(sender.ConvertToKey())) return Task.CompletedTask;
            var udpClient = clients[sender.ConvertToKey()];
            udpClient.Post.PacketReceivedCallBack(packetIndex);

            return Task.CompletedTask;
        }

        [Attributes.Listener("IA")]
        public static Task Alive(IPEndPoint sender, string _) => Task.CompletedTask;

        [Attributes.Listener("JTS")]
        public static Task JoinToServer(IPEndPoint sender, string _)
        {
            var clients = Server.GetClients();
            var senderKey = sender.ConvertToKey();
            if (string.IsNullOrEmpty(senderKey)) return Task.CompletedTask;
            if (!clients.Values.Any(c => c.Client.ConvertToKey().Equals(senderKey)))
            {
                var clientDic = new ClientDic(null, sender);
                Server.AddClient(senderKey, clientDic);

                Server.OnNewClientJoined?.Invoke(sender);

                // send join call back
                clientDic.Post.SendReliablePacket("JTS", $"{Settings.MaxPacketSize},{Settings.MaxPacketInQueue}");//1: Max packet size, 2: Max packets in queue
            }

            return Task.CompletedTask;
        }
    }
}