# Jaguar Quick Start Guide

This guide will walk you through creating your first multiplayer game server using Jaguar.

## Step 1: Create Your User Class

Every player in your game needs a User class. This class represents a connected player and their state.

```csharp
using Jaguar.Core.Entity;
using Jaguar.Core.WebSocket;

public class MyUser : User
{
    public string Username { get; set; }
    public int Level { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    // Constructor for real players with WebSocket connection
    public MyUser(WebSocketContextData client, string username) : base(client)
    {
        Username = username;
        Level = 1;
        Wins = 0;
        Losses = 0;
    }

    // Constructor for bot players (no WebSocket connection)
    public MyUser() : base()
    {
        Username = "Bot_" + UniqueId;
        Level = 1;
    }

    public override void Dispose()
    {
        // Clean up resources when user disconnects
        Console.WriteLine($"User {Username} (ID: {UniqueId}) disposed");
    }
}
```

**Key Points:**
- Inherit from `User` abstract class
- Provide two constructors: one with `WebSocketContextData` for real players, one without for bots
- Implement the `Dispose()` method for cleanup
- Add any custom properties you need for your game

---

## Step 2: Create Your Room Class

Rooms represent game sessions where players interact. A room can have multiple rounds.

```csharp
using Jaguar.Core.Entity;
using Jaguar.Enums;

public class MyGameRoom : Room
{
    public MyGameRoom() : base(roundCount: 3, access: Access.Public)
    {
        // Minimum 2 players, maximum 4 players
        UsersCount = new Core.Utils.Range(2, 4);
        
        // TypeId is used for matchmaking (0 = default)
        TypeId = 1;
        
        // Level range for matchmaking (optional)
        Level = new Core.Utils.Range(1, 100);
    }

    public override async Task NewUserJoinedAsync(User user)
    {
        var myUser = (MyUser)user;
        Console.WriteLine($"{myUser.Username} joined the room!");
        
        // Notify all existing users about the new player
        foreach (var existingUser in Users.Where(u => u?.UniqueId != user.UniqueId))
        {
            existingUser?.Send("PlayerJoined", new 
            { 
                username = myUser.Username,
                userId = myUser.UniqueId
            });
        }
        
        // Send current room state to the new user
        myUser.Send("RoomState", new 
        { 
            roomId = UniqueId,
            playerCount = Users.Count,
            maxPlayers = UsersCount.End,
            players = Users.Select(u => new 
            { 
                username = ((MyUser)u).Username, 
                userId = u.UniqueId 
            }).ToList()
        });
    }

    public override async Task RoomReadyForStartAsync(IEnumerable<User> users)
    {
        Console.WriteLine($"Room {UniqueId} is ready! {Users.Count} players joined.");
        
        // Notify all players that the room is ready
        foreach (var user in users)
        {
            user?.Send("RoomReady", new 
            { 
                message = "Game will start in 3 seconds...",
                playerCount = Users.Count
            });
        }
        
        // Wait 3 seconds before starting the first round
        await Task.Delay(3000);
        await StartRoundAsync();
    }

    public override async Task RoundStartedAsync(ushort roundId)
    {
        Console.WriteLine($"Round {roundId + 1} started in room {UniqueId}!");
        
        // Notify all players
        foreach (var user in Users)
        {
            user?.Send("RoundStarted", new 
            { 
                roundNumber = roundId + 1,
                totalRounds = RoundsCount,
                message = $"Round {roundId + 1} has begun!"
            });
        }
        
        // Example: Auto-complete round after 30 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(30000);
            
            if (CurrentRound?.Index == roundId && !CurrentRound.Completed)
            {
                CurrentRound.RoundComplete();
                await StartRoundAsync(); // Start next round or end game
            }
        });
    }

    public override async Task GameCompletedAsync()
    {
        Console.WriteLine($"Game completed in room {UniqueId}!");
        
        // Calculate final scores
        var leaderboard = Users.Select(u =>
        {
            u.TryGetTotalScore(out var score, out var rounds);
            var myUser = (MyUser)u;
            return new 
            { 
                username = myUser.Username,
                userId = myUser.UniqueId,
                score = score,
                roundsPlayed = rounds
            };
        }).OrderByDescending(u => u.score).ToList();
        
        // Update user stats
        if (leaderboard.Any())
        {
            var winner = Users.First(u => u.UniqueId == leaderboard[0].userId) as MyUser;
            if (winner != null) winner.Wins++;
            
            foreach (var loser in Users.Where(u => u.UniqueId != leaderboard[0].userId).Cast<MyUser>())
            {
                loser.Losses++;
            }
        }
        
        // Send results to all players
        foreach (var user in Users)
        {
            user?.Send("GameEnded", new 
            { 
                leaderboard = leaderboard,
                message = $"Winner: {leaderboard[0].username}!"
            });
        }
    }

    public override async Task UserExitedAsync(User user)
    {
        var myUser = (MyUser)user;
        Console.WriteLine($"{myUser.Username} left room {UniqueId}");
        
        // Notify remaining players
        foreach (var remainingUser in Users.Where(u => u?.UniqueId != user.UniqueId))
        {
            remainingUser?.Send("PlayerLeft", new 
            { 
                username = myUser.Username,
                userId = myUser.UniqueId
            });
        }
        
        // If game started and not enough players, end the game
        if (GameStarted && Users.Count < UsersCount.Start)
        {
            await ForceGameToEndAsync();
        }
    }

    public override async Task UserKickedAsync(User user)
    {
        var myUser = (MyUser)user;
        Console.WriteLine($"{myUser.Username} was kicked from room {UniqueId}");
        
        myUser.Send("Kicked", new { message = "You have been kicked from the room" });
    }
}
```

