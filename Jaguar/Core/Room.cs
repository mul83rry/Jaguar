using System.Collections.Immutable;
using Jaguar.Enums;
using Jaguar.Manager;

namespace Jaguar.Core
{
    [Serializable]
    public abstract class Room
    {
        private uint _maxUser;

        private bool _roomReadyInvoked;

        private Range _usersCount;

        /// <summary>
        /// Rooms access level.
        /// </summary>
        public AccessMode AccessMode { get; init; }

        public bool AllUsersJoined => _maxUser == Users.Count;
        //public bool AllUsersJoined => _maxUser == Users.Count(u => u is { InRoom: true });

        /// <summary>
        /// it return current playing round.
        /// it can be 'null' if 'GameStarted' is false or 'GameComplete' equals to true.
        /// </summary>
        public Round? CurrentRound => Rounds.SingleOrDefault(round => round is { Completed: false });

        /// <summary>
        /// check if game completed.
        /// </summary>
        // check for all round complete?
        public bool GameComplete => GameStarted && Rounds.All(r => r.Completed);

        /// <summary>
        /// this value specify`s that if the game started .
        /// </summary>
        public bool GameStarted { get; private set; }

        /// <summary>
        /// return true if a round is playing.
        /// </summary>
        public bool IsPlaying => CurrentRound != null;

        /// <summary>
        /// room level.
        /// </summary>
        public Range Level { get; init; }

        public bool EnableJoinAfterGameStarted { get; protected set; } = false; // Todo: check

        /// <summary>
        /// room password.
        /// </summary>
        public string Password { get; init; }

        public int RoundsCount => Rounds.Length;

        /// <summary>
        /// unique id of room.
        /// </summary>
        public long UniqueId { get; internal set; }

        /// <summary>
        /// Type of room, use for matchmaking, default value is 0
        /// </summary>
        public uint TypeId { get; set; } = 0;

        /// <summary>
        /// it show`s list of users in the room.
        /// </summary>
        public ImmutableList<User?> Users { get; private set; }

        /// <summary>
        /// range of users count.
        /// </summary>
        public Range UsersCount
        {
            get => _usersCount;
            set
            {
                _maxUser = value.End;
                _usersCount = value;
            }
        }

        public DateTime CreationTime { get; private set; }

        #region Rooms events

        /// <summary>
        /// game completed.
        /// </summary>
        public virtual Task GameCompletedAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// new user joined to room.
        /// </summary>
        /// <param name="user"></param>
        public virtual Task NewUserJoinedAsync(User user)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// required users has joined room.
        /// </summary>
        /// <param name="users">list of users in room</param>
        public virtual Task RoomReadyForStartAsync(IEnumerable<User> users)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// round with id 'roundId' started, 'roundId' start`s from '0'.
        /// </summary>
        /// <param name="roundId"></param>
        public virtual Task RoundStartedAsync(ushort roundId)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 'user' has quit.
        /// </summary>
        /// <param name="user"></param>
        public virtual Task UserExitedAsync(User user)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 'user' has kicked from the room.
        /// </summary>
        /// <param name="user"></param>
        public virtual Task UserKickedAsync(User user)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 'user' has been re joined to the room.
        /// </summary>
        /// <param name="user"></param>
        public virtual Task OnUserRejoined(User user)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 'user' has rejoined.
        /// </summary>
        /// <param name="user"></param>
        public virtual Task OnUserRejoinedAsync(User user)
        {
            return Task.CompletedTask;
        }

        #endregion Rooms events

        internal Round[] Rounds { get; set; }

        #region contructors

        /// <summary>
        /// default value of 'UserCount' is '2'.
        /// default value of 'AccessMode' is 'AccessMode.public'.
        /// </summary>
        /// <param name="roundCount">rounds count</param>
        protected Room(int roundCount)
        {
            Users = ImmutableList<User?>.Empty;
            CreationTime = DateTime.Now;
            Rounds = new Round[roundCount]; // Todo: test
            Password = string.Empty;
            UsersCount = new Range(2);
            _maxUser = UsersCount.End;
            AccessMode = AccessMode.Public;

            RoomManager.AddRoom(this);
        }

        #endregion contructors

