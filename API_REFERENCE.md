# Jaguar API Reference

Complete API documentation for all public classes, methods, properties, and events in Jaguar.

---

## Table of Contents

- [Server Class](#server-class)
- [User Class](#user-class)
- [Room Class](#room-class)
- [Round Class](#round-class)
- [RoomManager Class](#roommanager-class)
- [UsersManager Class](#usersmanager-class)
- [Listener Classes](#listener-classes)
- [Utility Classes](#utility-classes)
- [Enums](#enums)

---

## Server Class

**Namespace:** `Jaguar.Core`

The main server class that manages WebSocket connections and message routing.

### Constructor

```csharp
protected Server(string address)
```

Creates a new server instance.

**Parameters:**
- `address` (string): WebSocket server URL (e.g., `http://localhost:8080/`)

**Throws:**
- `ArgumentNullException`: If address is null, empty, or whitespace

### Static Properties

#### OnNewClientJoined
```csharp
public static Action<WebSocketContext>? OnNewClientJoined { get; set; }
```
Event fired when a new WebSocket client connects.

#### OnClientExited
```csharp
public static Action<WebSocketContext>? OnClientExited { get; set; }
```
Event fired when a client disconnects.

#### OnError
```csharp
public static Action<string>? OnError { get; set; }
```
Event fired when an error occurs. Subscribe to log errors.

#### OnWarn
```csharp
public static Action<string>? OnWarn { get; set; }
```
Event fired for warnings. Subscribe to log warnings.

#### OnServerStarted
```csharp
public static Action? OnServerStarted { get; set; }
```
Event fired when the server successfully starts.

#### MaxBufferSize
```csharp
public static int MaxBufferSize { get; } // Returns 8000
```
Maximum message size in bytes. Messages larger than this will be rejected.

#### Encoding
```csharp
public static Encoding Encoding { get; set; } // Default: UTF8
```
Text encoding used for message serialization/deserialization.

### Static Methods

#### AddListeners
```csharp
public static void AddListeners(Assembly assembly)
```

Scans the assembly for listener classes and registers them automatically.

**Parameters:**
- `assembly` (Assembly): Assembly to scan (typically `Assembly.GetExecutingAssembly()`)

**Example:**
```csharp
Server.AddListeners(Assembly.GetExecutingAssembly());
```

#### GetListenerNameMap
```csharp
public static Dictionary<string, byte> GetListenerNameMap()
```

Returns a dictionary mapping listener names to their EventId.

**Returns:** Dictionary where key is listener name and value is EventId (starting from 4)

#### Send (User)
```csharp
public static void Send(User user, string eventName, object message)
```

Sends a message to a specific user.

**Parameters:**
- `user` (User): Target user
- `eventName` (string): Name of the event/listener
- `message` (object): Data to send (will be JSON serialized)

**Throws:**
- `ArgumentNullException`: If any parameter is null

#### Send (Client)
```csharp
public static void Send(WebSocketContextData client, string eventName, object message)
```

Sends a message to a client (used for unregistered users).

**Parameters:**
- `client` (WebSocketContextData): Target client
- `eventName` (string): Name of the event/listener
- `message` (object): Data to send

### Instance Methods

#### Start
```csharp
public async void Start()
```

Starts the WebSocket server. This method blocks execution.

**Example:**
```csharp
var server = new MyServer("http://localhost:8080/");
server.Start(); // Blocks here
```

---

## User Class

**Namespace:** `Jaguar.Core.Entity`

Abstract class representing a connected player. Must be inherited.

### Constants

```csharp
public const double RoomNotFoundCode = -1;
public const double RoundNotFoundCode = -2;
public const double RoomNotStartedCode = -3;
```

### Properties

#### UniqueId
```csharp
public long UniqueId { get; }
```
Unique identifier for the user (9-digit number, auto-generated).

#### IsOnline
```csharp
public bool IsOnline { get; internal set; }
```
Whether the user is currently connected via WebSocket.

#### Rooms
```csharp
public List<Room?> Rooms { get; }
```
All rooms the user has joined.

#### CurrentRoom
```csharp
public Room? CurrentRoom { get; private set; }
```
The room the user is currently active in.

### Constructors

#### User() - For Bots
```csharp
protected User()
```
Creates a user without a WebSocket connection (bot/AI).

#### User(WebSocketContextData) - For Real Players
```csharp
protected User(WebSocketContextData? client)
```
Creates a user with a WebSocket connection.

**Parameters:**
- `client` (WebSocketContextData): WebSocket client data

**Throws:**
- `NullReferenceException`: If client is null

### Methods

#### SetAsCurrentRoom
```csharp
public bool SetAsCurrentRoom(Room? room)
```

Sets the specified room as the user's current room.

**Parameters:**
- `room` (Room): Room to set as current

**Returns:** `true` if successful, `false` if room is null, not in user's rooms, or user not in room

#### SetCurrentRoomToNull
```csharp
public void SetCurrentRoomToNull()
```

Clears the current room reference.

#### AddScore
```csharp
public bool AddScore()
```

Adds 1 point to the user's score in the current round.

**Returns:** `true` if successful, `false` if not in a room or round

#### AddScore (with count)
```csharp
public bool AddScore(double count)
```

Adds specified points to the user's score in the current round.

**Parameters:**
- `count` (double): Points to add

**Returns:** `true` if successful, `false` if not in a room or round

#### AddScore (with round index)
```csharp
public bool AddScore(double count, int roundIndex)
```

Adds specified points to a specific round.

**Parameters:**
- `count` (double): Points to add
- `roundIndex` (int): Index of the round

**Returns:** `true` if successful, `false` if invalid round

#### TryGetTotalScore
```csharp
public bool TryGetTotalScore(out double result, out int roundsCount)
```

Gets the total score across all rounds.

**Parameters:**
- `result` (out double): Total score
- `roundsCount` (out int): Number of rounds played

**Returns:** `true` if successful, `false` if not in a started game

#### Score
```csharp
public double Score(int roundIndex)
```

Gets the score for a specific round.

**Parameters:**
- `roundIndex` (int): Index of the round

**Returns:** Score value, or error code if invalid

**Throws:**
- `IndexOutOfRangeException`: If round index out of range
- `Exception`: If round not started

#### UpdateClient
```csharp
public void UpdateClient(WebSocketContextData? client)
```

Updates the WebSocket client (used for reconnection).

**Parameters:**
- `client` (WebSocketContextData): New client data

#### Send
```csharp
public void Send(string eventName, object data)
```

Sends a message to this user.

**Parameters:**
- `eventName` (string): Event name
- `data` (object): Data to send

#### Dispose
```csharp
public abstract void Dispose()
```

Must be implemented to clean up resources when user disconnects.

---

## Room Class

**Namespace:** `Jaguar.Core.Entity`

Abstract class representing a game session. Must be inherited.

### Properties

#### UniqueId
```csharp
public long UniqueId { get; internal set; }
```
Unique identifier for the room (5-digit number, auto-generated).

#### TypeId
```csharp
public uint TypeId { get; set; } // Default: 0
```
Room type identifier for matchmaking.

#### Access
```csharp
public Access Access { get; init; }
```
Access level (Public or Private).

#### Password
```csharp
public string Password { get; init; }
```
Password for private rooms (empty for public rooms).

#### Level
```csharp
public Range Level { get; init; }
```
Level range for matchmaking.

#### UsersCount
```csharp
public Range UsersCount { get; set; }
```
Minimum and maximum player count.

#### Users
```csharp
public ImmutableList<User?> Users { get; protected set; }
```
Immutable list of users in the room.

#### GameStarted
```csharp
public bool GameStarted { get; set; }
```
Whether the game has started.

#### GameComplete
```csharp
public bool GameComplete { get; }
```
Whether all rounds are completed.

#### IsPlaying
```csharp
public bool IsPlaying { get; }
```
Whether a round is currently active.

#### AllUsersJoined
```csharp
public bool AllUsersJoined { get; }
```
Whether maximum players have joined.

#### CurrentRound
```csharp
public Round? CurrentRound { get; }
```
Currently active round (null if no round active).

#### RoundsCount
```csharp
public int RoundsCount { get; }
```
Total number of rounds configured for this room.

#### EnableJoinAfterGameStarted
```csharp
public bool EnableJoinAfterGameStarted { get; protected set; } // Default: false
```
Whether players can join after game starts.

#### CreationTime
```csharp
public DateTime CreationTime { get; private set; }
```
UTC time when room was created.

### Constructor

```csharp
protected Room(int roundCount, Access access)
```

**Parameters:**
- `roundCount` (int): Number of rounds in the game
- `access` (Access): Public or Private

**Default Values:**
- UsersCount: Range(2) - exactly 2 players
- Password: empty string

### Methods

#### AddUserAsync
```csharp
public virtual async Task<bool> AddUserAsync(User user, string pwd = "")
```

Adds a user to the room.

**Parameters:**
- `user` (User): User to add
- `pwd` (string): Password (if private room)

**Returns:** `true` if user added, `false` if failed

**Conditions for Success:**
- Game not started (unless `EnableJoinAfterGameStarted` is true)
- Room not full
- Password correct (if private)
- User not already in room

#### StartRoundAsync
```csharp
public async Task<bool> StartRoundAsync()
```

Starts the next round or ends the game if all rounds complete.

**Returns:** `true` if round started or game ended, `false` if not enough players

**Behavior:**
- If all rounds complete: calls `GameCompletedAsync()` and removes room
- Otherwise: creates new round and calls `RoundStartedAsync()`

#### RemoveUserAsync
```csharp
public async Task RemoveUserAsync(User? user)
```

Removes a user from the room gracefully.

**Parameters:**
- `user` (User): User to remove

**Note:** Calls `UserExitedAsync()` hook

#### KickUserAsync
```csharp
public async Task KickUserAsync(User? user)
```

Kicks a user from the room.

**Parameters:**
- `user` (User): User to kick

**Note:** Calls `UserKickedAsync()` hook

#### RemoveUser
```csharp
public void RemoveUser(User user)
```

Synchronously removes a user (no hooks called).

**Parameters:**
- `user` (User): User to remove

#### ReplaceUser
```csharp
public void ReplaceUser(User oldUser, User newUser)
```

Replaces one user with another.

**Parameters:**
- `oldUser` (User): User to replace
- `newUser` (User): Replacement user

#### ShuffleUsers
```csharp
public void ShuffleUsers()
```

Randomly shuffles the user list.

#### Destroy
```csharp
public void Destroy()
```

Destroys the room and removes all users.

#### ForceGameToEndAsync
```csharp
public async Task<bool> ForceGameToEndAsync()
```

Forcibly ends the game.

**Returns:** `true` if game was ended, `false` if game not started

### Virtual Event Hooks

Override these methods to implement custom logic:

#### NewUserJoinedAsync
```csharp
public virtual Task NewUserJoinedAsync(User user)
```
Called when a user joins the room.

#### RoomReadyForStartAsync
```csharp
public virtual Task RoomReadyForStartAsync(IEnumerable<User> users)
```
Called when minimum required users have joined.

#### RoundStartedAsync
```csharp
public virtual Task RoundStartedAsync(ushort roundId)
```
Called when a new round starts. RoundId is 0-indexed.

#### GameCompletedAsync
```csharp
public virtual Task GameCompletedAsync()
```
Called when all rounds complete.

#### UserExitedAsync
```csharp
public virtual Task UserExitedAsync(User user)
```
Called when a user leaves the room.

#### UserKickedAsync
```csharp
public virtual Task UserKickedAsync(User user)
```
Called when a user is kicked.

#### OnUserRejoinedAsync
```csharp
public virtual Task OnUserRejoinedAsync(User user)
```
Called when a user reconnects to the room.

---

## Round Class

**Namespace:** `Jaguar.Core`

Represents a single round within a room.

### Properties

#### Index
```csharp
public ushort Index { get; private set; }
```
Zero-based index of the round.

#### Completed
```csharp
internal bool Completed { get; private set; }
```
Whether the round is completed (read-only from outside).

#### CreationTime
```csharp
public DateTime CreationTime { get; private set; }
```
UTC time when round was created.

### Methods

#### RoundComplete
```csharp
public void RoundComplete()
```
Marks the round as completed.

#### ResetScore
```csharp
public void ResetScore(User? user)
```
Resets a user's score in this round to 0.

**Parameters:**
- `user` (User): User whose score to reset

---

## RoomManager Class

**Namespace:** `Jaguar.Manager`

Static manager for all rooms.

### Methods

#### GetRooms<T>
```csharp
public static T[] GetRooms<T>() where T : Room
```

Gets all rooms of a specific type.

**Returns:** Array of rooms

**Example:**
```csharp
var allRooms = RoomManager.GetRooms<MyGameRoom>();
```

#### FindRoom
```csharp
public static Room? FindRoom(long id)
```

Finds a room by its unique ID.

**Parameters:**
- `id` (long): Room unique ID

**Returns:** Room or null if not found

#### FindRooms<T>
```csharp
public static T[] FindRooms<T>(User user) where T : Room
```

Finds all rooms containing a specific user.

**Parameters:**
- `user` (User): User to search for

**Returns:** Array of rooms

#### FindRoomsWithUser<T>
```csharp
public static T[] FindRoomsWithUser<T>(long userId) where T : Room
```

Finds all rooms containing a user by ID.

**Parameters:**
- `userId` (long): User unique ID

**Returns:** Array of rooms

#### JoinAsync<T> (Simple)
```csharp
public static async Task<bool> JoinAsync<T>(User user) where T : Room
```

Joins a random available room.

**Parameters:**
- `user` (User): User to join

**Returns:** `true` if joined, `false` if no available room

**Example:**
```csharp
bool joined = await RoomManager.JoinAsync<MyGameRoom>(user);
```

#### JoinAsync<T> (With Options)
```csharp
public static async Task<bool> JoinAsync<T>(User user, JoinOptions options) where T : Room
```

Joins a room with specific criteria.

**Parameters:**
- `user` (User): User to join
- `options` (JoinOptions): Join criteria

**Returns:** `true` if joined, `false` if failed

**Example:**
```csharp
var options = new JoinOptions()
    .UseTypeId(1)
    .UseLevel(5);
bool joined = await RoomManager.JoinAsync<MyGameRoom>(user, options);
```

#### ReJoin
```csharp
public static bool ReJoin(long uniqueId, WebSocketContextData client, out User? user)
```

Reconnects a disconnected user to their room.

**Parameters:**
- `uniqueId` (long): User's unique ID
- `client` (WebSocketContextData): New client connection
- `user` (out User): Output user object

**Returns:** `true` if rejoined, `false` if user's room not found

#### IsUserInActiveGame
```csharp
public static bool IsUserInActiveGame(long userId)
```

Checks if a user is in an active game.

**Parameters:**
- `userId` (long): User unique ID

**Returns:** `true` if user is in a started, non-completed game

---

## UsersManager Class

**Namespace:** `Jaguar.Manager`

Static manager for all users.

### Methods

#### GetAll
```csharp
public static List<User?> GetAll()
```

Gets all connected users.

**Returns:** List of all users

#### FindUser (by client)
```csharp
public static User? FindUser(BigInteger? client)
```

Finds a user by their client ID.

**Parameters:**
- `client` (BigInteger): Client ID

**Returns:** User or null

#### FindUser (by ID)
```csharp
public static User? FindUser(long id)
```

Finds a user by their unique ID.

**Parameters:**
- `id` (long): User unique ID

**Returns:** User or null

#### FindUser<T> (by predicate)
```csharp
public static T? FindUser<T>(Func<T, bool> predicate) where T : class
```

Finds a user matching a predicate.

**Parameters:**
- `predicate` (Func): Search predicate

**Returns:** First matching user or null

**Example:**
```csharp
var user = UsersManager.FindUser<MyUser>(u => u.Username == "Player1");
```

#### GetUsersCount
```csharp
public static int GetUsersCount()
```

Gets the total number of connected users.

**Returns:** User count

---

## Listener Classes

**Namespace:** `Jaguar.Listeners`

### UnRegisteredUserListener<TRequest>

For handling messages from clients before they authenticate.

```csharp
public abstract class UnRegisteredUserListener<TRequest>
{
    public string Name { get; set; }
    public abstract void Config();
    public abstract Task OnMessageReceived(WebSocketContextData sender, TRequest request);
}
```

**Usage:**
```csharp
public class LoginListener : UnRegisteredUserListener<LoginRequest>
{
    public override void Config()
    {
        Name = "Login";
    }

    public override async Task OnMessageReceived(WebSocketContextData sender, LoginRequest request)
    {
        // Handle login
    }
}
```

### RegisteredUserListener<TUser, TRequest>

For handling messages from authenticated users.

```csharp
public abstract class RegisteredUserListener<TUser, TRequest> where TUser : User
{
    public string Name { get; set; }
    public abstract void Config();
    public abstract Task OnMessageReceived(TUser sender, TRequest request);
}
```

**Usage:**
```csharp
public class JoinRoomListener : RegisteredUserListener<MyUser, JoinRoomRequest>
{
    public override void Config()
    {
        Name = "JoinRoom";
    }

    public override async Task OnMessageReceived(MyUser sender, JoinRoomRequest request)
    {
        // Handle room join
    }
}
```

### RegisteredUserListener<TUser, TRequest, TResponse>

For request-response pattern (with callback).

```csharp
public abstract class RegisteredUserListener<TUser, TRequest, TResponse> where TUser : User
{
    public string Name { get; set; }
    public abstract void Config();
    public abstract Task<TResponse> OnMessageReceived(TUser sender, TRequest request);
}
```

**Usage:**
```csharp
public class GetStatsListener : RegisteredUserListener<MyUser, GetStatsRequest, StatsResponse>
{
    public override void Config()
    {
        Name = "GetStats";
    }

    public override async Task<StatsResponse> OnMessageReceived(MyUser sender, GetStatsRequest request)
    {
        return new StatsResponse { /* ... */ };
    }
}
```

---

## Utility Classes

### JoinOptions

**Namespace:** `Jaguar.Core.Utils`

Fluent builder for room join criteria.

```csharp
public sealed record JoinOptions
```

#### Methods

```csharp
public JoinOptions UseLevel(uint level)
public JoinOptions UseRoomId(long id)
public JoinOptions UsePassword(string password)
public JoinOptions UseTypeId(uint id)
```

**Constraints:**
- Cannot use `RoomId` with `Level` or `TypeId`
- `Password` requires `RoomId`

**Example:**
```csharp
var options = new JoinOptions()
    .UseTypeId(1)
    .UseLevel(10);
```

### Range

**Namespace:** `Jaguar.Core.Utils`

Represents a numeric range.

```csharp
public struct Range
{
    public uint Start { get; set; }
    public uint End { get; set; }
}
```

#### Constructors

```csharp
public Range(uint start, uint end)  // Different start/end
public Range(uint fix)               // Same start and end
```

#### Methods

```csharp
public readonly bool InRange(uint i)
```

Checks if a value is within the range (inclusive).

**Example:**
```csharp
var range = new Range(1, 10);
bool isIn = range.InRange(5); // true
```

---

## Enums

### Access

**Namespace:** `Jaguar.Enums`

Room access level.

```csharp
public enum Access
{
    None = 0,
    Private = 1,
    Public = 1  // Note: Same value as Private (seems like a bug)
}
```

**Note:** There appears to be an issue where Private and Public have the same value. This may need fixing in the source code.

---

## Data Transfer Objects

### Packet

**Namespace:** `Jaguar.Core.Dto`

Internal message format.

```csharp
public record Packet
{
    public byte EventId { get; set; }
    public string? Message { get; }
    public BigInteger? Sender { get; init; }
}
```

**Protocol:**
- First 7 bytes: Sender ID
- Byte 8: EventId
- Bytes 9-N: JSON message
- Last byte: 200 (EOF marker)

---

## Extension Methods

**Namespace:** `Jaguar.Helpers`

### Extensions Class

```csharp
public static class Extensions
{
    public static IEnumerable<byte> ToBytes(this string value)
    public static string ToJson(this object obj)
    public static T? FromJson<T>(this string json)
}
```

**Usage:**
```csharp
var json = myObject.ToJson();
var bytes = json.ToBytes();
var restored = json.FromJson<MyObject>();
```

---

This completes the API reference. For usage examples, see [QUICK_START.md](QUICK_START.md).
