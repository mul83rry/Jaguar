using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Jaguar.Core.Data
{
    public struct Packet
    {
        public Packet(uint index, string eventName, string? message, bool reliable, byte signIndex) : this()
        {
            Index = index;
            EventName = eventName;
            Message = message;
            Reliable = reliable;

            SignIndex = signIndex;

            BigData = StarterPack = false;
            Length = 0;
        }

        public Packet(uint index, string eventName, string? message, bool reliable, bool bigData, bool starterPack, uint length, byte signIndex) : this(index, eventName, message, reliable, signIndex)
        {
            BigData = bigData;
            StarterPack = starterPack;
            Length = length;
        }

        public Packet(byte[] data)
        {
            OnPacketArrived = null;
            Reliable = data[0] >= 1;
            if (Reliable)
            {
                var index = 6;
                SignIndex = (byte)(data[0] - 1);
                var eventNameLength = data[1];
                BigData = data[2] == 1;
                StarterPack = data[3] == 1;
                var indexLength = data[4];
                Length = data[5];

                var indexAsByteArray = new byte[indexLength];
                Array.Copy(data, index, indexAsByteArray, 0, indexLength);
                Index = uint.Parse(Server.Encoding.GetString(indexAsByteArray));
                index += indexLength;


                var eventNameAsByteArray = new byte[eventNameLength];
                Array.Copy(data, index, eventNameAsByteArray, 0, eventNameLength);
                EventName = Server.Encoding.GetString(eventNameAsByteArray);
                index += eventNameLength;


                var messageAsByteArray = new byte[data.Length - index];
                Array.Copy(data, index, messageAsByteArray, 0, data.Length - index);
                Message = Server.Encoding.GetString(messageAsByteArray);
            }
            else
            {
                var index = 2;

                Index = 0;
                SignIndex = 0;
                var eventNameLength = data[1];
                var eventNameAsByteArray = new byte[eventNameLength];
                Array.Copy(data, index, eventNameAsByteArray, 0, eventNameLength);
                EventName = Server.Encoding.GetString(eventNameAsByteArray);
                index += eventNameLength;


                var messageAsByteArray = new byte[data.Length - index];
                Array.Copy(data, index, messageAsByteArray, 0, data.Length - index);
                Message = Server.Encoding.GetString(messageAsByteArray);

                BigData = false;
                StarterPack = false;
                Length = 0;
            }

            Sender = null;
        }


        [JsonProperty("I")] public uint Index { get; set; }
        [JsonProperty("E")] public string EventName { get; set; }
        [JsonProperty("M")] public string? Message { get; set; }
        [JsonProperty("R")] public bool Reliable { get; set; }
        [JsonProperty("SI")] public byte SignIndex { get; set; } // Start from '1'
        [JsonProperty("B")] public bool BigData { get; set; }
        [JsonProperty("S")] public bool StarterPack { get; set; }
        [JsonProperty("L")] public uint Length { get; set; }

        [JsonIgnore] public IPEndPoint? Sender { get; set; }
        [JsonIgnore] public Action<uint>? OnPacketArrived { get; set; }

        public byte[] ToByte()
        {
            if (Message == null) throw new NullReferenceException("Message property is empty");

            else
            {
                var messageAsByteArray = Encoding.UTF8.GetBytes(Message);
                var eventNameAsByteArray = Encoding.UTF8.GetBytes(EventName);

                byte[] data;
                if (Reliable)
                {
                    var packetHeaderSize = 6;
                    var indexAsByteArray = Encoding.UTF8.GetBytes(Index.ToString());

                    data = new byte[messageAsByteArray.Length + packetHeaderSize + eventNameAsByteArray.Length +
                                    indexAsByteArray.Length];

                    data[0] = (byte)(Reliable ? (1 + SignIndex) : 0);
                    data[1] = (byte)EventName.Length;
                    data[2] = (byte)(BigData ? 1 : 0);
                    data[3] = (byte)(StarterPack ? 1 : 0);
                    data[4] = (byte)indexAsByteArray.Length;
                    data[5] = (byte)Length;


                    Array.Copy(indexAsByteArray, 0, data, packetHeaderSize, indexAsByteArray.Length);
                    packetHeaderSize += indexAsByteArray.Length;
                    Array.Copy(eventNameAsByteArray, 0, data, packetHeaderSize, eventNameAsByteArray.Length);
                    Array.Copy(messageAsByteArray, 0, data, packetHeaderSize + eventNameAsByteArray.Length,
                        messageAsByteArray.Length);
                }
                else
                {
                    const int packetHeaderSize = 2;
                    data = new byte[messageAsByteArray.Length + packetHeaderSize + eventNameAsByteArray.Length];

                    data[0] = (byte)(Reliable ? 1 : 0);
                    data[1] = (byte)EventName.Length;

                    Array.Copy(eventNameAsByteArray, 0, data, packetHeaderSize, eventNameAsByteArray.Length);
                    Array.Copy(messageAsByteArray, 0, data, packetHeaderSize + eventNameAsByteArray.Length,
                        messageAsByteArray.Length);
                }

                return data;
            }
        }
    }
}