**Key Points:**
- Constructor specifies round count and access level (Public/Private)
- Override lifecycle methods to implement game logic
- Use `UsersCount` to set min/max players
- Use `TypeId` and `Level` for matchmaking
- Call `StartRoundAsync()` to begin rounds
- Call `CurrentRound.RoundComplete()` to finish a round

---

## Step 3: Create Message Listeners

Listeners handle incoming messages from clients.

### A. Unregistered User Listener (Before Login)

```csharp
using Jaguar.Listeners;
using Jaguar.Core.WebSocket;

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}

public class LoginResponse
{
    public bool Success { get; set; }
    public long? UserId { get; set; }
    public string Message { get; set; }
}

public class LoginListener : UnRegisteredUserListener<LoginRequest>
{
    public override void Config()
    {
        Name = "Login"; // Event name that clients will use
    }

    public override async Task OnMessageReceived(WebSocketContextData sender, LoginRequest request)
    {
        Console.WriteLine($"Login attempt: {request.Username}");
        
        // Validate credentials (this is a simplified example)
        // In production, check against database
        if (string.IsNullOrEmpty(request.Username) || request.Username.Length < 3)
        {
            Jaguar.Core.Server.Send(sender, "LoginResponse", new LoginResponse
            {
                Success = false,
                Message = "Username must be at least 3 characters"
            });
            return;
        }
        
        if (request.Password == "correctpassword") // Replace with real authentication
        {
            // Create user instance
            var user = new MyUser(sender, request.Username);
            
            Console.WriteLine($"User {user.Username} logged in with ID {user.UniqueId}");
            
            // Send success response
            Jaguar.Core.Server.Send(sender, "LoginResponse", new LoginResponse
            {
                Success = true,
                UserId = user.UniqueId,
                Message = $"Welcome, {user.Username}!"
            });
        }
        else
        {
            Jaguar.Core.Server.Send(sender, "LoginResponse", new LoginResponse
            {
                Success = false,
                Message = "Invalid credentials"
            });
        }
    }
}
```

### B. Registered User Listener (After Login)

```csharp
public class JoinRoomRequest
{
    public uint? TypeId { get; set; }
    public long? RoomId { get; set; }
    public string Password { get; set; }
}

public class JoinRoomListener : RegisteredUserListener<MyUser, JoinRoomRequest>
{
    public override void Config()
    {
        Name = "JoinRoom";
    }

    public override async Task OnMessageReceived(MyUser sender, JoinRoomRequest request)
    {
        Console.WriteLine($"{sender.Username} wants to join a room");
        
        var options = new Jaguar.Core.Utils.JoinOptions();
        
        // Join specific room by ID
        if (request.RoomId.HasValue)
        {
            options = options.UseRoomId(request.RoomId.Value);
            
            if (!string.IsNullOrEmpty(request.Password))
            {
                options = options.UsePassword(request.Password);
            }
        }
        // Join by type
        else if (request.TypeId.HasValue)
        {
            options = options.UseTypeId(request.TypeId.Value);
        }

        bool joined = await Jaguar.Manager.RoomManager.JoinAsync<MyGameRoom>(sender, options);
        
        if (joined)
        {
            sender.Send("JoinRoomResponse", new 
            { 
                success = true,
                roomId = sender.CurrentRoom?.UniqueId,
                message = "Successfully joined room!"
            });
        }
        else
        {
            sender.Send("JoinRoomResponse", new 
            { 
                success = false,
                message = "Could not join room. Room might be full or not found."
            });
        }
    }
}
```

### C. Game Action Listener

