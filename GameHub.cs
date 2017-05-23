using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using OnlineMemoryGame.Models;
using Microsoft.AspNet.SignalR;

namespace OnlineMemoryGame
{
    public class GameHub : Hub
    {
        public bool Join(string userName)
        {
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                Clients.Caller.playerExist(); // js
                return true;
            }
            else // create player
            {
                player = GameState.Instance.CreatePlayer(userName);
                player.ConnectionId = Context.ConnectionId;
                Clients.Caller.name = player.Name;
                Clients.Caller.hash = player.Hash;
                Clients.Caller.id = player.Id;

                Clients.Caller.playerJoined(player); // js
                return StartGame(player);
            }
        }

        private bool StartGame(Player player)
        {
            if (player != null)
            {
                Player player2;
                var game = GameState.Instance.FindGame(player, out player2);
                if (game != null)
                {
                    Clients.Group(player.Group).buildBoard(game); // js
                    return true;
                }
                player2 = GameState.Instance.GetNewOppenent(player);

                if (player2 == null)
                {
                    Clients.Caller.waitingList();
                    return true;
                }

                game = GameState.Instance.CreateGame(player, player2);
                game.WhosTurn = player.Id;

                Clients.Group(player.Group).buildBoard(game); // js
                return true;
            }
            return true;
        }

        public bool Flip(string cardName)
        {
            var userName = Clients.Caller.name;
            Player player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                Player playerOppenent;
                Game game = GameState.Instance.FindGame(player, out playerOppenent);
                if (game != null)
                {
                    if (!string.IsNullOrWhiteSpace(game.WhosTurn) && game.WhosTurn != player.Id)
                    {
                        return false;
                    }

                    Card card = FindCard(game, cardName);

                    if (card != null)
                    {
                        Clients.Group(player.Group).flipCard(card); // calls javascript function for displaying.
                        return true;
                    }
                }
            }

            return false;
        }

        public bool CheckCard(string cardName)
        {
            var userName = Clients.Caller.name;
            Player player = GameState.Instance.GetPlayer(userName);
            if (player != null)
            {
                Player playerOppenent;
                Game game = GameState.Instance.FindGame(player, out playerOppenent);
                if (game != null)
                {
                    if (!string.IsNullOrWhiteSpace(game.WhosTurn) && game.WhosTurn != player.Id)
                        return true;

                    Card card = FindCard(game, cardName);

                    if (game.LastCard == null) // first flip
                    {
                        game.WhosTurn = player.Id;
                        game.LastCard = card;
                        return true;
                    }
                    else // second card
                    {
                        bool isMatched = IsMatch(game, card);
                        if (isMatched)
                        {
                            StoreMatch(player, card);
                            game.LastCard = null;
                            Clients.Group(player.Group).showMatch(card, userName);

                            if (player.Matches.Count > 15)
                            {
                                Clients.Group(player.Group).winner(card, userName); // js function
                                GameState.Instance.ResetGame(game);
                                return true;
                            }
                            return true;
                        }
                        else // not matched
                        {
                            Player oppenent = GameState.Instance.GetOppenent(player, game);
                            game.WhosTurn = oppenent.Id;
                            Clients.Group(player.Group).resetFlip(game.LastCard, card); // js function
                            game.LastCard = null;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void SendMessage(string userName, string message)
        {
            Clients.All.addMessage(userName, message);
        }

        private void StoreMatch(Player player, Card card)
        {
            player.Matches.Add(card.Id);
            player.Matches.Add(card.Pair);
        }

        private bool IsMatch(Game game, Card card)
        {
            if (card == null)
                return false;

            if (game.LastCard != null)
            {
                if (game.LastCard.Pair == card.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private Card FindCard(Game game, string cardName)
        {
            return game.Board.Pieces.FirstOrDefault(c => c.Name == cardName);
        }
    }
}