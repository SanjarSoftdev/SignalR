using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorMultiplayerGame.Shared.Hubs
{
    public class GameHub : Hub
    {
        private static Dictionary<string, Player> _players = new();

        public async Task JoinGame(string playerName)
        {
            var player = new Player
            {
                Id = Context.ConnectionId,
                Name = playerName,
                X = Random.Shared.Next(0, 10),
                Y = Random.Shared.Next(0, 10)
            };

            _players[Context.ConnectionId] = player;
            await Clients.All.SendAsync("PlayerJoined", player);
            await Clients.Caller.SendAsync("AllPlayers", _players.Values);
        }

        public async Task MovePlayer(int x, int y)
        {
            if (_players.TryGetValue(Context.ConnectionId, out var player))
            {
                player.X = x;
                player.Y = y;
                await Clients.All.SendAsync("PlayerMoved", player);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_players.TryGetValue(Context.ConnectionId, out var player))
            {
                _players.Remove(Context.ConnectionId);
                await Clients.All.SendAsync("PlayerLeft", player.Id);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }

    public class Player
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}
