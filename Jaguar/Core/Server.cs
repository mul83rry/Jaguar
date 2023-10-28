using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text;
using Jaguar.Core.Socket;
using Jaguar.Extensions;
using Jaguar.Manager;
using Microsoft.Extensions.Logging;

namespace Jaguar.Core;

public class MuTask
{
    public Type? FunctionType { get; init; }
    public MethodInfo? Method;
    public ListenersManager? ListenersManager;
    public Type? SenderType;
}

public class Server
{
    public static Action<IPEndPoint>? OnNewClientJoined;
    public static Action<IPEndPoint>? OnClientExited;
    public static Action<string, int>? OnServerStarted;

    internal static readonly ConcurrentDictionary<string, MuTask> ListenersDic = new();
    internal static readonly ConcurrentDictionary<string, MuTask> CallBackListenersDic = new();

    private static ImmutableList<long> _usersUniqueId = ImmutableList<long>.Empty;


    private static readonly ConcurrentDictionary<string, ClientDic> Clients = new();

    internal static ILogger? Logger;


    public static Dictionary<string, ClientDic?> GetClients() => new(Clients!);

    public static int ClientsCount => GetClients().Count;

    public static int GetActiveClientsCount(TimeSpan time)
    {
        var dateTime = DateTime.UtcNow.Add(-time);
        return Clients.Count(c => c.Value.LastActivateTime >= dateTime);
    }

    internal static string Ip = null!;
    internal static int Port;

    public Server(string ip, int port)
    {
        if (string.IsNullOrEmpty(ip) || string.IsNullOrWhiteSpace(ip))
            throw new ArgumentNullException(nameof(ip));

        Ip = ip;
        Port = port;
        UdpSocket.Server = this;
    }

    protected Server(string ip, int port, ILogger logger)
    {
        if (string.IsNullOrEmpty(ip) || string.IsNullOrWhiteSpace(ip))
            throw new ArgumentNullException(nameof(ip));

        Ip = ip;
        Port = port;
        UdpSocket.Server = this;
        Logger = logger;
    }

    public static void AddListener<T>() where T : ListenersManager
    {
        _ = (T)Activator.CreateInstance(typeof(T), Array.Empty<object>())!;
    }

    /// <summary>
    /// Start the server
    /// </summary>
    public void Start()
    {
        UdpSocket.Start();
    }

    /// <summary>
    /// decoding and encoding type
    /// </summary>
    public static Encoding Encoding { internal get; set; } = Encoding.UTF8;

    /// <summary>
    /// add an listener for a new 'eventName'.
    /// </summary>
    /// <param name="eventName">server listen to this eventName.</param>
    /// <param name="muTask">server invoke this event after eventName called.</param>
    internal static bool AddListener(string eventName, MuTask muTask)
    {
        return ListenersDic.TryAdd(eventName, muTask);
    }

    /// <summary>
    /// add an listener for a new 'eventName'.
    /// </summary>
    /// <param name="eventName">server listen to this eventName.</param>
    /// <param name="muTask">server invoke this event after eventName called.</param>
    internal static bool AddAsyncListener(string eventName, MuTask muTask)
    {
        return CallBackListenersDic.TryAdd(eventName, muTask);
    }

    public static void Send(User user, string eventName, object message)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        if (eventName == null) throw new ArgumentNullException(nameof(eventName));
        if (message == null) throw new ArgumentNullException(nameof(message));

        //if (Clients.ContainsKey(user.Client.ConvertToKey()))
        if (Clients.TryGetValue(user.Client.ConvertToKey(), out var usr))
            usr.PacketSender.SendPacket(eventName, message);
    }

    public static void SendBytes(User? user, byte[] bytes)
    {
        if (user?.Client == null) return;
        //if (Clients.ContainsKey(user.Client.ConvertToKey()))
        if (Clients.TryGetValue(user.Client.ConvertToKey(), out var usr))
            UdpSocket.SendBytes(usr.Client, bytes);
    }

    public static void SendBytes(IPEndPoint? client, byte[] bytes)
    {
        if (Clients.ContainsKey(client.ConvertToKey())) // Todo: need?
            UdpSocket.SendBytes(client, bytes);
    }

    public static void SendReliable(User? user, string eventName, object message, Action<uint>? onPacketsArrived = null)
    {
        if (user == null) return;
        //if (Clients.ContainsKey(user.Client.ConvertToKey()))
        if (Clients.TryGetValue(user.Client.ConvertToKey(), out var usr))
            usr.PacketSender.SendReliablePacket(eventName, message, onPacketsArrived);
    }

    public static void Send(IPEndPoint client, string eventName, object message)
    {
        //if (Clients.ContainsKey(client.ConvertToKey()))
        if (Clients.TryGetValue(client.ConvertToKey(), out var usr))
            usr.PacketSender.SendPacket(eventName, message);
    }

    public static void SendReliable(IPEndPoint client, string eventName, object message, Action<uint>? onPacketsArrived = null)
    {
        //if (Clients.ContainsKey(client.ConvertToKey()))
        if (Clients.TryGetValue(client.ConvertToKey(), out var usr))
            usr.PacketSender.SendReliablePacket(eventName, message, onPacketsArrived);

    }

    internal static void UpdateClient(User? user, IPEndPoint? sender)
    {
        if (Clients.ContainsKey(sender.ConvertToKey()))
        {
            Clients[sender.ConvertToKey()].User = user ?? throw new NullReferenceException("User not found");
        }
    }

    internal static long GenerateUniqueUserId()
    {
        var id = new Random().Next(100000000, 999999999);

        while (true)
        {
            if (_usersUniqueId.All(ui => ui != id))
                break;

            id = new Random().Next(100000000, 999999999);
        }

        _usersUniqueId = _usersUniqueId.Add(id);

        return id;
    }

    public static void RemoveClient(string clientKey)
    {
        Clients.TryRemove(clientKey, out _);
    }

    public static void AddClient(string senderKey, ClientDic clientDic)
    {
        Clients.TryAdd(senderKey, clientDic);
    }

    public static void RemoveUsersUniqueId(long userUniqueId)
    {
        _usersUniqueId = _usersUniqueId.Remove(userUniqueId);
    }
}