```csharp
public class GameActionRequest
{
    public string Action { get; set; } // e.g., "move", "attack", "collect"
    public int X { get; set; }
    public int Y { get; set; }
    public string Data { get; set; }
}

public class GameActionListener : RegisteredUserListener<MyUser, GameActionRequest>
{
    public override void Config()
    {
        Name = "GameAction";
    }

    public override async Task OnMessageReceived(MyUser sender, GameActionRequest request)
    {
        if (sender.CurrentRoom == null)
        {
            sender.Send("Error", new { message = "You are not in a room" });
            return;
        }

        if (!sender.CurrentRoom.GameStarted)
        {
            sender.Send("Error", new { message = "Game has not started yet" });
            return;
        }

        Console.WriteLine($"{sender.Username} performed action: {request.Action} at ({request.X}, {request.Y})");
        
        // Example: Award points for certain actions
        if (request.Action == "collect")
        {
            sender.AddScore(10); // Add 10 points
        }
        
        // Broadcast action to all players in the room
        foreach (var user in sender.CurrentRoom.Users.Where(u => u?.UniqueId != sender.UniqueId))
        {
            user?.Send("PlayerAction", new 
            { 
                username = sender.Username,
                userId = sender.UniqueId,
                action = request.Action,
                x = request.X,
                y = request.Y,
                data = request.Data
            });
        }
        
        // Confirm action to sender
        sender.Send("ActionConfirmed", new 
        { 
            action = request.Action,
            success = true
        });
    }
}
```

### D. Listener with Response Callback

```csharp
public class GetRoomInfoRequest
{
    public long RoomId { get; set; }
}

public class RoomInfoResponse
{
    public long RoomId { get; set; }
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public bool GameStarted { get; set; }
    public List<PlayerInfo> Players { get; set; }
}

public class PlayerInfo
{
    public long UserId { get; set; }
    public string Username { get; set; }
    public int Level { get; set; }
}

public class GetRoomInfoListener : RegisteredUserListener<MyUser, GetRoomInfoRequest, RoomInfoResponse>
{
    public override void Config()
    {
        Name = "GetRoomInfo";
    }

    public override async Task<RoomInfoResponse> OnMessageReceived(MyUser sender, GetRoomInfoRequest request)
    {
        var room = Jaguar.Manager.RoomManager.FindRoom(request.RoomId);
        
        if (room == null)
        {
            return null;
        }
        
        return new RoomInfoResponse
        {
            RoomId = room.UniqueId,
            PlayerCount = room.Users.Count,
            MaxPlayers = (int)room.UsersCount.End,
            GameStarted = room.GameStarted,
            Players = room.Users.Select(u => new PlayerInfo
            {
                UserId = u.UniqueId,
                Username = ((MyUser)u).Username,
                Level = ((MyUser)u).Level
            }).ToList()
        };
    }
}
```

---

## Step 4: Create and Start Your Server

```csharp
using Jaguar.Core;
using System.Reflection;

public class MyGameServer : Server
{
    public MyGameServer(string address) : base(address)
    {
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("Initializing Jaguar Game Server...");
        
        // Configure server event handlers
        Server.OnServerStarted = () => 
        {
            Console.WriteLine("╔════════════════════════════════════╗");
            Console.WriteLine("║  Jaguar Server Started!            ║");
            Console.WriteLine("║  Listening on: http://localhost:8080/");
            Console.WriteLine("╚════════════════════════════════════╝");
        };

        Server.OnNewClientJoined = (context) => 
        {
            var endpoint = context.Request.RemoteEndPoint;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] New client connected from {endpoint}");
        };

        Server.OnClientExited = (context) => 
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Client disconnected");
        };

        Server.OnError = (message) => 
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        };

        Server.OnWarn = (message) => 
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] {message}");
            Console.ResetColor();
        };

        // Register all listeners from current assembly
        Console.WriteLine("Registering listeners...");
        Server.AddListeners(Assembly.GetExecutingAssembly());
        
        var listenerCount = Server.GetListenerNameMap().Count;
        Console.WriteLine($"Registered {listenerCount} listeners");

        // Create and start server
        var server = new MyGameServer("http://localhost:8080/");
        server.Start();

        Console.WriteLine("\nPress Ctrl+C to stop the server...");
        
        // Keep server running
        while (true)
        {
            Thread.Sleep(1000);
        }
    }
}
```

**Important Notes:**
- The server runs synchronously, so put it in a separate thread if you need async main
- WebSocket URL must start with `http://` (not `ws://`) and end with `/`
- Default port 8080, but you can use any available port
- `AddListeners()` automatically discovers all listener classes in your assembly

---

## Step 5: Client-Side Connection

### Unity C# Client Example