        /// <summary>
        /// return true if all users defined in 'range' variable joined the room.
        /// </summary>
        /// <summary>
        /// add new user to room.
        /// </summary>
        /// <param name="user">user want join the room</param>
        /// <param name="pwd"></param>
        /// <exception cref="Exception">'GameStarted' and 'AllUsersJoined' must be 'false'.</exception>        
        public async Task<bool> AddUserAsync(User user, string pwd = "") // todo: check this
        {
            if (GameStarted && !EnableJoinAfterGameStarted) return false;
            if (Users.Count == _maxUser) return false;
            if (!string.IsNullOrEmpty(pwd) && string.IsNullOrEmpty(Password)) return false;
            if (!string.IsNullOrEmpty(Password) && Password != pwd) return false;

            if (Users.Any(u => u.UniqueId == user.UniqueId))
            {
                return false;
            }

            user.SetAsCurrentRoom(this);
            Users = Users.Add(user);


            // call new user join event
            await NewUserJoinedAsync(user);

            // check users count
            if (!UsersCount.InRange((uint)Users.Count)) return true;
            if (_roomReadyInvoked) return true;
            _roomReadyInvoked = true;
            // call room ready event
            await RoomReadyForStartAsync(Users.ToArray());

            return true;
        }

        //public void CleanUsers()
        //{
        //    foreach (var user in Users.Where(u => u is { InRoom: false }))
        //    {
        //        if (user != null) Server.RemoveUsersUniqueId(user.UniqueId);
        //    }

        //    Users = Users.RemoveAll(u => u is { InRoom: false });
        //}

        public void Destroy()
        {
            while (Users.Count > 0)
            {
                Users[0]?.SetCurrentRoomToNull();
                Users = Users.RemoveAt(0);
            }

            RoomManager.RemoveFromList(this);
            // todo: stop all tasks too
        }

        public async Task<bool> ForceGameToEndAsync()
        {
            if (!GameStarted)
                return false;
            //throw new Exception("Game not started.");

            await GameCompletedAsync();
            RoomManager.RemoveFromList(this);

            return true;
        }

        /// <summary>
        /// kick an user from the room.
        /// </summary>
        /// <param name="user">'user' how kicked from the room.</param>
        public async Task KickUserAsync(User? user)
        {
            RemoveFromRoom(user, false);
            await UserKickedAsync(user);
        }

        /// <summary>
        /// remove an user from the room.
        /// </summary>
        /// <param name="user">'user' how removed from the room.</param>
        /// <param name="newUser">'user' how replaced.</param>
        public async Task RemoveUserAndReplaceAsync(User? user, User? newUser)
        {
            RemoveFromRoomWithoutCloneTheUser(user);
            Users = Users.Replace(user, newUser);

            await UserExitedAsync(user);
        }

        /// <summary>
        /// remove an user from the room.
        /// </summary>
        /// <param name="user">'user' how removed from the room.</param>
        public async Task RemoveUserAsync(User? user)
        {
            RemoveFromRoom(user);
            await UserExitedAsync(user);
        }

        /// <summary>
        /// it search for first playable round.
        ///     if found, it start the round and Round and 'RoundStarted()' event calls.
        ///     otherwise, 'GameCompleted()' event calls.
        /// </summary>
        /// <exception cref="Exception">throw an error, if users count isn`t in 'UserCount' range.</exception>
        public async Task<bool> StartRoundAsync()
        {
            if (Users.Count < UsersCount.Start)
            {
                if (!GameStarted)
                    return false;
            }


            if (GameComplete) // check if game complete!
            {
                await GameCompletedAsync();
                RoomManager.RemoveFromList(this);
                return true;
            }
            // get last round complete
            var lastFinishedRound = Rounds.LastOrDefault(round => round is { Completed: true });

            ushort index = 0;
            if (lastFinishedRound != null)
            {
                // first round has been completed
                index = lastFinishedRound.Index;
                index++;
            }

            GameStarted = true;
            Rounds[index] = new Round(index, this);
            await RoundStartedAsync(index);
            return true;
        }

        internal async Task UserLeftAsync(User? user)
        {
            RemoveFromRoom(user);
            await UserExitedAsync(user);
        }

        private static void RemoveFromRoomWithoutCloneTheUser(User? user) => user?.SetCurrentRoomToNull();

        private void RemoveFromRoom(User? user, bool keepCash = true)
        {
            if (Users.Count == 0) return;

            if (GameStarted)
            {
                if (user == null) return;
                user.SetCurrentRoomToNull();

                if (!keepCash)
                {
                    Users = Users.Remove(user);
                }
                else
                {
                    var clonedUser = user.ShallowCopy();
                    var oldUser = Users.SingleOrDefault(u => u.UniqueId == user.UniqueId);
                    if (oldUser == null) return;

                    Users = Users.Replace(user, clonedUser);
                    //clonedUser.InRoom = false;
                }
            }
            else
            {
                if (user == null) return;
                Users = Users.Remove(user);
                user.SetCurrentRoomToNull();
            }

        }
    }
}