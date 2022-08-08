using System.Net;

namespace Jaguar.Core.Data
{
    internal class PacketInQueue
    {
        internal PacketInQueue(uint packetIndex, uint packetLength, string eventName, string? message, IPEndPoint? sender, byte signIndex)
        {
            PacketIndex = packetIndex;
            PacketLength = packetLength;
            EventName = eventName;
            Message = message;
            Sender = sender;
            SignIndex = signIndex;
        }

        internal uint PacketIndex { get; set; }
        internal uint PacketLength { get; set; }
        internal string EventName { get; set; }
        internal string? Message { get; set; }
        internal IPEndPoint? Sender { get; set; }
        internal byte SignIndex { get; set; }
    }
}