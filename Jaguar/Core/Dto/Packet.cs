using System.Numerics;
using Jaguar.Core.Handlers;
using Newtonsoft.Json;

namespace Jaguar.Core.Dto;

public record Packet
{
    // 0: none,
    // 1: join,
    // 2: already used
    public byte EventId { get; internal init; }
    public string? Message { get; }
    public BigInteger? Sender { get; init; }

    public Packet(byte[] data)
    {
        if (data.Length < 8)
        {
            EventId = 0;
            Message = "";
            Sender = null;
            return;
        }


        Sender = new BigInteger(data.Take(7).ToArray());

        data = data.Skip(7).ToArray();

        EventId = data[0];

        data = data.Skip(1).ToArray();

        Message = data.Any() ? Server.Encoding.GetString(data) : string.Empty;
    }

    public Packet(byte eventId, object message)
    {
        EventId = eventId;
        Message = JsonConvert.SerializeObject(message);
    }
    
    public Packet(string eventName, object message)
    {
        EventId = Listener.ListenersNameToId[eventName];
        Message = JsonConvert.SerializeObject(message);
    }
}