using System.Net;
using System.Net.Sockets;
using Jaguar.Core.Dto;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Jaguar.Core.LiteNetLib;

internal class LiteNetLibServer : INetEventListener
{
    private NetManager _server;
    private NetDataWriter _writer;
    private static Dictionary<int, ClientData> _peersById;
    private int _port;

    internal LiteNetLibServer(int port)
    {
        _server = new NetManager(this)
        {
            AutoRecycle = true
        };
        _port = port;
        
        _peersById = new Dictionary<int, ClientData>();
    }

    internal void Start()
    {
        Console.WriteLine("Server started.");

        _server.Start(_port /* port */);
        while (true)
        {
            _server.PollEvents();
            System.Threading.Thread.Sleep(15);
        }
    }

    public static ClientData FindPeerById(int peerId)
    {
        if (_peersById.TryGetValue(peerId, out var peer))
        {
            return peer;
        }
        return null;
    }
    
    public void OnPeerConnected(NetPeer peer)
    {
        _peersById.TryAdd(peer.Id, new ClientData(null, peer));
        _peersById[peer.Id] = new ClientData(null, peer);
        Console.WriteLine($"We have a new connection from {peer.Address}:{peer.Port}");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        // Remove the peer from tracking
        if (_peersById.ContainsKey(peer.Id))
        {
            _peersById.Remove(peer.Id);
        }
        Console.WriteLine($"Peer disconnected: {peer.Address}:{peer.Port}");
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Console.WriteLine($"Network error: {socketError}");
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        string message = reader.GetString();
        Console.WriteLine($"Received: {message}");
        reader.Recycle();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("SomeConnectionKey");
    }

    internal void SendMessage(NetPeer peer, Packet packet,
        DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        if (_writer == null)
        {
            _writer = new NetDataWriter();
        }

        _writer.Reset();
        _writer.Put(System.Text.Json.JsonSerializer.Serialize(packet));
        peer.Send(_writer, deliveryMethod);
    }


    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        string message = reader.GetString();
        Console.WriteLine("[Server] Received: " + message);
        reader.Recycle();
    }
}