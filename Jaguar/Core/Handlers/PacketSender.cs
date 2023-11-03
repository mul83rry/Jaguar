using System.Collections.Concurrent;
using System.Net;
using Jaguar.Core.Dto;
using Jaguar.Core.Socket;
using Jaguar.Extensions;
using Microsoft.Extensions.Logging;
using JsonConverter = System.Text.Json.JsonSerializer;

namespace Jaguar.Core.Handlers;

internal class PacketSender
{
    private readonly IPEndPoint _ipEndPoint;
    private readonly ClientDic _clientDic;
    private readonly ConcurrentDictionary<uint, bool> _sentPacketsSituation = new();

    private readonly ConcurrentQueue<Packet> _packetsInQueue = new();
    private uint _packetIndex;

    private readonly ConcurrentDictionary<uint, Action<uint>?> _onReliablePacketsArrived = new();
    private bool _destroyed;

    public static uint PostManagementCounter;
    
    public bool Destroyed => _destroyed;
    
    internal void Destroy()
    {
        _destroyed = true;
        PostManagementCounter--;
    }

    internal PacketSender(IPEndPoint ipEndPoint, ClientDic clientDic)
    {
        _ipEndPoint = ipEndPoint;
        _clientDic = clientDic;
    }

    static PacketSender()
    {
        PostManagementCounter++;
    }

    // internal void Init() => _ = StartReliablePacketsServiceAsync();

    internal void SendReliablePacket(string eventName, object message, Action<uint>? onPacketArrived = null)
    {
        var msg = message is string ? message.ToString() : JsonConverter.Serialize(message);

        var packet = new Packet(0, eventName, msg, true, 0) {Sender = _ipEndPoint};
        if (onPacketArrived != null)
            packet.OnPacketArrived = onPacketArrived;

        _packetsInQueue.Enqueue(packet);
    }

    internal void SendReliablePacket(string eventName, object message, byte signIndex,
        Action<uint>? onPacketArrived = null)
    {
        var msg = message is string ? message.ToString() : JsonConverter.Serialize(message);

        var packet = new Packet(0, eventName, msg, true, signIndex) {Sender = _ipEndPoint};
        if (onPacketArrived != null)
            packet.OnPacketArrived = onPacketArrived;

        _packetsInQueue.Enqueue(packet);
    }

    internal void SendPacket(string eventName, object message)
    {
        var msg = message is string ? message.ToString() : JsonConverter.Serialize(message);

        var packet = new Packet(0, eventName, msg, false, 0) {Sender = _ipEndPoint};
        SendPacket(packet);
    }

    internal async Task StartReliablePacketsServiceAsync()
    {
        while (!_destroyed)
        {
            if (_clientDic.LastActivateTime.Add(Settings.DisconnectUserAfterDeActive) < DateTime.UtcNow)
            {
                // Destroy
                _destroyed = true;
                break;
            }

            while (!_packetsInQueue.IsEmpty)
            {
                _packetsInQueue.TryDequeue(out var packet);

                if (packet.Message is null)
                {
                    Server.Logger?.LogError($"Invalid message \n{JsonConverter.Serialize(packet)}");
                    continue;
                }

                if (!packet.Reliable) continue;
                // string?[] fragmentedMessage = ChunksUpTo(packet.Message, Settings.MaxPacketSize).ToArray();
                string?[] fragmentedMessage = packet.Message.ChunksUpTo(Settings.MaxPacketSize).ToArray();
                var packets = new Packet[Math.Max(1, fragmentedMessage.Length)];

                packets[0] = new Packet(_packetIndex++, packet.EventName,
                        message: fragmentedMessage.Length > 0 ? fragmentedMessage[0] : string.Empty,
                        reliable: true,
                        bigData: packets.Length > 1,
                        starterPack: true,
                        length: (uint) packets.Length,
                        packet.SignIndex)
                    {Sender = packet.Sender};

                if (packet.OnPacketArrived is not null)
                {
                    packets[0].OnPacketArrived = packet.OnPacketArrived;
                    _onReliablePacketsArrived.TryAdd(packets[0].Index, packets[0].OnPacketArrived);
                }

                for (uint i = 1; i < fragmentedMessage.Length; i++)
                {
                    packets[i] = new Packet(_packetIndex++,
                            packet.EventName,
                            message: fragmentedMessage[i],
                            reliable: true,
                            bigData: packets[0].BigData,
                            starterPack: false,
                            length: (uint) (packets.Length - i), 0)
                        {Sender = packet.Sender};
                }

                for (uint i = 0; i < packets.Length; i++)
                {
                    _ = SendPacketAndWaitForResponse(packets[i]);
                }
            }

            await Task.Delay(Settings.DelayBetweenSendPacketPerClient);
        }

        Console.WriteLine("Destroyed");
    }

    private async Task SendPacketAndWaitForResponse(Packet packet)
    {
        _sentPacketsSituation.TryAdd(packet.Index, false);

        var sent = _sentPacketsSituation[packet.Index];
        while (!sent && !_destroyed)
        {
            SendPacket(packet);
            await Task.Delay(250);
            try
            {
                if (_sentPacketsSituation.TryGetValue(packet.Index, out var value))
                {
                    sent = value;
                }
            }
            catch
            {
                sent = false;
            }
        }

        _sentPacketsSituation.TryRemove(packet.Index, out _);

        if (!packet.StarterPack) return;
        if (!_onReliablePacketsArrived.ContainsKey(packet.Index)) return;

        for (var i = packet.Index; i < packet.Index + packet.Length; i++)
        {
            {
                if (_sentPacketsSituation.TryGetValue(i, out var value) && value) continue;
            }
            {
                if (_sentPacketsSituation.TryGetValue(i, out var value) && !value)
                    return;
            }
        }

        _onReliablePacketsArrived.TryRemove(packet.Index, out var messageArrived);
        messageArrived?.Invoke(packet.Index);
    }

    private void SendPacket(Packet packet)
    {
        if (packet.Reliable)
        {
            if (!_sentPacketsSituation.ContainsKey(packet.Index)) return;
            if (_sentPacketsSituation[packet.Index]) return;
        }

        UdpSocket.Send(packet.Sender, packet);
    }

    internal void PacketReceivedCallBack(uint index)
    {
        if (_sentPacketsSituation.TryGetValue(index, out var res) && !res)
        {
            _sentPacketsSituation[index] = true;
        }
        // if (!_sentPacketsSituation.ContainsKey(index)) return;
        // if (_sentPacketsSituation[index]) return;
    }
}