namespace Jaguar;

public static class Settings
{
    public static int MaxPacketSize = 200;
    public static int MaxPacketInQueue = 10000;
    public static TimeSpan DisconnectUserAfterDeActive = TimeSpan.FromSeconds(100);
    public static sbyte DelayBetweenSendPacketPerClient = 5;
}