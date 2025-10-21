using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Jaguar.Core.Dto;
using Jaguar.Core.Entity;
using Jaguar.Core.Utils;
using Jaguar.Core.WebSocket;
using Jaguar.Listeners;

namespace Jaguar.Core;

public class Server
{
    public static Action<WebSocketContext>? OnNewClientJoined;
    public static Action<WebSocketContext>? OnClientExited;
    public static Action<string>? OnError { get; set; }
    public static Action<string>? OnWarn { get; set; }
    public static Action? OnServerStarted;

    public static int MaxBufferSize => 8000;

    internal static readonly ConcurrentDictionary<string, JaguarTask> Listeners = new();

    private static ImmutableList<long> _usersUniqueId = ImmutableList<long>.Empty;

    private readonly WebSocket.WebSocket webSocket;

    protected Server(string address)
    {
        if (string.IsNullOrEmpty(address) || string.IsNullOrWhiteSpace(address))
            throw new ArgumentNullException(nameof(address));

        var uri = address;
        webSocket = new WebSocket.WebSocket(address, MaxBufferSize);
    }

    // 0: none,
    // 1: join,
    // 2: already used,
    // 3: listenersSetting
    public static Dictionary<string, byte> GetListenerNameMap()
    {
        var sequenceNumber = 4;
        return Listeners.ToDictionary(i => i.Key,
            _ => (byte)sequenceNumber++);
    }


    public static void AddListeners(Assembly assembly)
    {
        #region UnRegistered user listener

        var unRegisteredUserListeners = from x in assembly.GetTypes()
            let y = x.BaseType
            where !x.IsAbstract && !x.IsInterface &&
                  y is { IsGenericType: true } &&
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
            Debug.Assert(instance != null, nameof(instance) + " != null");
            var propertyInfo = instance.GetType().GetProperty("Name");

            // Get the value of the property from the instance
            Debug.Assert(propertyInfo != null, nameof(propertyInfo) + " != null");
            var name = (string)propertyInfo.GetValue(instance)!;

            // Check listener name
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Listener name is null or empty. Listener: {listener.FullName}");
            }

            var jaguarTask = new JaguarTask
            {
                RequestType = genericArgument,
                Method = methodInfo,
                Object = instance,
                // ListenersManager = (ListenersManager) Activator.CreateInstance(listener)!,
                SenderType = typeof(WebSocketContextData)
            };

            if (!AddListener(name, jaguarTask))
            {
                throw new InvalidDataException($"Can't add new listener{name}");
            }
        }

        #endregion

        #region Registered user listener

        var registeredUserListeners = from x in assembly.GetTypes()
            let y = x.BaseType
            where !x.IsAbstract && !x.IsInterface &&
                  y is { IsGenericType: true } &&
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
            Debug.Assert(instance != null, nameof(instance) + " != null");
            var propertyInfo = instance.GetType().GetProperty("Name");

            // Get the value of the property from the instance
            Debug.Assert(propertyInfo != null, nameof(propertyInfo) + " != null");
            var name = (string)propertyInfo.GetValue(instance)!;

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
                    Object = instance,
                    SenderType = genericArgument0
                };
                if (!AddListener(name, jaguarTask))
                {
                    throw new InvalidDataException($"Can't add new listener{name}");
                }
            }
            else
            {
                var jaguarTask = new JaguarTask
                {
                    RequestType = genericArgument1,
                    Method = methodInfo,
                    Object = instance,
                    SenderType = genericArgument0
                };
                if (!AddListener(name, jaguarTask))
                {
                    throw new InvalidDataException($"Can't add new listener{name}");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Start the server
    /// </summary>
    public async void Start()
    {
        webSocket.Start();
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
    private static bool AddListener(string eventName, JaguarTask jaguarTask)
    {
        return Listeners.TryAdd(eventName, jaguarTask);
    }

    public static void Send(User user, string eventName, object message)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        if (eventName == null) throw new ArgumentNullException(nameof(eventName));
        if (message == null) throw new ArgumentNullException(nameof(message));

        if (user.Client != null)
        {
            var packet = new Packet(user.Client, eventName, message);
            Core.WebSocket.WebSocket.Send(user.Client.SocketContext, packet);
        }
    }

    public static void Send(WebSocketContextData client, string eventName, object message)
    {
        var packet = new Packet(client, eventName, message);
        Core.WebSocket.WebSocket.Send(client.SocketContext, packet);
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
}