```csharp
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

public class JaguarClient
{
    private ClientWebSocket webSocket;
    private byte[] clientId;
    private Dictionary<byte, string> listenerMap;
    
    public async Task Connect(string serverUrl)
    {
        webSocket = new ClientWebSocket();
        await webSocket.ConnectAsync(new Uri(serverUrl), CancellationToken.None);
        
        // Generate unique 7-byte client ID
        clientId = new byte[7];
        new Random().NextBytes(clientId);
        
        // Send join message (EventId = 1)
        await SendRawMessage(1, new byte[0]);
        
        // Start receiving messages
        _ = ReceiveMessages();
    }
    
    private async Task SendRawMessage(byte eventId, byte[] data)
    {
        var message = new byte[7 + 1 + data.Length + 1];
        Array.Copy(clientId, 0, message, 0, 7);
        message[7] = eventId;
        Array.Copy(data, 0, message, 8, data.Length);
        message[message.Length - 1] = 200; // EOF marker
        
        await webSocket.SendAsync(
            new ArraySegment<byte>(message), 
            WebSocketMessageType.Binary, 
            true, 
            CancellationToken.None
        );
    }
    
    public async Task Send(string eventName, object data)
    {
        if (listenerMap == null || !listenerMap.ContainsValue(eventName))
        {
            Console.WriteLine($"Event {eventName} not found in listener map");
            return;
        }
        
        var eventId = listenerMap.First(kv => kv.Value == eventName).Key;
        var json = JsonConvert.SerializeObject(data);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        
        await SendRawMessage(eventId, jsonBytes);
    }
    
    private async Task ReceiveMessages()
    {
        var buffer = new byte[8000];
        
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), 
                CancellationToken.None
            );
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "", 
                    CancellationToken.None
                );
                break;
            }
            
            HandleMessage(buffer, result.Count);
        }
    }
    
    private void HandleMessage(byte[] data, int length)
    {
        var eventId = data[0];
        var messageBytes = new byte[length - 2]; // Remove EventId and EOF
        Array.Copy(data, 1, messageBytes, 0, length - 2);
        var json = Encoding.UTF8.GetString(messageBytes);
        
        // EventId 3 = Listener name map
        if (eventId == 3)
        {
            listenerMap = JsonConvert.DeserializeObject<Dictionary<string, byte>>(json)
                .ToDictionary(kv => kv.Value, kv => kv.Key);
            Console.WriteLine($"Received listener map with {listenerMap.Count} events");
            return;
        }
        
        // Find event name
        if (listenerMap != null && listenerMap.TryGetValue(eventId, out var eventName))
        {
            OnMessageReceived?.Invoke(eventName, json);
        }
    }
    
    public event Action<string, string> OnMessageReceived;
}

// Usage example
public class GameClient : MonoBehaviour
{
    private JaguarClient client;
    
    async void Start()
    {
        client = new JaguarClient();
        client.OnMessageReceived += HandleServerMessage;
        
        await client.Connect("ws://localhost:8080/");
        
        // Wait for listener map, then login
        await Task.Delay(1000);
        
        await client.Send("Login", new 
        { 
            Username = "Player1", 
            Password = "correctpassword" 
        });
    }
    
    private void HandleServerMessage(string eventName, string json)
    {
        Debug.Log($"Received {eventName}: {json}");
        
        switch (eventName)
        {
            case "LoginResponse":
                var response = JsonConvert.DeserializeObject<LoginResponse>(json);
                if (response.Success)
                {
                    Debug.Log($"Logged in with ID: {response.UserId}");
                }
                break;
                
            case "RoundStarted":
                Debug.Log("Round started!");
                break;
                
            case "PlayerAction":
                // Handle other player's action
                break;
        }
    }
}
```

---

## Next Steps

1. **Test Your Server**: Run the server and connect with a test client
2. **Implement Game Logic**: Add your specific game rules to the Room class
3. **Add More Listeners**: Create listeners for all player actions
4. **Database Integration**: Store user data, game statistics, etc.
5. **Security**: Add proper authentication, input validation, rate limiting
6. **Scaling**: Consider load balancing for production

## Common Patterns

### Creating a Room Manually
```csharp
var room = new MyGameRoom();
await room.AddUserAsync(user1);
await room.AddUserAsync(user2);
await room.StartRoundAsync();
```

### Finding Rooms
```csharp
var allRooms = RoomManager.GetRooms<MyGameRoom>();
var userRooms = RoomManager.FindRooms<MyGameRoom>(user);
var specificRoom = RoomManager.FindRoom(roomId);
```

### Broadcasting Messages
```csharp
// To all users in a room
foreach (var user in room.Users)
{
    user?.Send("Announcement", new { message = "Hello!" });
}

// To specific user
user.Send("PrivateMessage", new { text = "Secret!" });
```

For more advanced topics, see [ADVANCED_USAGE.md](ADVANCED_USAGE.md).
