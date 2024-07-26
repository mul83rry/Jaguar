using System.Numerics;
using Jaguar.Core.WebSocket;
using LiteNetLib;
using Newtonsoft.Json;

namespace Jaguar.Core.Dto;

public record Packet
{
    public string EventName { get; set; }
    public string? Message { get; }

    public static Packet Create(string eventName, object message)
    {
        return new Packet(eventName, JsonConvert.SerializeObject(message));
    }

    public Packet(string eventName, string? message)
    {
        EventName = eventName;
        Message = message;
    }
}