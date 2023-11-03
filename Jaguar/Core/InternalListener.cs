using System.Net;
using Jaguar.Extensions;
using Jaguar.New;

namespace Jaguar.Core;

internal class JoinToServerListener : UnRegisteredUserListener<string>
{
    public override void Config()
    {
        Name = "JTS";
    }

    public override async Task OnMessageReceived(IPEndPoint sender, string data)
    {
        var clients = Server.GetClients();
        var senderKey = sender.ConvertToKey();
        if (string.IsNullOrEmpty(senderKey)) return;
        if (!clients.Values.Any(c => c.Client.ConvertToKey().Equals(senderKey)))
        {
            using var clientDic = new ClientDic(null, sender);
            Server.AddClient(senderKey, clientDic);

            _ = Task.Run(() =>
                // init 
            {
                clientDic.PacketSender.StartReliablePacketsServiceAsync();
                clientDic.PacketReceiver.CheckSequenceDataAsync();
                Console.WriteLine("START");
            });

            Server.OnNewClientJoined?.Invoke(sender);

            // send join call back
            clientDic.PacketSender.SendReliablePacket("JTS",
                $"{Settings.MaxPacketSize},{Settings.MaxPacketInQueue}"); //1: Max packet size, 2: Max packets in queue

            while (!clientDic.PacketReceiver.Destroyed || !clientDic.PacketSender.Destroyed)
            {
                await Task.Delay(500);
            }

            // close session
            Server.RemoveClient(senderKey);
        }
    }
}

internal class PacketReceivedCallbackListener : UnRegisteredUserListener<uint>
{
    public override void Config()
    {
        Name = "PRC"; // packet received callback
    }

    public override Task OnMessageReceived(IPEndPoint sender, uint data)
    {
        var clients = Server.GetClients();

        if (!clients.Values.Any(c => c.Client.ConvertToKey().Equals(sender.ConvertToKey())))
            return Task.CompletedTask;

        if (!clients.ContainsKey(sender.ConvertToKey())) return Task.CompletedTask;
        var udpClient = clients[sender.ConvertToKey()];
        udpClient.PacketSender.PacketReceivedCallBack(data);

        return Task.CompletedTask;
    }
}

// internal class InternalListenerOld
// {
//     [Attributes.Listener("PRC")]
//     public static Task PacketReceivedCallBack(IPEndPoint sender, uint packetIndex)
//     {
//         var clients = Server.GetClients();
//
//         if (!clients.Values.Any(c => c.Client.ConvertToKey().Equals(sender.ConvertToKey())))
//             return Task.CompletedTask;
//
//         if (!clients.ContainsKey(sender.ConvertToKey())) return Task.CompletedTask;
//         var udpClient = clients[sender.ConvertToKey()];
//         udpClient.PacketSender.PacketReceivedCallBack(packetIndex);
//
//         return Task.CompletedTask;
//     }
//
//     // [Attributes.Listener("IA")]
//     // public static Task Alive(IPEndPoint sender, string _) => Task.CompletedTask;
//
//     [Attributes.Listener("JTS")]
//     public static async Task JoinToServer(IPEndPoint sender, string _)
//     {
//         var clients = Server.GetClients();
//         var senderKey = sender.ConvertToKey();
//         if (string.IsNullOrEmpty(senderKey)) return;
//         if (!clients.Values.Any(c => c.Client.ConvertToKey().Equals(senderKey)))
//         {
//             using var clientDic = new ClientDic(null, sender);
//             Server.AddClient(senderKey, clientDic);
//
//             Task.Run(() =>
//                 // init 
//             {
//                 clientDic.PacketSender.StartReliablePacketsServiceAsync();
//                 clientDic.PacketReceiver.CheckSequenceDataAsync();
//                 Console.WriteLine("START");
//             });
//
//             Server.OnNewClientJoined?.Invoke(sender);
//
//             // send join call back
//             clientDic.PacketSender.SendReliablePacket("JTS",
//                 $"{Settings.MaxPacketSize},{Settings.MaxPacketInQueue}"); //1: Max packet size, 2: Max packets in queue
//
//             while (!clientDic.PacketReceiver.Destroyed || !clientDic.PacketSender.Destroyed)
//             {
//                 await Task.Delay(500);
//             }
//
//             // clientDic.PacketSender.Destroy();
//             // clientDic.PacketReceiver.Destroy();
//
//             Server.RemoveClient(senderKey);
//             Console.WriteLine("END");
//         }
//     }
// }