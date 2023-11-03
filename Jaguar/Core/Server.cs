using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text;
using Jaguar.Core.Socket;
using Jaguar.Extensions;
using Jaguar.Listeners;
using Jaguar.Manager;
using Microsoft.Extensions.Logging;

namespace Jaguar.Core;

public class JaguarTask
{
    public Type? FunctionType { get; init; }
    public MethodInfo? Method;
    public Object? @object;
    public Type? SenderType;
}

public class Server
{
    public static Action<IPEndPoint>? OnNewClientJoined;
    public static Action<IPEndPoint>? OnClientExited;
    public static Action<string, int>? OnServerStarted;

    internal static readonly ConcurrentDictionary<string, JaguarTask> ListenersDic = new();
    internal static readonly ConcurrentDictionary<string, JaguarTask> CallBackListenersDic = new();

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

    public static void AddListenerNew(Assembly assembly)
    {
        #region Byte listener

        // Get all types in the assembly
        var types = assembly.GetTypes();

        // Filter the types to only include those that implement IByteListener
        var byteListeners = types.Where(t => t.GetInterfaces().Contains(typeof(IByteListener)));
        
        // var byteListeners = from x in assembly.GetTypes()
        //     let y = x.BaseType
        //     where y == typeof(IByteListener)
        //     select x;

        foreach (var listener in byteListeners)
        {
            var methodInfo = listener.GetMethod("OnMessageReceived");
            if (methodInfo == null) continue;

            var instance = Activator.CreateInstance(listener);
            listener.GetMethod("Config")?.Invoke(instance, Array.Empty<object>());

            // Check listener Instance
            if (IByteListener.Instance is null)
            {
                throw new InvalidDataException($"IByteListener.Instance is null");
            }
        }

        #endregion

        #region UnRegistered user listener

        var unRegisteredUserListeners = from x in assembly.GetTypes()
            let y = x.BaseType
            where !x.IsAbstract && !x.IsInterface &&
                  y is {IsGenericType: true} &&
                  y.GetGenericTypeDefinition() == typeof(UnRegisteredUserListener<>)
            select x;

        foreach (var listener in unRegisteredUserListeners)
        {
            var genericArguments = listener.BaseType?.GetGenericArguments();
            if (genericArguments == null || genericArguments.Length == 0) continue;
            var genericArgument = genericArguments[0];
            var methodInfo = listener.GetMethod("OnMessageReceived");
            if (methodInfo == null) continue;

            var instance = Activator.CreateInstance(listener);
            listener.GetMethod("Config")?.Invoke(instance, Array.Empty<object>());


            // Get the property info
            var propertyInfo = instance.GetType().GetProperty("Name");

            // Get the value of the property from the instance
            var name = (string) propertyInfo.GetValue(instance);

            // Check listener name
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Listener name is null or empty. Listener: {listener.FullName}");
            }

            var jaguarTask = new JaguarTask
            {
                FunctionType = genericArgument,
                Method = methodInfo,
                @object = instance,
                // ListenersManager = (ListenersManager) Activator.CreateInstance(listener)!,
                SenderType = typeof(IPEndPoint)
            };
            AddListener(name, jaguarTask);
        }

        #endregion
       
        #region Registered user listener

        var registeredUserListeners = from x in assembly.GetTypes()
            let y = x.BaseType
            where !x.IsAbstract && !x.IsInterface &&
                  y is {IsGenericType: true} &&
                  y.GetGenericTypeDefinition() == typeof(RegisteredUserListener<,>)
            select x;

        foreach (var listener in registeredUserListeners)
        {
            var genericArguments = listener.BaseType?.GetGenericArguments();
            if (genericArguments == null || genericArguments.Length == 0) continue;
            var genericArgument1 = genericArguments[0];
            var genericArgument2 = genericArguments[1];
            var methodInfo = listener.GetMethod("OnMessageReceived");
            if (methodInfo == null) continue;

            var instance = Activator.CreateInstance(listener);
            listener.GetMethod("Config")?.Invoke(instance, Array.Empty<object>());


            // Get the property info
            var propertyInfo = instance.GetType().GetProperty("Name");

            // Get the value of the property from the instance
            var name = (string) propertyInfo.GetValue(instance);

            // Check listener name
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Listener name is null or empty. Listener: {listener.FullName}");
            }

            // var parameters = methodInfo.GetParameters();

            var jaguarTask = new JaguarTask
            {
                FunctionType = genericArgument2,
                Method = methodInfo,
                @object = instance,
                SenderType = genericArgument1
            };
            AddListener(name, jaguarTask);
        }

        #endregion
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
    /// <param name="jaguarTask">server invoke this event after eventName called.</param>
    internal static bool AddListener(string eventName, JaguarTask jaguarTask)
    {
        return ListenersDic.TryAdd(eventName, jaguarTask);
    }

    /// <summary>
    /// add an listener for a new 'eventName'.
    /// </summary>
    /// <param name="eventName">server listen to this eventName.</param>
    /// <param name="jaguarTask">server invoke this event after eventName called.</param>
    internal static bool AddAsyncListener(string eventName, JaguarTask jaguarTask)
    {
        return CallBackListenersDic.TryAdd(eventName, jaguarTask);
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

    public static void SendReliable(IPEndPoint client, string eventName, object message,
        Action<uint>? onPacketsArrived = null)
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