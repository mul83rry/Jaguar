# Jaguar Advanced Usage Guide

Advanced patterns and best practices for production-ready multiplayer games with Jaguar.

---

## Authentication & Security

### JWT-Based Authentication

```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

public class AuthListener : UnRegisteredUserListener<AuthRequest>
{
    public override void Config() => Name = "Authenticate";

    public override async Task OnMessageReceived(WebSocketContextData sender, AuthRequest request)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET"));
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false
            };

            var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out _);
            var userId = principal.FindFirst("userId")?.Value;
            var username = principal.FindFirst("username")?.Value;

            var user = new MyUser(sender, username) { UserId = long.Parse(userId) };
            Jaguar.Core.Server.Send(sender, "AuthSuccess", new { userId = user.UniqueId });
        }
        catch (Exception ex)
        {
            Jaguar.Core.Server.Send(sender, "AuthFailed", new { message = "Invalid token" });
        }
    }
}
```

### Rate Limiting

```csharp
public class RateLimiter
{
    private static readonly ConcurrentDictionary<long, Queue<DateTime>> UserRequests = new();
    private const int MaxRequestsPerMinute = 60;

    public static bool IsRateLimited(long userId)
    {
        var now = DateTime.UtcNow;
        var requests = UserRequests.GetOrAdd(userId, _ => new Queue<DateTime>());

        lock (requests)
        {
            while (requests.Count > 0 && (now - requests.Peek()).TotalMinutes > 1)
                requests.Dequeue();

            if (requests.Count >= MaxRequestsPerMinute)
                return true;

            requests.Enqueue(now);
            return false;
        }
    }
}
```

---

## Database Integration

### Entity Framework Setup

```csharp
public class GameDbContext : DbContext
{
    public DbSet<UserData> Users { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(Environment.GetEnvironmentVariable("DB_CONNECTION"));
    }
}

public class PersistentRoom : Room
{
    private GameDbContext _db;
    private Guid _sessionId;

    public override async Task RoomReadyForStartAsync(IEnumerable<User> users)
    {
        _db = new GameDbContext();
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            RoomId = UniqueId,
            StartedAt = DateTime.UtcNow
        };
        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync();
        _sessionId = session.Id;
        
        await StartRoundAsync();
    }

    public override async Task GameCompletedAsync()
    {
        var session = await _db.GameSessions.FindAsync(_sessionId);
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.Dispose();
    }
}
```

---

## Custom Room Patterns

### Timed Rounds

```csharp
public class TimedRoom : Room
{
    private const int RoundDurationSeconds = 60;
    private CancellationTokenSource _cts;

    public override async Task RoundStartedAsync(ushort roundId)
    {
        _cts = new CancellationTokenSource();
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(RoundDurationSeconds * 1000, _cts.Token);
            if (!_cts.Token.IsCancellationRequested)
            {
                CurrentRound?.RoundComplete();
                await StartRoundAsync();
            }
        });
    }

    public override async Task GameCompletedAsync()
    {
        _cts?.Cancel();
        await base.GameCompletedAsync();
    }
}
```

### Lobby System

```csharp
public class LobbyRoom : Room
{
    private bool _lobbyPhase = true;

    public override async Task NewUserJoinedAsync(User user)
    {
        if (_lobbyPhase)
        {
            user.Send("LobbyState", new { players = Users.Select(u => ((MyUser)u).Username) });
        }
    }

    public async Task SetPlayerReady(MyUser user, bool ready)
    {
        user.IsReady = ready;
        if (Users.All(u => ((MyUser)u).IsReady))
        {
            _lobbyPhase = false;
            await StartRoundAsync();
        }
    }
}
```

---

## Advanced Matchmaking

### ELO-Based Ranking

```csharp
public class RankedRoom : Room
{
    public int MinElo { get; set; }
    public int MaxElo { get; set; }

    public override async Task<bool> AddUserAsync(User user, string pwd = "")
    {
        var myUser = (MyUser)user;
        if (myUser.Elo < MinElo || myUser.Elo > MaxElo)
            return false;
        return await base.AddUserAsync(user, pwd);
    }

    public override async Task GameCompletedAsync()
    {
        // Calculate ELO changes
        var sorted = Users.OrderByDescending(u => {
            u.TryGetTotalScore(out var score, out _);
            return score;
        }).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var user = (MyUser)sorted[i];
            int eloChange = CalculateEloChange(user.Elo, ((MyUser)sorted[(i+1) % sorted.Count]).Elo, i == 0);
            user.Elo += eloChange;
        }
    }

    private int CalculateEloChange(int playerElo, int opponentElo, bool won)
    {
        double expected = 1.0 / (1.0 + Math.Pow(10, (opponentElo - playerElo) / 400.0));
        return (int)(32 * ((won ? 1.0 : 0.0) - expected));
    }
}
```

---

## State Synchronization

### Real-time State Broadcasting

