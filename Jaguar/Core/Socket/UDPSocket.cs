using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Jaguar.Core.Dto;
using Jaguar.Core.Handlers;
using Jaguar.Extensions;
using Jaguar.Manager;
using Microsoft.Extensions.Logging;
using JsonConverter = System.Text.Json.JsonSerializer;


namespace Jaguar.Core.Socket;

internal static class UdpSocket
{
    private const string ByteListenerKey = "UDPBYTES";

    internal static Server? Server { get; set; }
    private static UdpListener? _listener;


    internal struct Received
    {
        internal Packet Packet;
        internal byte[] BytesArray;

        internal Received(string eventName, IPEndPoint sender)
        {
            Packet = new Packet
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
                var result = await Client.ReceiveAsync();

                if (result == default)
                {
                    Server.Logger?.Log(LogLevel.Warning, "Jaguar: Client.ReceiveAsync Is NULL");
                }

                var buffer = new byte[result.Buffer.Length - 1];
                Array.Copy(result.Buffer, 1, buffer, 0, result.Buffer.Length - 1);

                if (result.Buffer[0] == 0)
                {
                    return new Received(
                        buffer, sender: result.RemoteEndPoint);
                }

                // byte[] data
                return new Received(
                        ByteListenerKey, sender: result.RemoteEndPoint)
                { BytesArray = buffer };
            }
            catch (Exception ex)
            {
                Server.Logger?.Log(LogLevel.Error,
                    $"Jaguar_1: {ex.Message},,,,{ex.StackTrace},,,,{ex.Source},,,,{ex.InnerException},,,,{ex.Data},,,,{ex.TargetSite}");
                //CheckClientsConnection();
                _continueWhile = true;
            }

