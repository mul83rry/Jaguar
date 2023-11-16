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

internal class WebSocket
{
    private static int _maxBufferSize;
    private static string _uri;

    #region Constants

    private const byte NoneEventId = 0;
    private const byte JoinEventId = 1;
    private const byte AlreadyUsedEventId = 2;

    #endregion

    private static Dictionary<BigInteger, WebSocketContext> _clients = new();

    // internal WebSocket(string uri = "http://localhost:5000/", int maxBufferSize = 1024)
    internal WebSocket(string uri, int maxBufferSize)
    {
        _uri = uri;
        _maxBufferSize = maxBufferSize;
    }

    internal static void Start()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(_uri);
        listener.Start();
        Console.WriteLine("Listening...");

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

    private static async void ProcessRequest(HttpListenerContext listenerContext)
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
                }
                else
                {
                    // var message = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                    // var sendBuffer = Encoding.UTF8.GetBytes("You sent: " + message);

                    // manage received message
                    Handle(webSocketContext, receiveBuffer);


                    // await webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(sendBuffer),
                    //     WebSocketMessageType.Text, endOfMessage: true, cancellationToken: CancellationToken.None);
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

        if (receivedPacket.EventId == NoneEventId)
        {
            // Invalid packet
            return;
        }

        switch (receivedPacket)
        {
            case {EventId: JoinEventId, Sender: not null}:
            {
                // new clients join
                var isSuccess = _clients.TryAdd(receivedPacket.Sender.Value, webSocketContext);
                if (!isSuccess) return;

                var packet = new Packet(JoinEventId, "Successfully join");
                Send(receivedPacket.Sender.Value, packet);

                Server.OnNewClientJoined?.Invoke(webSocketContext);
                break;
            }
            case {EventId: AlreadyUsedEventId, Sender: not null}:
            {
                // already used
                var packet = new Packet(AlreadyUsedEventId, "Already used");
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

        var clients = Server.GetClients();

        var stringEventName = ""; // Todo: Get from hash list (packet.EventId)

        if (Server.ListenersDic.TryGetValue(stringEventName, out var normalTask))
        {
            var data = JsonSerializer.Deserialize(packet.Message, normalTask.RequestType);

            object? convertedSender = packet.Sender;
            if (normalTask.SenderType != typeof(WebSocketContext))
            {
                if (normalTask.SenderType != null)
                {
                    convertedSender = Convert.ChangeType(
                        UsersManager.FindUser(packet.Sender),
                        normalTask.SenderType);
                }

                if (convertedSender == null)
                {
                    Server.Logger?.Log(LogLevel.Error, $"Jaguar: user not submit");
                }

                normalTask.Method?.Invoke(
                    normalTask.@object
                    , new[] {convertedSender, data});
            }
            else
            {
                if (!clients.TryGetValue(packet.Sender.Value, out var client))
                {
                    Server.Logger?.Log(LogLevel.Warning, $"Jaguar: Client not found");
                    return;
                }

                if (client is null)
                {
                    Server.Logger?.Log(LogLevel.Error, $"Client is null");
                }

                normalTask.Method?.Invoke(
                    normalTask.@object
                    , new[] {client.Client, data});
            }
        }
    }

    internal static void Send(BigInteger sender, Packet packet)
    {
        if (!_clients.TryGetValue(sender, out var webSocketContext)) return;

        if (packet.Message == null) return;

        var bytes = packet.Message.ToBytes();
        webSocketContext.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    internal static void Send(WebSocketContext sender, Packet packet)
    {
        if (packet.Message == null) return;

        var bytes = packet.Message.ToBytes();
        sender.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }
}