using System.Collections.Concurrent;
using System.Net;
using Jaguar.Core.Data;
using Jaguar.Core.Socket;
using Newtonsoft.Json;

namespace Jaguar.Core.Processor;

internal class PostManagement
{
    private readonly IPEndPoint _ipEndPoint;
    private readonly ConcurrentDictionary<uint, bool> _sentPacketsSituation = new();

    private readonly ConcurrentQueue<Packet> _packetsInQueue = new();
    private uint _packetIndex;

    private readonly ConcurrentDictionary<uint, Action<uint>?> _onReliablePacketsArrived = new();
    private bool _destroyed;


    internal void Destroy()
    {
        _destroyed = true;
    }

    internal PostManagement(IPEndPoint ipEndPoint)
    {
        _ipEndPoint = ipEndPoint;
    }

    internal void Init() => _ = SendReliablePacketsAsync();

    internal void SendReliablePacket(string eventName, object message, Action<uint>? onPacketArrived = null)
    {
        var msg = message is string ? message.ToString() : JsonConvert.SerializeObject(message);

        var packet = new Packet(0, eventName, msg, true, 0) { Sender = _ipEndPoint };
        if (onPacketArrived != null)
            packet.OnPacketArrived = onPacketArrived;

        _packetsInQueue.Enqueue(packet);
    }

    internal void SendReliablePacket(string eventName, object message, byte signIndex, Action<uint>? onPacketArrived = null)
    {
        var msg = message is string ? message.ToString() : JsonConvert.SerializeObject(message);

        var packet = new Packet(0, eventName, msg, true, signIndex) { Sender = _ipEndPoint };
        if (onPacketArrived != null)
            packet.OnPacketArrived = onPacketArrived;

        _packetsInQueue.Enqueue(packet);

    }

    internal void SendPacket(string eventName, object message)
    {
        var msg = message is string ? message.ToString() : JsonConvert.SerializeObject(message);

        var packet = new Packet(0, eventName, msg, false, 0) { Sender = _ipEndPoint };
        SendPacket(packet);
    }

    private async Task SendReliablePacketsAsync()
    {
        while (!_destroyed)
        {
            while (_packetsInQueue.Count > 0)
            {
                _packetsInQueue.TryDequeue(out var packet);

                if (!packet.Reliable) continue;
                string?[] fragmentedMessage = ChunksUpTo(packet.Message, Settings.MaxPacketSize).ToArray();
                var packets = new Packet[Math.Max(1, fragmentedMessage.Length)];

                packets[0] = new Packet(_packetIndex++, packet.EventName,
                        message: fragmentedMessage.Length > 0 ? fragmentedMessage[0] : "",
                        reliable: true,
                        bigData: packets.Length > 1,
                        starterPack: true,
                        length: (uint)packets.Length,
                        packet.SignIndex)
                    { Sender = packet.Sender };

                if (packet.OnPacketArrived is not null)
                {
                    packets[0].OnPacketArrived = packet.OnPacketArrived;
                    _onReliablePacketsArrived.TryAdd(packets[0].Index, packets[0].OnPacketArrived);
                }

                for (uint i = 1; i < fragmentedMessage.Length; i++)
                {
                    packets[i] = new Packet(_packetIndex++, packet.EventName,
                            message: fragmentedMessage[i],
                            reliable: true,
                            bigData: packets[0].BigData,
                            starterPack: false,
                            length: (uint)(packets.Length - i), 0)
                        { Sender = packet.Sender };
                }

                for (uint i = 0; i < packets.Length; i++)
                {
                    _ = SendPacketAndWaitForResponse(packets[i]);
                }
            }

            await Task.Delay(5);
        }
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
                if (_sentPacketsSituation.ContainsKey(packet.Index))
                {
                    sent = _sentPacketsSituation[packet.Index];
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
            if (_sentPacketsSituation.ContainsKey(i) && _sentPacketsSituation[i]) continue;
            if (_sentPacketsSituation.ContainsKey(i) && !_sentPacketsSituation[i])
                return;
        }           
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
        if (!_sentPacketsSituation.ContainsKey(index)) return;
        if (_sentPacketsSituation[index]) return;

        _sentPacketsSituation[index] = true;
    }

    private static IEnumerable<string> ChunksUpTo(string? str, int maxChunkSize)
    {
        if (str == null) yield break;
        for (var i = 0; i < str.Length; i += maxChunkSize)
        {
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }
    }

}