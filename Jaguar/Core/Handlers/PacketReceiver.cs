using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using Jaguar.Core.Dto;
using Jaguar.Core.Socket;
using JsonConverter = System.Text.Json.JsonSerializer;


namespace Jaguar.Core.Handlers;

internal class PacketReceiver
{
    internal PacketReceiver(ClientDic clientDic)
    {
        _receivedPacketsSituation = ImmutableDictionary.CreateBuilder<uint, bool>();
        _clientDic = clientDic;
    }

    private readonly ImmutableDictionary<uint, bool>.Builder _receivedPacketsSituation;
    private readonly SortedList<uint, Packet> _receivedReliablePackets = new();
    private readonly ConcurrentQueue<(uint, Packet)> _receivedReliablePacketsInQueue = new();
    private readonly SortedList<uint, PacketInQueue> _reliableMessagesSequenced = new();
    private readonly ClientDic _clientDic;
    private uint _lastReliableMessageIndexReceived;
    private bool _destroyed;

    public bool Destroyed => _destroyed;
    
    public long PacketsInQueue => _receivedReliablePackets.Count - _lastReliableMessageIndexReceived;


    // internal void Init() => _ = CheckSequenceDataAsync();

    internal void ReceivedReliablePacket(Packet packet)
    {
        // Console.WriteLine($"ReceivedReliablePacket: {packet.EventName} - {packet.Index}");
        if (_receivedPacketsSituation.TryGetValue(packet.Index, out var value))
        {
            if (value)
            {
                var log = JsonConverter.Serialize(_receivedPacketsSituation);
                // Console.WriteLine($"ReceivedReliablePacket: {log}");
                return;
            }

            // Console.WriteLine($"ReceivedReliablePacket: value is not null");
            SendReceivedCallBack(packet.Index,
                packet.Sender); // Send a callback that I have already received the message
            return;
        }

        _receivedPacketsSituation.TryAdd(packet.Index, false);


        SendReceivedCallBack(packet.Index, packet.Sender);


        #region test

        var found = _receivedReliablePackets.Any(p =>
            p.Value.Message is {Length: > 5} && p.Value.Message == packet.Message &&
            MathF.Abs(packet.Index - p.Value.Index) < 2);
        if (found)
        {
            var otherPacket = _receivedReliablePackets.FirstOrDefault(p =>
                p.Value.Message != null && p.Value.Message.Length > 5 && p.Value.Message == packet.Message &&
                MathF.Abs(packet.Index - p.Value.Index) < 2);
            var log =
                $"Packet_0:\n{JsonConverter.Serialize(packet)}\n\nPacket_1:\n{JsonConverter.Serialize(otherPacket)}";
            SaveLog(log);
        }

        #endregion

        //receivedReliablePackets.Add(packet.Index, packet); // Todo: add to a temp list, then in CheckSequenceDataAsync move temp data to receivedReliablePackets
        _receivedReliablePacketsInQueue.Enqueue((packet.Index, packet));
    }

    private async void SaveLog(string s)
    {
        // Todo: check await File.WriteAllTextAsync(@"C:\WriteLines.txt", s);
    }

    internal async Task CheckSequenceDataAsync()
    {
        while (!_destroyed)
        {
            if (_clientDic.LastActivateTime.AddSeconds(100) < DateTime.UtcNow)
            {
                // Destroy
                _destroyed = true;
                break;
            }

            while (!_receivedReliablePacketsInQueue.IsEmpty)
            {
                if (_receivedReliablePacketsInQueue.TryDequeue(out var result))
                {
                    // Console.WriteLine($"_receivedReliablePacketsInQueue: {JsonConvert.SerializeObject(result)}");
                    _receivedReliablePackets.Add(result.Item1, result.Item2);
                }
            }

            for (var i = _lastReliableMessageIndexReceived; i < _receivedReliablePackets.Count; i++)
            {
                var starterPacket = _receivedReliablePackets[_receivedReliablePackets.Keys[(int) i]];


                // Console.WriteLine($"starterPacket: {starterPacket.Index} - {_lastReliableMessageIndexReceived}");
                if (starterPacket.Index != _lastReliableMessageIndexReceived)
                {
                    // Console.WriteLine($"starterPacket: A1");
                    continue;
                }

                if (!starterPacket.StarterPack)
                {
                    // Console.WriteLine($"starterPacket: B1");
                    continue;
                }

                var message = new StringBuilder(starterPacket.Message);
                var eventName = starterPacket.EventName;
                var breakTheFor = false;
                for (var packetIndex = i + 1; packetIndex < i + starterPacket.Length; packetIndex++)
                {
                    if (!_receivedReliablePackets.ContainsKey(packetIndex))
                    {
                        breakTheFor = true;
                        break;
                    }

                    var nextPacket = _receivedReliablePackets[packetIndex];
                    message.Append(nextPacket.Message);
                }

                if (breakTheFor)
                {
                    break;
                }

                _reliableMessagesSequenced.Add(starterPacket.Index,
                    new PacketInQueue(starterPacket.Index, starterPacket.Length, eventName, message.ToString(),
                        starterPacket.Sender, starterPacket.SignIndex));

                CheckReliableMessagesSequenced();
                i += starterPacket.Length - 1;
            }

            await Task.Delay(5);
        }

        // Console.WriteLine("Destroyed");
    }

    private void CheckReliableMessagesSequenced()
    {
        // Console.WriteLine($"CheckReliableMessagesSequenced: Start");
        // Console.WriteLine($"CheckReliableMessagesSequenced: _lastReliableMessageIndexReceived");
        // Console.WriteLine($"CheckReliableMessagesSequenced: {JsonConvert.SerializeObject(_reliableMessagesSequenced)}");
        if (_lastReliableMessageIndexReceived != _reliableMessagesSequenced.Keys[0]) return;

        _lastReliableMessageIndexReceived +=
            _reliableMessagesSequenced[_reliableMessagesSequenced.Keys[0]].PacketLength;
        var data = _reliableMessagesSequenced[_reliableMessagesSequenced.Keys[0]];
        // Console.WriteLine($"data: {JsonConvert.SerializeObject(data)}");
        _reliableMessagesSequenced.RemoveAt(0);
        // Console.WriteLine($"CheckReliableMessagesSequenced2: {JsonConvert.SerializeObject(_reliableMessagesSequenced)}");

        // Console.WriteLine($"CheckReliableMessagesSequenced: Listener.OnMessageReceived is null = {Listener.OnMessageReceived is null}");
        var packet = new Packet(data.PacketIndex, data.EventName, data.Message, false, data.SignIndex);
        // Console.WriteLine($"CheckReliableMessagesSequenced: {data.PacketIndex} - {data.EventName} - {data.Message} - {data.SignIndex}");
        // Console.WriteLine($"Packet: {JsonConvert.SerializeObject(packet)}");
        if (Listener.OnMessageReceived != null)
        {
            Listener.OnMessageReceived.Invoke(data.Sender,
                packet);
            // Console.WriteLine($"CheckReliableMessagesSequenced: not null");
        }
        else
        {
            // Console.WriteLine($"CheckReliableMessagesSequenced: is null");
        }

        // Console.WriteLine($"CheckReliableMessagesSequenced: End");
    }

    private static void SendReceivedCallBack(uint packetIndex, IPEndPoint? sender) =>
        UdpSocket.Send(sender, new Packet(0, "PRC", packetIndex.ToString(), false, 0));

    internal void Destroy()
    {
        _destroyed = true;
    }
}