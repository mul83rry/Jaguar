using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using Jaguar.Core.Data;
using Jaguar.Core.Socket;
using Newtonsoft.Json;

namespace Jaguar.Core.Processor
{
    internal class ReceiptManagement
    {
        internal ReceiptManagement()
        {
            _receivedPacketsSituation = ImmutableDictionary.CreateBuilder<uint, bool>();
        }

        private readonly ImmutableDictionary<uint, bool>.Builder _receivedPacketsSituation;
        private readonly SortedList<uint, Packet> _receivedReliablePackets = new();
        private readonly ConcurrentQueue<(uint, Packet)> _receivedReliablePacketsInQueue = new();
        private readonly SortedList<uint, PacketInQueue> _reliableMessagesSequenced = new();

        private uint _lastReliableMessageIndexReceived;
        private bool _destroyed;

        public long PacketsInQueue => _receivedReliablePackets.Count - _lastReliableMessageIndexReceived;


        internal void Init() => _ = CheckSequenceDataAsync();

        internal void ReceivedReliablePacket(Packet packet)
        {
            if (_receivedPacketsSituation.ContainsKey(packet.Index))
            {
                if (!_receivedPacketsSituation[packet.Index])
                {
                    SendReceivedCallBack(packet.Index, packet.Sender); // Send a callback that I have already received the message
                }
                return;
            }

            _receivedPacketsSituation.TryAdd(packet.Index, false);


            SendReceivedCallBack(packet.Index, packet.Sender);


            #region test
            var found = _receivedReliablePackets.Any(p => p.Value.Message is { Length: > 5 } && p.Value.Message == packet.Message && MathF.Abs(packet.Index - p.Value.Index) < 2);
            if (found)
            {
                var otherPacket = _receivedReliablePackets.FirstOrDefault(p => p.Value.Message != null && p.Value.Message.Length > 5 && p.Value.Message == packet.Message && MathF.Abs(packet.Index - p.Value.Index) < 2);
                var log = $"Packet_0:\n{JsonConvert.SerializeObject(packet)}\n\nPacket_1:\n{JsonConvert.SerializeObject(otherPacket)}";
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

        private async Task CheckSequenceDataAsync()
        {
            while (!_destroyed)
            {
                await Task.Delay(5);

                while (_receivedReliablePacketsInQueue.Count > 0)
                {
                    if (_receivedReliablePacketsInQueue.TryDequeue(out var result))
                    {
                        _receivedReliablePackets.Add(result.Item1, result.Item2);
                    }
                }

                for (var i = _lastReliableMessageIndexReceived; i < _receivedReliablePackets.Count; i++)
                {

                    var starterPacket = _receivedReliablePackets[_receivedReliablePackets.Keys[(int)i]];


                    if (starterPacket.Index != _lastReliableMessageIndexReceived)
                    {
                        continue;
                    }

                    if (!starterPacket.StarterPack)
                    {
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

                    _reliableMessagesSequenced.Add(starterPacket.Index, new PacketInQueue(starterPacket.Index, starterPacket.Length, eventName, message.ToString(), starterPacket.Sender, starterPacket.SignIndex));

                    CheckReliableMessagesSequenced();
                    i += starterPacket.Length - 1;
                }
            }
        }

        private void CheckReliableMessagesSequenced()
        {
            if (_lastReliableMessageIndexReceived != _reliableMessagesSequenced.Keys[0]) return;
            _lastReliableMessageIndexReceived += _reliableMessagesSequenced[_reliableMessagesSequenced.Keys[0]].PacketLength;
            var data = _reliableMessagesSequenced[_reliableMessagesSequenced.Keys[0]];
            _reliableMessagesSequenced.RemoveAt(0);
            Listener.OnNewMessageReceived?.Invoke(data.Sender, new Packet(data.PacketIndex, data.EventName, data.Message, false, data.SignIndex));

        }

        private static void SendReceivedCallBack(uint packetIndex, IPEndPoint? sender) =>
            UdpSocket.Send(sender, new Packet(0, "PRC", packetIndex.ToString(), false, 0));

        internal void Destroy()
        {
            _destroyed = true;
        }
    }
}
