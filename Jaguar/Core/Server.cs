using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Jaguar.Core.Dto;
using Jaguar.Core.WebSocket;
using Jaguar.Listeners;
using Microsoft.Extensions.Logging;

namespace Jaguar.Core;

public class Server
{
    public static Action<WebSocketContext>? OnNewClientJoined;
    public static Action<WebSocketContext>? OnClientExited;
    public static Action<string>? OnError;
    public static Action? OnServerStarted;
    
    public static int MaxBufferSize { get; set; } = 1024;

    internal static readonly ConcurrentDictionary<string, JaguarTask> Listeners = new();
    internal static readonly Dictionary<byte, string> ClientListeners = new();

    private static ImmutableList<long> _usersUniqueId = ImmutableList<long>.Empty;


    // private static readonly ConcurrentDictionary<BigInteger, ClientData> Clients = new(); // socketId, clientData

    internal static ILogger? Logger;


    // public static Dictionary<BigInteger, ClientData?> GetClients() => new(Clients!);

    internal WebSocket.WebSocket WebSocket;
    
    public Server(string address, ILogger _logger)
    {
        if (string.IsNullOrEmpty(address) || string.IsNullOrWhiteSpace(address))
            throw new ArgumentNullException(nameof(address));

        _logger = Logger;

        var uri = address;
        WebSocket = new WebSocket.WebSocket(address, MaxBufferSize);
    }

    // 0: none,
    // 1: join,
    // 2: already used,
    // 3: listenersSetting
    public static Dictionary<string, byte> GetListenerNameMap()
    {
        var sequenceNumber = 4;
        return Listeners.ToDictionary(i => i.Key,
            _ => (byte) sequenceNumber++);
    }


    public static void AddListeners(Assembly assembly)
    {
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
                RequestType = genericArgument,
                Method = methodInfo,
                @object = instance,
                // ListenersManager = (ListenersManager) Activator.CreateInstance(listener)!,
                SenderType = typeof(WebSocketContextData)
            };
            AddListener(name, jaguarTask);
        }

        #endregion

        #region Registered user listener

        var registeredUserListeners = from x in assembly.GetTypes()
            let y = x.BaseType
            where !x.IsAbstract && !x.IsInterface &&
                  y is {IsGenericType: true} &&
                  (
                      y.GetGenericTypeDefinition() == typeof(RegisteredUserListener<,>)
                      || y.GetGenericTypeDefinition() == typeof(RegisteredUserListener<,,>)
                  )
            select x;

        foreach (var listener in registeredUserListeners)
        {
            var genericArguments = listener.BaseType?.GetGenericArguments();
            if (genericArguments == null || genericArguments.Length == 0) continue;

            var methodInfo = listener.GetMethod("OnMessageReceived");
            if (methodInfo == null) continue;

            var genericArgument0 = genericArguments[0];
            var genericArgument1 = genericArguments[1];


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


            // Callback listener
            if (genericArguments.Length == 3)
            {
                var genericArgument2 = genericArguments[2];

                var jaguarTask = new JaguarTask
                {
                    RequestType = genericArgument1,
                    ResponseType = genericArgument2,
                    Method = methodInfo,
                    @object = instance,
                    SenderType = genericArgument0
                };
            }
            else
            {
                var jaguarTask = new JaguarTask
                {
                    RequestType = genericArgument1,
                    Method = methodInfo,
                    @object = instance,
                    SenderType = genericArgument0
                };
                AddListener(name, jaguarTask);
            }
        }

        #endregion
    }

    /// <summary>
    /// Start the server
    /// </summary>
    public void Start()
    {
        WebSocket.Start();
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
        return Listeners.TryAdd(eventName, jaguarTask);
    }

    public static void Send(User user, string eventName, object message)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        if (eventName == null) throw new ArgumentNullException(nameof(eventName));
        if (message == null) throw new ArgumentNullException(nameof(message));


        var packet = new Packet(user.Client, eventName, message);
        Core.WebSocket.WebSocket.Send(user.Client.SocketContext, packet);
    }

    public static void Send(WebSocketContextData client, string eventName, object message)
    {
        var packet = new Packet(client, eventName, message);
        Core.WebSocket.WebSocket.Send(client.SocketContext, packet);
    }

    internal static void UpdateClient(User user, WebSocketContextData? sender)
    {
        user.Client = sender;
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

    // public static void RemoveClient(BigInteger clientKey)
    // {
    //     Clients.TryRemove(clientKey, out _);
    // }
    //
    // public static void AddClient(BigInteger senderKey, ClientData clientData)
    // {
    //     Clients.TryAdd(senderKey, clientData);
    // }

    public static void RemoveUsersUniqueId(long userUniqueId)
    {
        _usersUniqueId = _usersUniqueId.Remove(userUniqueId);
    }
}