```csharp
public class RealtimeRoom : Room
{
    private Timer _syncTimer;
    private Dictionary<long, PlayerState> _states = new();

    public override async Task RoundStartedAsync(ushort roundId)
    {
        // 20 updates per second
        _syncTimer = new Timer(_ => BroadcastState(), null, 0, 50);
    }

    public void UpdatePlayerState(long userId, PlayerState state)
    {
        _states[userId] = state;
    }

    private void BroadcastState()
    {
        var snapshot = new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), states = _states };
        foreach (var user in Users)
            user?.Send("StateSnapshot", snapshot);
    }
}

public class PlayerState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
}
```

---

## Reconnection Handling

```csharp
public class RejoinListener : UnRegisteredUserListener<RejoinRequest>
{
    private static Dictionary<long, string> SessionTokens = new();

    public override void Config() => Name = "Rejoin";

    public override async Task OnMessageReceived(WebSocketContextData sender, RejoinRequest request)
    {
        if (!SessionTokens.TryGetValue(request.UserId, out var token) || token != request.SessionToken)
        {
            Server.Send(sender, "RejoinFailed", new { message = "Invalid session" });
            return;
        }

        bool success = RoomManager.ReJoin(request.UserId, sender, out var user);
        if (success && user != null)
        {
            ((MyUser)user).IsOnline = true;
            Server.Send(sender, "RejoinSuccess", BuildGameState((MyUser)user));
        }
    }

    private object BuildGameState(MyUser user)
    {
        user.TryGetTotalScore(out var score, out _);
        return new
        {
            roomId = user.CurrentRoom?.UniqueId,
            currentRound = user.CurrentRoom?.CurrentRound?.Index,
            yourScore = score,
            players = user.CurrentRoom?.Users.Select(u => ((MyUser)u).Username)
        };
    }

    public static string GenerateSessionToken(long userId)
    {
        var token = Guid.NewGuid().ToString();
        SessionTokens[userId] = token;
        return token;
    }
}
```

---

## Performance Optimization

### Object Pooling

```csharp
public class MessagePool
{
    private static readonly ConcurrentBag<Message> Pool = new();

    public static Message Rent()
    {
        return Pool.TryTake(out var msg) ? msg : new Message();
    }

    public static void Return(Message msg)
    {
        msg.Reset();
        Pool.Add(msg);
    }
}
```

### Batch Updates

```csharp
public class BatchUpdateRoom : Room
{
    private List<PlayerAction> _pendingActions = new();
    private Timer _batchTimer;

    public override async Task RoundStartedAsync(ushort roundId)
    {
        _batchTimer = new Timer(_ => FlushActions(), null, 0, 100); // Every 100ms
    }

    public void QueueAction(PlayerAction action)
    {
        lock (_pendingActions)
        {
            _pendingActions.Add(action);
        }
    }

    private void FlushActions()
    {
        List<PlayerAction> actions;
        lock (_pendingActions)
        {
            actions = new List<PlayerAction>(_pendingActions);
            _pendingActions.Clear();
        }

        if (actions.Count > 0)
        {
            foreach (var user in Users)
                user?.Send("BatchActions", new { actions });
        }
    }
}
```

---

## Testing Strategies

### Bot Load Testing

```csharp
public class LoadTester
{
    public static async Task SimulateLoad(int botCount)
    {
        var bots = new List<MyUser>();
        
        for (int i = 0; i < botCount; i++)
        {
            var bot = new MyUser() { Username = $"Bot_{i}" };
            bots.Add(bot);
            await RoomManager.JoinAsync<MyGameRoom>(bot);
            await Task.Delay(100); // Stagger joins
        }

        // Simulate actions
        _ = Task.Run(async () =>
        {
            while (true)
            {
                foreach (var bot in bots.Where(b => b.CurrentRoom != null))
                {
                    bot.AddScore(Random.Shared.Next(1, 10));
                }
                await Task.Delay(1000);
            }
        });
    }
}
```

---

## Deployment Best Practices

### Graceful Shutdown

```csharp
public class GracefulServer : Server
{
    private static CancellationTokenSource _shutdownCts = new();

    public GracefulServer(string address) : base(address) { }

    public static void InitiateShutdown()
    {
        Console.WriteLine("Shutting down gracefully...");
        _shutdownCts.Cancel();

        // Notify all users
        var allUsers = UsersManager.GetAll();
        foreach (var user in allUsers)
        {
            user?.Send("ServerShutdown", new { message = "Server restarting in 30 seconds" });
        }

        // Wait for games to complete
        Task.Delay(30000).Wait();

        // Force close connections
        Environment.Exit(0);
    }
}

// Handle Ctrl+C
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    GracefulServer.InitiateShutdown();
};
```

### Health Monitoring

```csharp
public class HealthMonitor
{
    public static void StartMonitoring()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var stats = new
                {
                    connectedUsers = UsersManager.GetUsersCount(),
                    activeRooms = RoomManager.GetRooms<Room>().Length,
                    memoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    timestamp = DateTime.UtcNow
                };

                Console.WriteLine($"[Health] Users: {stats.connectedUsers}, Rooms: {stats.activeRooms}, Memory: {stats.memoryMB}MB");
                
                await Task.Delay(10000); // Every 10 seconds
            }
        });
    }
}
```

---

For more examples, see [QUICK_START.md](QUICK_START.md) and [API_REFERENCE.md](API_REFERENCE.md).
