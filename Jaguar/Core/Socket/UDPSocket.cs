using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Jaguar.Core.Data;
using Jaguar.Extensions;
using Newtonsoft.Json;

namespace Jaguar.Core.Socket
{
    internal static class UdpSocket
    {
        private static int _numberChecker;
        private static int _numberChecker2;
        private const string ByteListener = "UDPBYTES";

        internal static Server? Server { get; set; }
        internal static UdpListener? Listener;



        internal struct Received
        {
            internal Packet Packet;
            internal byte[] BytesArray;
            internal Received(string eventName, IPEndPoint sender)
            {
                Packet = new Packet()
                {
                    EventName = eventName,
                    Sender = sender
                };
                BytesArray = Array.Empty<byte>();
            }

            internal Received(byte[] bytes, IPEndPoint? sender)
            {
                BytesArray = Array.Empty<byte>();
                if (bytes.Length < 2)
                {
                    Packet = new Packet();
                    return;
                }
                try
                {
                    Packet = new Packet(bytes)
                    {
                        Sender = sender
                    };
                }
                catch
                {
                    Packet = new Packet();
                }
            }
        }

        internal abstract class UdpBase
        {
            protected UdpClient Client;

            protected UdpBase() => Client = new UdpClient();

            internal async Task<Received> Receive()
            {
                try
                {
                    _numberChecker++;
                    var result = await Client.ReceiveAsync();
                    _numberChecker--;

                    if (result == default)
                    {
                        Trace.TraceError("RUDPNet: Client.ReceiveAsync Is NULL");
                    }

                    var buffer = new byte[result.Buffer.Length - 1];
                    Array.Copy(result.Buffer, 1, buffer, 0, result.Buffer.Length - 1);

                    if (result.Buffer[0] == 0)
                    {
                        return new Received(
                            buffer, sender: result.RemoteEndPoint);
                    }
                    else // byte[] data
                    {
                        return new Received(
                            "UDPBYTES", sender: result.RemoteEndPoint)
                        { BytesArray = buffer };
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"RUDPNet: {ex.Message}");
                    CheckClientsConnection();
                    _continueWhile = true;
                }

                return new Received(Array.Empty<byte>(), sender: null);
            }
        }

        //UDPServer
        internal class UdpListener : UdpBase
        {
            internal UdpListener(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port)) { }

            internal UdpListener(IPEndPoint sender)
            {
                Client = new UdpClient(sender);
                Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                const int iocIn = unchecked((int)0x80000000);
                const int iocVendor = 0x18000000;
                const int sioUdpConnreset = iocIn | iocVendor | 12;

                byte[] optionInValue = { Convert.ToByte(false) };
                var optionOutValue = new byte[4];
                Client.Client.IOControl(sioUdpConnreset, optionInValue, optionOutValue);
            }

            internal void Send(IPEndPoint? endpoint, byte[] dataGram)
            {
                Client.Send(dataGram, dataGram.Length, endpoint);
            }

        }

