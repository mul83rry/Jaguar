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

        public static T[] FindRoomsWithUser<T>(long userId) where T : Room =>
            GetRooms<T>().Where(r => r.Users.Any(u => u != null && u.UniqueId == userId)).ToArray();

        /// <summary>
        /// return room with user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static T[] FindRooms<T>(User user) where T : Room =>
            GetRooms<T>().Where(r => r.Users.Any(u => u != null && u.UniqueId == user.UniqueId)).ToArray();

        /// <summary>
        /// join to a random room
        /// </summary>
        /// <param name="user"></param>
        /// <returns>true if can join</returns>
        public static async Task<bool> JoinAsync<T>(User user) where T : Room
        {
            //if (user.InRoom) return false;
            var rooms = FindRoomsWithType<T>(typeId: 0);
            if (!rooms.Any()) return false;

            return await rooms[0].AddUserAsync(user);
        }

        /// <summary>
        /// join to a room with choose options
        /// </summary>
        /// <param name="user"></param>
        /// <param name="options"></param>
        /// <returns>true if can join</returns>
        public static async Task<bool> JoinAsync<T>(User user, JoinOptions options) where T : Room
        {
            //if (user.InRoom) return false;
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
                    var rooms1 = FindRoomsWithTypeIdAndLevel<T>(options.Level.Value, options.TypeId.Value);
                    if (!rooms1.Any())
                        return false;

                    return await rooms1[0].AddUserAsync(user);
                }

                var rooms2 = FindRoomsWithType<T>(options.TypeId.Value);
                if (!rooms2.Any())
                    return false;

                return await rooms2[0].AddUserAsync(user);
            }

            if (options.Level.HasValue)
            {
                var rooms = FindRoomsWithLevel<T>(options.Level.Value);
                if (!rooms.Any())
                    return false;

                return await rooms[0].AddUserAsync(user);
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
            //user.Rooms = room;
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
                user?.SetCurrentRoomToNull();
            }
        }

        private static T[] FindRoomsWithType<T>(uint typeId) where T : Room =>
            GetRooms<T>().Where(r => (!r.IsPlaying || r.EnableJoinAfterGameStarted) && r.AccessMode == AccessMode.Public
            && !r.AllUsersJoined && typeId == r.TypeId && r.Users.Count > 0).ToArray();

        private static T[] FindRoomsWithLevel<T>(uint level) where T : Room =>
            GetRooms<T>().Where(r => (!r.IsPlaying || r.EnableJoinAfterGameStarted)
            && r.AccessMode == AccessMode.Public && r.AllUsersJoined == false && r.Level.InRange(level) && r.Users.Count > 0)
                .ToArray();

        private static T[] FindRoomsWithTypeIdAndLevel<T>(uint level, uint typeId) where T : Room =>
            GetRooms<T>()
                .Where(r => (!r.IsPlaying || r.EnableJoinAfterGameStarted) && r.Level.InRange(level) && r.AccessMode == AccessMode.Public && !r.AllUsersJoined && r.Users.Count > 0 && r.TypeId == typeId)
                .ToArray();

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