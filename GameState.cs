using OnlineMemoryGame.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace OnlineMemoryGame
{
    public class GameState
    {
        private readonly static Lazy<GameState> _instance = new Lazy<GameState>(() => new GameState(GlobalHost.ConnectionManager.GetHubContext<GameHub>()));
        private readonly ConcurrentDictionary<string, Player> _players = new ConcurrentDictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Game> _games = new ConcurrentDictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
        private IHubConnectionContext<dynamic> Clients;
        private IGroupManager Groups;

        private GameState(IHubContext context)
        {
            Clients = context.Clients;
            Groups = context.Groups;
        }

        public static GameState Instance
        {
            get { return _instance.Value; }
        }

        private string GetMD5Hash(string userName)
        {
            return String.Join("", MD5.Create().ComputeHash(Encoding.Default.GetBytes(userName)).Select(b => b.ToString("x2")));
        }


        public Player CreatePlayer(string userName)
        {
            var player = new Player(userName, userName);
            _players[userName] = player;
            return player;
        }

        public Player GetPlayer(string userName)
        {
            return _players.Values.FirstOrDefault(u => u.Name == userName);
        }

        public Player GetNewOppenent(Player player)
        {
            return _players.Values.FirstOrDefault(u => !u.IsPlaying && u.Id != player.Id);
        }

        public Player GetOppenent(Player player, Game game)
        {
            if (game.Player1.Id == player.Id)
                return game.Player2;
            else
                return game.Player1;
        }

        public Game CreateGame(Player player1, Player player2)
        {
            var game = new Game()
            {
                Player1 = player1,
                Player2 = player2,
                Board = new Board()
            };

            var group = Guid.NewGuid().ToString("d");
            _games[group] = game;

            player1.IsPlaying = true;
            player1.Group = group;

            player2.IsPlaying = true;
            player2.Group = group;

            Groups.Add(player1.ConnectionId, group);
            Groups.Add(player2.ConnectionId, group);

            return game;
        }

        public Game FindGame(Player player, out Player oppenent)
        {
            oppenent = null;
            if (player.Group == null)
                return null;

            _games.TryGetValue(player.Group, out Game game);
            if (game != null)
            {
                if (player.Id == game.Player1.Id)
                    oppenent = game.Player2;
                else
                    oppenent = game.Player1;

                return game;
            }
            return null;
        }

        public void ResetGame(Game game)
        {
            var groupName = game.Player1.Group;

            Groups.Remove(game.Player1.ConnectionId, groupName);
            Groups.Remove(game.Player2.ConnectionId, groupName);

            _players.TryRemove(game.Player1.Name, out Player player1);
            _players.TryRemove(game.Player2.Name, out Player player2);

            _games.TryRemove(groupName, out game);
        }
    }
}