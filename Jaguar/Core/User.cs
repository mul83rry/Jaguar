﻿using System.Net;
using Jaguar.Extensions;

namespace Jaguar.Core
{
    [Serializable]
    public abstract class User
    {
        public const double RoomNotFoundCode = -1;
        public const double RoundNotFoundCode = -2;
        public const double RoomNotStartedCode = -3;

        /// <summary>
        /// it return current room witch user joined.
        /// </summary>
        [NonSerialized] internal IPEndPoint? Client;

        private Room? _room;

        public bool InRoom { get; internal set; }

        public bool IsOnline { get; internal set; }

        public DateTime LastActivateTime => Server.GetClients()[Client.ConvertToKey()].LastActivateTime;

        /// <summary>
        /// user current room
        /// </summary>
        public Room? Room
        {
            get => _room; internal set
            {
                _room = value;
                InRoom = value != null;
            }
        }

        /// <summary>
        /// unique id of user
        /// </summary>
        public long UniqueId { get; }

        #region constructor

        /// <summary>
        /// there is two kind of constructor for user.
        /// this constructor is for bot or some thing like that
        /// </summary>
        protected User()
        {
            UniqueId = Server.GenerateUniqueUserId();
            //IsOnline = true;
        }

        /// <summary>
        /// there is two kind of constructor for user.
        /// this constructor is for real users, witch needs Sender variable as an argument
        /// </summary>
        protected User(IPEndPoint? ipEndPoint)
        {
            if (ipEndPoint == null)
                throw new NullReferenceException("Sender can not be null");

            Server.UpdateClient(this, ipEndPoint);

            UniqueId = Server.GenerateUniqueUserId();
            IsOnline = true;
            Client = ipEndPoint;
        }

        #endregion constructor

        /*public void DestroyOtherClients()
        {
            if (Client == null) return;
            UDPServer.ClientsList.RemoveAll(u => u.Client == Client && u.User != null && u.User.UniqueId != UniqueId);
        }*/

        /// <summary>
        /// add one score to this user in current round
        /// </summary>
        /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null.</exception>
        public bool AddScore()
        {
            if (Room == null) return false;
            if (Room.CurrentRound == null) return false;

            Room.CurrentRound.TryAddScore(this);
            return true;
        }

        /// <summary>
        /// add specified score to this user in specified round
        /// </summary>
        /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null.</exception>
        public bool AddScore(double count, int roundIndex)
        {
            if (Room == null) return false;
            if (roundIndex >= Room.Rounds.Length) return false;

            Room.Rounds[roundIndex].TryAddScore(this, count);
            return true;
        }

        /// <summary>
        /// add specified score to this user in current round
        /// </summary>
        /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null.</exception>
        public bool AddScore(double count)
        {
            if (Room == null) return false;
            if (Room.CurrentRound == null) return false;

            Room.CurrentRound.TryAddScore(this, count);
            return true;
        }

        public bool TryGetTotalScore(out double result, out int roundsCount)
        {
            roundsCount = 0;
            result = 0;
            if (Room is not { GameStarted: true }) return false;

            //if (_room != null) roundsCount = _room.Rounds.Length;
            foreach (var round in Room.Rounds)
            {
                if (round == null) continue; // todo: test
                roundsCount++;
                round.TryGetScore(this, out var roundScore);
                result += roundScore;
            }
            return true;
        }

        /// <summary>
        /// score of user in specified round
        /// </summary>
        /// <param name="roundIndex"></param>
        /// <returns>return score of user in specified round</returns>
        /// <exception cref="Exception">throw an error if user is`t currently present in any round or room of user is null or round index is out of range.</exception>
        public double Score(int roundIndex)
        {
            if (Room == null) return RoomNotFoundCode; // throw new Exception("room not found 6");
            if (roundIndex > Room.Rounds.Length) throw new IndexOutOfRangeException("Index out of range");
            if (Room.Rounds[roundIndex] == null) throw new Exception("Round not started");


            Room.Rounds[roundIndex].TryGetScore(this, out var score);
            return score;
        }

        /// <summary>
        /// this is for real users, witch get Sender variable for update.
        /// </summary>
        /// <param name="client">Sender of user</param>
        public void UpdateClient(IPEndPoint? client)
        {
            Server.UpdateClient(this, client);
            Client = client;
        }

        internal User ShallowCopy() => (User)MemberwiseClone();
    }
}