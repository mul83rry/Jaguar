namespace Jaguar.Core.Dto;

public record Packet
{
    public string EventName { get; set; }
    public string Message { get; }

    public static Packet Create(string eventName, object message)
    {
        string messageObject = System.Text.Json.JsonSerializer.Serialize<object>(message);
        Console.WriteLine($"SerializedMessageObject : {messageObject}");
        return new Packet(eventName, messageObject);
    }

    public Packet(string eventName, string message)
    {
        EventName = eventName;
        Message = message;
    }
}