            return new Received(Array.Empty<byte>(), sender: null);
        }
    }

    //UDPServer
    private class UdpListener : UdpBase
    {
        internal UdpListener(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {
        }

        private UdpListener(IPEndPoint sender)
        {
            Client = new UdpClient(sender);
            Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            const int iocIn = unchecked((int)0x80000000);
            const int iocVendor = 0x18000000;
            const int sioUdpConnReset = iocIn | iocVendor | 12;

            byte[] optionInValue = { Convert.ToByte(false) };
            var optionOutValue = new byte[4];
            //Client.Client.IOControl(sioUdpConnReset, optionInValue, optionOutValue);
        }

        internal void Send(IPEndPoint? endpoint, byte[] dataGram)
        {
            Client.Send(dataGram, dataGram.Length, endpoint);
        }
    }

    private static bool _continueWhile;

    public static void Start()
    {
        _continueWhile = false;
        Server.AddListeners(Assembly.GetExecutingAssembly());

        _listener = new UdpListener(Server.Ip, Server.Port);

        //start listening for messages
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                try
                {
                    var received = await _listener.Receive();

                    if (_continueWhile)
                    {
                        continue;
                    }

                    _continueWhile = false;


                    var clients = Server.GetClients();
                    {
                        if (clients.TryGetValue(received.Packet.Sender.ConvertToKey(), out var client))
                        {
                            if (client is not null)
                            {
                                client.LastActivateTime = DateTime.UtcNow;
                            }
                            else
                            {
                                Server.Logger?.Log(LogLevel.Error, "Client is null");
                            }
                        }
                    }

                    if (received.Packet.EventName == ByteListenerKey)
                    {
                        if (received.Packet.Sender != null)
                        {
                            IByteListener.Instance?.OnMessageReceived(received.Packet.Sender,
                                received.BytesArray);
                            // foreach (var manager in ListenersManager.Managers)
                            // {
                            //     _ = manager.OnBytesReceived(received.Packet.Sender,
                            //         received.BytesArray);
                            // }
                        }

                        continue;
                    }


                    if (Server.ListenersDic.TryGetValue(received.Packet.EventName, out var normalTask))
                    {
                        object? convertedSender = received.Packet.Sender;
                        if (normalTask.SenderType != typeof(IPEndPoint))
                        {
                            var type = normalTask.SenderType;
                            if (type != null)
                            {
                                convertedSender = Convert.ChangeType(
                                    UsersManager.FindUser(received.Packet.Sender),
                                    type);
                            }

                            if (convertedSender == null)
                            {
                                Server.Logger?.Log(LogLevel.Error, $"Jaguar: user not submit");
                                continue;
                            }
                        }

                        if (normalTask.RequestType == typeof(string))
                        {
                            if (!received.Packet.Reliable)
                            {
                                normalTask.Method?.Invoke(
                                    normalTask.@object
                                    , new[] { convertedSender, received.Packet.Message });
                            }
                            else
                            {
                                if (!clients.TryGetValue(received.Packet.Sender.ConvertToKey(), out var client))
                                {
                                    Server.Logger?.Log(LogLevel.Warning, $"Jaguar: Client not found");
                                    continue;
                                }

                                if (client is not null)
                                {
                                    client.PacketReceiver
                                        .ReceivedReliablePacket(received.Packet);
                                }
                                else
                                {
                                    Server.Logger?.Log(LogLevel.Error, $"Client is null");
                                }
                            }
                        }
                        else
                        {
                            if (!received.Packet.Reliable)
                            {
                                if (received.Packet.Message == null)
                                {
                                    Server.Logger?.Log(LogLevel.Error, $"Jaguar: message is null");
                                    continue;
                                }

                                var type = normalTask.RequestType;
                                if (type == null)
                                {
                                    Server.Logger?.Log(LogLevel.Error, $"Jaguar: invalid listener type");
                                    continue;
                                }

                                var data = JsonConverter.Deserialize(received.Packet.Message, type);
                                normalTask.Method?.Invoke(
                                    normalTask.@object
                                    , new[] { convertedSender, data });
                            }
                            else
                            {
                                if (!clients.TryGetValue(received.Packet.Sender.ConvertToKey(), out var client))
                                    continue;

                                if (client is not null)
                                {
                                    client.PacketReceiver
                                        .ReceivedReliablePacket(received.Packet);
                                }
                                else
                                {
                                    Server.Logger?.Log(LogLevel.Error, $"Client is null");
                                }
                            }
                        }
                    }
                    else if (Server.CallBackListenersDic.TryGetValue(received.Packet.EventName,
                                 out var callbackTask))
                    {
                        if (callbackTask.RequestType == typeof(string))
                        {
                            if (!clients.TryGetValue(received.Packet.Sender.ConvertToKey(), out var client))
                                continue;

                            if (client is not null)
                            {
                                client.PacketReceiver
                                    .ReceivedReliablePacket(received.Packet);
                            }
                            else
                            {
                                Server.Logger?.Log(LogLevel.Error, $"Client is null");
                            }
                        }
                        else
                        {
                            if (!clients.TryGetValue(received.Packet.Sender.ConvertToKey(), out var client))
                                continue;

                            if (client is not null)
                            {
                                client.PacketReceiver
                                    .ReceivedReliablePacket(received.Packet);
                            }
                            else
                            {
                                Server.Logger?.Log(LogLevel.Error, $"Client is null");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Server.Logger?.Log(LogLevel.Error, $"Jaguar_2: {ex.Message}");
                    try
                    {
                        //CheckClientsConnection();
                    }
                    catch (Exception)
                    {
                        Server.Logger?.Log(LogLevel.Error, $"Jaguar_3: {ex.Message}");
                    }
                }
            }
        });

        // Task.Factory.StartNew(async () =>
        // {
        //     while (true)
        //     {
        //         await Task.Delay(2000);
        //         //CheckClientsConnection();
        //     }
        // });

        Listener.OnMessageReceived += async (sender, packet) =>
        {
            var clients = Server.GetClients();

            if (Server.CallBackListenersDic.ContainsKey(packet.EventName)) // is async
            {
                try
                {
                    object? convertedSender = sender;
                    if (Server.CallBackListenersDic[packet.EventName].SenderType != typeof(IPEndPoint))
                    {
                        var type = Server.CallBackListenersDic[packet.EventName].SenderType;
                        if (type != null)
                        {
                            var user = UsersManager.FindUser(sender);
                            convertedSender = Convert.ChangeType(user,
                                type);
                        }

                        if (convertedSender == null)
                        {
                            Server.Logger?.Log(LogLevel.Critical, $"Jaguar: Invalid cast to {type}");
                            return;
                        }
                    }

                    if (packet.Message == null) return;

                    {
                        var type = Server.CallBackListenersDic[packet.EventName].RequestType;
                        if (type == null) return;

                        var data = type == typeof(string)
                            ? new[] { convertedSender, packet.Message }
                            : new[] { convertedSender, JsonConverter.Deserialize(packet.Message, type) };

                        var methodInfo = Server.CallBackListenersDic[packet.EventName].Method
                                         ?? throw new NullReferenceException(
                                             $"The method of {packet.EventName} is null");
                        var listenerInstance = Server.CallBackListenersDic[packet.EventName].@object
                                               ?? throw new NullReferenceException(
                                                   $"The @Object of {packet.EventName} is null");

                        // Invoke the method and get the response
                        dynamic responseTask =
                            methodInfo.Invoke(listenerInstance, data) ?? throw new NullReferenceException($"{packet.EventName}");

                        // If the method is async and you want to await the response
                        if (responseTask is not Task task) return;
                        await task.ConfigureAwait(false);
                        var resultProperty = task.GetType().GetProperty("Result");

                        if (resultProperty == null)
                        {
                            throw new NullReferenceException($"resultProperty is null");
                        }
                        dynamic finalResponse = resultProperty.GetValue(task) ?? throw new InvalidOperationException();

                        if (!clients.ContainsKey(sender.ConvertToKey())) return;
                        if (finalResponse != null)
                        {
                            Server.GetClients()[sender.ConvertToKey()]!.PacketSender
                                .SendReliablePacket(packet.EventName, finalResponse, packet.SignIndex);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
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
                            convertedSender = Convert.ChangeType(UsersManager.FindUser(sender),
                                type);
                        }

                        if (convertedSender == null) return;
                    }

                    var type1 = Server.ListenersDic[packet.EventName].RequestType;
                    if (type1 == null) return;
                    if (packet.Message != null)
                    {
                        Console.WriteLine($"{packet.EventName} - {packet.Message}");
                        object? data = null;

                        if (type1 != typeof(string))
                        {
                            data = JsonConverter.Deserialize(packet.Message,
                                type1);
                        }
                        var parameters = type1 == typeof(string)
                            ? new[] { convertedSender, packet.Message }
                            : new[]
                            {
                                convertedSender,
                                data
                            };

                        Server.ListenersDic[packet.EventName].Method?.Invoke(
                            Server.ListenersDic[packet.EventName].@object
                            , parameters);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    // ignored
                }
            }
        };

        Server.OnServerStarted?.Invoke(Server.Ip, Server.Port);
    }


    //private static void CheckClientsConnection()
    //{
    //    var clients = Server.GetClients();

    //    for (var i = 0; i < clients.Count; i++)
    //    {
    //        var client = clients.ElementAt(i).Value;
    //        var clientKey = clients.ElementAt(i).Key;

    //        if (client.LastActivateTime.AddSeconds(70) > DateTime.UtcNow)
    //        {
    //            continue;
    //        }

    //        var inUse = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
    //            .Any(p => p.Port == client.Client.Port);
    //        if (inUse) continue;
    //        Server.RemoveClient(clientKey);

    //        client.PacketSender.Destroy();
    //        client.PacketReceiver.Destroy();

    //        Server.OnClientExited?.Invoke(client.Client);
    //    }
    //}

    internal static void Send(IPEndPoint? ep, Packet packet)
    {
        var packetAsBytes = packet.ToByte();
        var dataGram = new byte[packetAsBytes.Length + 1];
        Array.Copy(packetAsBytes, 0, dataGram, 1, packetAsBytes.Length);
        _listener!.Send(ep, dataGram);
    }

    internal static void SendBytes(IPEndPoint? ep, byte[] bytes)
    {
        var dataGram = new byte[bytes.Length + 1];
        dataGram[0] = 1;
        Array.Copy(bytes, 0, dataGram, 1, bytes.Length);
        _listener!.Send(ep, dataGram);
    }
}