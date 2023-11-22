using Jaguar.Core.Dto;
using System.Net;
using System.Net.WebSockets;
using System.Numerics;
using System.Text.Json;
using Jaguar.Core.Handlers;
using Jaguar.Helpers;
using Jaguar.Manager;
using Microsoft.Extensions.Logging;

namespace Jaguar.Core.Socket;

public class WebSocketContextData
{
    public WebSocketContext SocketContext { get; init; }
    public User? User { get; set; }
    public Dictionary<string, byte> SupportedListeners { get; internal set; }
}

internal class WebSocket
{
    private static int _maxBufferSize;
    private static string _uri;


    // Todo: manage disconnected clients
    internal static readonly Dictionary<BigInteger, WebSocketContextData> Clients = new();

    internal WebSocket(string uri, int maxBufferSize)
    {
        _uri = uri;
        _maxBufferSize = maxBufferSize;
    }

    internal void Start()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(_uri);
        listener.Start();
        Server.OnServerStarted?.Invoke();

        while (true)
        {
            var listenerContext = listener.GetContext();
            if (listenerContext.Request.IsWebSocketRequest)
            {
                ProcessRequest(listenerContext);
            }
            else
            {
                listenerContext.Response.StatusCode = 400;
                listenerContext.Response.Close();
            }
        }
    }

    private async void ProcessRequest(HttpListenerContext listenerContext)
    {
        WebSocketContext? webSocketContext = null;

        try
        {
            webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
            var receiveBuffer = new byte[_maxBufferSize];

            while (webSocketContext.WebSocket.State == WebSocketState.Open)
            {
                var receiveResult =
                    await webSocketContext.WebSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer),
                        CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    Server.OnClientExited?.Invoke(webSocketContext);
                    await webSocketContext.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "",
                        CancellationToken.None);
                    
                    // remove client
                    var res = Clients.Remove(Clients.FirstOrDefault(i => i.Value.SocketContext == webSocketContext).Key);
                    
                    if (!res)
                    {
                        Server.OnError?.Invoke($"");
                    }
                }
                else
                {
                    // handle received message
                    _ = Handle(webSocketContext, receiveBuffer);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception: {0}", e);
        }
        finally
        {
            if (webSocketContext is {WebSocket: not null}) webSocketContext.WebSocket.Dispose();
        }
    }

    private static async Task Handle(WebSocketContext webSocketContext, byte[] bytes)
    {
        var receivedPacket = new Packet(bytes);

        switch (receivedPacket)
        {
            case {Sender: null}:
            {
                break;
            }
            case {EventId: EventIdConstants.NoneEventId}:
            {
                break;
            }
            case {EventId: EventIdConstants.ListenerNameMapEventId}:
            {
                if (Clients.TryGetValue(receivedPacket.Sender.Value, out var client))
                {
                    if (receivedPacket.Message == null)
                    {
                        break;
                    }

                    client.SupportedListeners =
                        JsonSerializer.Deserialize<Dictionary<string, byte>>(receivedPacket.Message) ??
                        new Dictionary<string, byte>();

                    if (!client.SupportedListeners.Any())
                    {
                        Console.WriteLine("Invalid client listeners");
                    }
                }

                break;
            }
            case {EventId: EventIdConstants.JoinEventId}:
            {
                // new clients join
                var isSuccess = Clients.TryAdd(receivedPacket.Sender.Value,
                    new WebSocketContextData
                    {
                        SocketContext = webSocketContext,
                        SupportedListeners = new Dictionary<string, byte>(),
                    });
                if (!isSuccess) return;

                var listenersMapData = Server.GetListenerNameMap();

                var packet = new Packet(EventIdConstants.ListenerNameMapEventId, listenersMapData);
                Send(receivedPacket.Sender.Value, packet);

                Server.OnNewClientJoined?.Invoke(webSocketContext);
                break;
            }
            case {EventId: EventIdConstants.AlreadyUsedEventId}:
            {
                // already used
                var packet = new Packet(EventIdConstants.AlreadyUsedEventId, "Already used");
                Send(receivedPacket.Sender.Value, packet);
                break;
            }
            default:
                _ = CheckListeners(receivedPacket);
                break;
        }
    }

    private static async Task CheckListeners(Packet packet)
    {
        if (packet.Sender is null)
        {
            return;
        }

        if (!Clients.TryGetValue(packet.Sender.Value, out var client))
        {
            Server.Logger?.Log(LogLevel.Warning, $"Jaguar: Client not found");
            return;
        }

        var stringEventName = Server.GetListenerNameMap()
            .FirstOrDefault(i => i.Value == packet.EventId).Key;

        if (string.IsNullOrEmpty(stringEventName))
        {
            Server.Logger?.Log(LogLevel.Warning, $"Jaguar: Event not found");
            return;
        }

        if (Server.Listeners.TryGetValue(stringEventName, out var normalTask))
        {
            var data = JsonSerializer.Deserialize(packet.Message, normalTask.RequestType);

            object? convertedSender = packet.Sender;
            if (normalTask.SenderType != typeof(WebSocketContextData))
            {
                if (client.User is null)
                {
                    Server.Logger?.Log(LogLevel.Error, $"Jaguar: user not submit");
                    return;
                }

                normalTask.Method?.Invoke(
                    normalTask.@object
                    , new[] {client.User, data});
            }
            else
            {
                normalTask.Method?.Invoke(
                    normalTask.@object
                    , new[] {client, data});
            }
        }
    }

    private static void Send(BigInteger sender, Packet packet)
    {
        if (!Clients.TryGetValue(sender, out var webSocketContext))
        {
            Server.OnError?.Invoke($"Jaguar: Client not found, {packet.EventId}");
            return;
        }

        if (packet.Message == null)
        {
            Server.OnError?.Invoke($"Jaguar: Message is null, {packet.EventId}");
            return;
        }

        Send(webSocketContext.SocketContext, packet);
    }

    internal static void Send(WebSocketContext sender, Packet packet)
    {
        if (packet.Message == null)
        {
            Server.OnError?.Invoke($"Jaguar: Message is null, {packet.EventId}");
            return;
        }

        var bytes = new[] {packet.EventId}
            .Concat(packet.Message.ToBytes())
            .Concat(new[] {Packet.SignEof})
            .ToArray();

        if (Server.MaxBufferSize < bytes.Length)
        {
            Server.OnError?.Invoke(
                $"Jaguar: MaxBufferSize is {Server.MaxBufferSize} but message size is {bytes.Length}");
            return;
        }

        sender.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true,
            CancellationToken.None);
    }


    public class EventIdConstants
    {
        public const byte NoneEventId = 0;
        public const byte JoinEventId = 1;
        public const byte AlreadyUsedEventId = 2;
        public const byte ListenerNameMapEventId = 3;
    }
}