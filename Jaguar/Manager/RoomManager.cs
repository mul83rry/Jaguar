using System.Collections.Immutable;
using System.Net;
using Jaguar.Core;
using Jaguar.Enums;

namespace Jaguar.Manager
{
    public static class RoomManager
    {
        /// <summary>
        /// return all active rooms.
        /// </summary>
        private static ImmutableList<Room> Rooms { get; set; } = ImmutableList<Room>.Empty;


        public static T[] GetRooms<T>() where T : Room
        {
            var result = Array.Empty<T>();
            var typeOfRoomCount = Rooms.OfType<T>().Count();
            if (typeOfRoomCount == 0) return result;
            var rooms = Rooms.OfType<T>().ToArray();
            if (rooms.Length == 0) return result;
            result = new T[rooms.Length];
            Array.Copy(rooms, result, typeOfRoomCount);
            return result;
        }


        /// <summary>
        /// return room with unique id 'id'
        /// </summary>
        /// <param name="id">unique id of room</param>
        /// <returns></returns>
        public static Room? FindRoom(long id) => GetRooms<Room>().SingleOrDefault(r => r.UniqueId == id);
    
        public static Room? FindRoomWithUser(long id) => GetRooms<Room>().SingleOrDefault(r => r.Users.Any(u => u.UniqueId == id));

        /// <summary>
        /// return room with user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static Room FindRoom(User user) => GetRooms<Room>().SingleOrDefault(r => r.Users.Any(u => u != null && u.UniqueId == user.UniqueId))!;

        /// <summary>
        /// join to a random room
        /// </summary>
        /// <param name="user"></param>
        /// <returns>true if can join</returns>
        public static async Task<bool> JoinAsync(User user)
        {
            if (user.InRoom) return false;
            var room = FindRoomWithType(0);
            if (room == null) return false;

            return await room.AddUserAsync(user);
        }

        /// <summary>
        /// join to a room with choose options
        /// </summary>
        /// <param name="user"></param>
        /// <param name="options"></param>
        /// <returns>true if can join</returns>
        public static async Task<bool> JoinAsync(User user, JoinOptions options)
        {
            if (user.InRoom) return false;
            if (options.RoomId.HasValue)
            {
                var room = FindRoom(id: options.RoomId.Value);

                if (room == null)
                    return false;

                var pwd = options.Password ?? string.Empty;

                return await room.AddUserAsync(user, pwd);
            }

            if (options.TypeId.HasValue)
            {
                if (options.Level.HasValue)
                {
                    var room1 = FindRoomWithTypeIdAndLevel(options.Level.Value, options.TypeId.Value);
                    if (room1 == null)
                        return false;

                    return await room1.AddUserAsync(user);
                }

                var room2 = FindRoomWithType(options.TypeId.Value);
                if (room2 == null)
                    return false;

                return await room2.AddUserAsync(user);
            }

            if (options.Level.HasValue)
            {
                var room = FindRoomWithLevel(options.Level.Value);
                if (room == null)
                    return false;

                return await room.AddUserAsync(user);
            }

            return false;
        }

        public static bool ReJoin(long uniqueId, IPEndPoint client, out User? user)
        {
            var room = GetRooms<Room>().LastOrDefault(r => r.Users.Any(u => u != null && u.UniqueId == uniqueId));
            if (room == null)
            {
                user = null;
                return false;
            }

            user = room.Users.Single(u => u != null && u.UniqueId == uniqueId) ?? throw new InvalidOperationException();
            user.UpdateClient(client);
            user.Room = room;
            room.OnUserRejoined(user);

            return true;
        }

        internal static void AddRoom(Room room)
        {
            if (room == null)
                throw new Exception("room is null");

            room.UniqueId = GenerateUniqueId();

            Rooms = Rooms.Add(room);
        }

        internal static void RemoveFromList(Room room)
        {
            if (room == null)
                throw new Exception("room is null");

            Rooms = Rooms.Remove(room);

            // set user room to null

            foreach (var user in room.Users)
            {
                if (user != null) user.Room = null;
            }
        }

        private static Room? FindRoomWithType(uint typeId) => GetRooms<Room>().FirstOrDefault(r => (!r.IsPlaying || r.EnableJoinAfterGameStarted) && r.AccessMode == AccessMode.Public
            && !r.AllUsersJoined && typeId == r.TypeId && r.Users.Count(rr => rr is
            {
                InRoom: true
            }) > 0);

        private static Room? FindRoomWithLevel(uint level) => GetRooms<Room>().FirstOrDefault(r => (!r.IsPlaying || r.EnableJoinAfterGameStarted)
            && r.AccessMode == AccessMode.Public && r.AllUsersJoined == false && r.Level.InRange(level) && r.Users.Count(rr => rr is
            {
                InRoom: true
            }) > 0);

        private static Room? FindRoomWithTypeIdAndLevel(uint level, uint typeId) =>
            GetRooms<Room>().FirstOrDefault(r => (!r.IsPlaying || r.EnableJoinAfterGameStarted) && r.Level.InRange(level) && r.AccessMode == AccessMode.Public && !r.AllUsersJoined && r.Users.Count(rr => rr is
            {
                InRoom: true
            }) > 0 && r.TypeId == typeId);

        private static long GenerateUniqueId()
        {
            var newId = new Random().Next(11111, 99999);

            while (true)
            {
                if (Rooms.All(r => r.UniqueId != newId))
                    break;

                newId = new Random().Next(11111, 99999);
            }

            return newId;
        }
    }
}