        private static bool _continueWhile;
        internal static void Start(string ip, int port)
        {
            _continueWhile = false;
            _ = new InternalListener();

            Listener = new UdpListener(ip, port);
            //Console.WriteLine("Server Started.");
            Console.Title = $"Server listening to {ip}:{port}";

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    Console.Title = $"{_numberChecker},{_numberChecker2}";
                }
            });
            //start listening for messages
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        _numberChecker2++;
                        var received = await Listener.Receive();
                        _numberChecker2--;

                        if (_continueWhile) continue;
                        _continueWhile = false;


                        var clients = Server.GetClients();
                        if (clients.ContainsKey(received.Packet.Sender.ConvertToKey()))
                        {
                            clients[received.Packet.Sender.ConvertToKey()].LastActivateTime = DateTime.Now;
                        }


                        if (received.Packet.EventName == ByteListener)
                        {
                            if (received.Packet.Sender != null)
                            {
                                Processor.Listener.OnNewBytesMessageReceived?.Invoke(received.Packet.Sender,
                                    received.BytesArray);
                            }
                            continue;
                        }


                        if (Server.ListenersDic.ContainsKey(received.Packet.EventName))
                        {
                            object? convertedSender = received.Packet.Sender;
                            if (Server.ListenersDic[received.Packet.EventName].SenderType != typeof(IPEndPoint))
                            {
                                var type = Server.ListenersDic[received.Packet.EventName].SenderType;
                                if (type != null)
                                {
                                    convertedSender = Convert.ChangeType(
                                        Manager.UsersManager.FindUser(received.Packet.Sender),
                                        type);
                                }
                                if (convertedSender == null) continue;
                            }

                            if (Server.ListenersDic[received.Packet.EventName].FunctionType == typeof(string))
                            {
                                if (!received.Packet.Reliable)
                                {
                                    // ????? received.Packet.Sender = received.Packet.Sender;
                                    Server.ListenersDic[received.Packet.EventName].Method?.Invoke(Server.ListenersDic[received.Packet.EventName].ListenersManager
                                        , new[] { convertedSender, received.Packet.Message });
                                }
                                else
                                {
                                    if (clients.ContainsKey(received.Packet.Sender.ConvertToKey()))
                                    {
                                        clients[received.Packet.Sender.ConvertToKey()].Receipt
                                            .ReceivedReliablePacket(received.Packet);
                                    }

                                }
                            }
                            else
                            {
                                if (!received.Packet.Reliable)
                                {
                                    if (received.Packet.Message == null) continue;
                                    var type = Server.ListenersDic[received.Packet.EventName].FunctionType;
                                    if (type == null) continue;
                                    var data = JsonConvert.DeserializeObject(received.Packet.Message, type);
                                    Server.ListenersDic[received.Packet.EventName].Method?.Invoke(Server.ListenersDic[received.Packet.EventName].ListenersManager
                                        , new[] { convertedSender, data });
                                }
                                else
                                {
                                    if (clients.ContainsKey(received.Packet.Sender.ConvertToKey()))
                                    {
                                        clients[received.Packet.Sender.ConvertToKey()].Receipt
                                            .ReceivedReliablePacket(received.Packet);
                                    }
                                }
                            }
                        }
                        else if (Server.AsyncListenersDic.ContainsKey(received.Packet.EventName))
                        {
                            if (Server.AsyncListenersDic[received.Packet.EventName].FunctionType == typeof(string))
                            {
                                if (clients.ContainsKey(received.Packet.Sender.ConvertToKey()))
                                {
                                    clients[received.Packet.Sender.ConvertToKey()].Receipt
                                        .ReceivedReliablePacket(received.Packet);
                                }
                            }
                            else
                            {
                                if (clients.ContainsKey(received.Packet.Sender.ConvertToKey()))
                                {
                                    clients[received.Packet.Sender.ConvertToKey()].Receipt
                                        .ReceivedReliablePacket(received.Packet);
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError($"RUDPNet: {ex.Message}");
                        try
                        {
                            CheckClientsConnection();
                        }
                        catch (Exception)
                        {
                            Trace.TraceError($"RUDPNet: {ex.Message}");
                        }
                    }
                }
            });

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(2000);
                    CheckClientsConnection();
                }
            });

            Processor.Listener.OnNewMessageReceived += (sender, packet) =>
            {
                var clients = Server.GetClients();

                if (Server.AsyncListenersDic.ContainsKey(packet.EventName)) // is async
                {
                    try
                    {
                        object? convertedSender = sender;
                        if (Server.AsyncListenersDic[packet.EventName].SenderType != typeof(IPEndPoint))
                        {
                            var type = Server.AsyncListenersDic[packet.EventName].SenderType;
                            if (type != null)
                            {
                                convertedSender = Convert.ChangeType(Manager.UsersManager.FindUser(sender),
                                    type);
                            }
                            if (convertedSender == null) return;
                        }

                        if (packet.Message == null) return;
                        {
                            var type = Server.AsyncListenersDic[packet.EventName].FunctionType;
                            if (type == null) return;
                            var result = Server.AsyncListenersDic[packet.EventName].Method?.Invoke(
                                Server.AsyncListenersDic[packet.EventName].ListenersManager
                                , type == typeof(string) ? new[] { convertedSender, packet.Message }
                                    : new[] { convertedSender, JsonConvert.DeserializeObject(packet.Message, type) });

                            if (!clients.ContainsKey(sender.ConvertToKey())) return;
                            if (result != null)
                            {
                                Server.GetClients()[sender.ConvertToKey()].Post
                                    .SendReliablePacket(packet.EventName, result, packet.SignIndex);
                            }

                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
                else if (Server.ListenersDic.ContainsKey(packet.EventName))
                {
                    try
                    {
                        object? convertedSender = sender;
                        if (Server.ListenersDic[packet.EventName].SenderType != typeof(IPEndPoint))
                        {
                            var type = Server.ListenersDic[packet.EventName].SenderType;
                            if (type != null)
                            {
                                convertedSender = Convert.ChangeType(Manager.UsersManager.FindUser(sender),
                                    type);
                            }
                            if (convertedSender == null) return;
                        }

                        var type1 = Server.ListenersDic[packet.EventName].FunctionType;
                        if (type1 == null) return;
                        if (packet.Message != null)
                        {
                            Server.ListenersDic[packet.EventName].Method?.Invoke(
                                Server.ListenersDic[packet.EventName].ListenersManager
                                , type1 == typeof(string)
                                    ? new[] { convertedSender, packet.Message }
                                    : new[]
                                    {
                                            convertedSender,
                                            JsonConvert.DeserializeObject(packet.Message,
                                                type1)
                                    });
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

            };

        }

        

        private static void CheckClientsConnection()
        {
            var clients = Server.GetClients();

            for (var i = 0; i < clients.Count; i++)
            {
                var client = clients.ElementAt(i).Value;
                var clientKey = clients.ElementAt(i).Key;

                if (client.LastActivateTime.AddSeconds(70) > DateTime.Now)
                {
                    continue;
                }

                var inUse = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                    .Any(p => p.Port == client.Client.Port);
                if (inUse) continue;
                Server.RemoveClient(clientKey);

                client.Post.Destroy();
                client.Receipt.Destroy();

                Server.OnClientExited?.Invoke(client.Client);
            }
        }

        internal static void Send(IPEndPoint? ep, Packet packet)
        {
            var packetAsBytes = packet.ToByte();
            var dataGram = new byte[packetAsBytes.Length + 1];
            Array.Copy(packetAsBytes, 0, dataGram, 1, packetAsBytes.Length);
            Listener!.Send(ep, dataGram);
        }

        internal static void SendBytes(IPEndPoint? ep, byte[] bytes)
        {
            var dataGram = new byte[bytes.Length + 1];
            dataGram[0] = 1;
            Array.Copy(bytes, 0, dataGram, 1, bytes.Length);
            Listener!.Send(ep, dataGram);
        }

    }
}