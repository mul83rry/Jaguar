using System.Numerics;
using Jaguar.Core.Handlers;
using Jaguar.Core.Socket;
using Newtonsoft.Json;

namespace Jaguar.Core.Dto;

public record Packet
{
    // 200: last byte index
    internal const byte SignEof = 200;

    // 0: none,
    // 1: join,
    // 2: already used
    public byte EventId { get; set; }
    public string? Message { get; }
    public BigInteger? Sender { get; init; }

    public Packet(byte[] data)
    {
        Sender = new BigInteger(data.Take(7).ToArray());

        data = data.Skip(7).ToArray();

        EventId = data[0];

        data = data.Skip(1).ToArray();

        var index = Array.IndexOf(data, SignEof);

        if (index == 0)
        {
            data = Array.Empty<byte>();
        }
        else
        {
            data = data.Take(index)
                .ToArray();
        }

        Message = data.Any() ? Server.Encoding.GetString(data) : string.Empty;
    }

    public Packet(byte eventId, object message)
    {
        EventId = eventId;
        Message = JsonConvert.SerializeObject(message);
    }

    public Packet(WebSocketContextData client, string eventName, object message)
    {
        if (!client.SupportedListeners.TryGetValue(eventName, out var eventId)) return;

        EventId = eventId;
        Message = JsonConvert.SerializeObject(message);
    }
}