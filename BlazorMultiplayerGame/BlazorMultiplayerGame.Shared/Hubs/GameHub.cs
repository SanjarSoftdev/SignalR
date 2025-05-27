using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using BlazorMultiplayerGame.Shared.Models;
using System.Numerics;

namespace BlazorMultiplayerGame.Shared.Hubs
{
    public class GameHub : Hub
    {
        private static GameState _gameState = new();
        private const float FIELD_WIDTH = 20f;
        private const float FIELD_HEIGHT = 10f;
        private const float GOAL_WIDTH = 3f;
        private const float PLAYER_SPEED = 0.2f;
        private const float BALL_SPEED = 0.3f;
        private const float FRICTION = 0.98f;

        public async Task JoinGame(string playerName)
        {
            if (_gameState.Players.Count >= 2)
            {
                await Clients.Caller.SendAsync("GameFull");
                return;
            }

            var side = _gameState.Players.Count == 0 ? PlayerSide.Left : PlayerSide.Right;
            var startX = side == PlayerSide.Left ? 2f : FIELD_WIDTH - 2f;
            var startY = FIELD_HEIGHT / 2f;

            var player = new Player
            {
                Id = Context.ConnectionId,
                Name = playerName,
                Position = new Vector2(startX, startY),
                Velocity = Vector2.Zero,
                Side = side,
                Score = 0
            };

            _gameState.Players[Context.ConnectionId] = player;
            await Clients.All.SendAsync("PlayerJoined", player);

            if (_gameState.Players.Count == 2)
            {
                _gameState.IsGameStarted = true;
                ResetBall();
                await Clients.All.SendAsync("GameStarted", _gameState);
            }
            else
            {
                await Clients.Caller.SendAsync("WaitingForOpponent");
            }
        }

        public async Task MovePlayer(Vector2 direction)
        {
            if (!_gameState.IsGameStarted || !_gameState.Players.TryGetValue(Context.ConnectionId, out var player))
                return;

            // Update player velocity based on input
            player.Velocity = direction * PLAYER_SPEED;

            // Update position
            var newPosition = player.Position + player.Velocity;

            // Keep player in their half
            if (player.Side == PlayerSide.Left)
            {
                newPosition.X = Math.Clamp(newPosition.X, 0, FIELD_WIDTH / 2);
            }
            else
            {
                newPosition.X = Math.Clamp(newPosition.X, FIELD_WIDTH / 2, FIELD_WIDTH);
            }
            newPosition.Y = Math.Clamp(newPosition.Y, 0, FIELD_HEIGHT);

            player.Position = newPosition;
            await Clients.All.SendAsync("PlayerMoved", player);

            // Check for ball collision
            await CheckBallCollision(player);
        }

        private async Task CheckBallCollision(Player player)
        {
            var ball = _gameState.Ball;
            var distance = Vector2.Distance(player.Position, ball.Position);
            
            if (distance < player.Velocity.Length() + ball.Radius)
            {
                // Calculate ball velocity based on player's movement
                var newVelocity = player.Velocity * BALL_SPEED;
                ball.Velocity = newVelocity;
                await Clients.All.SendAsync("BallMoved", ball);
            }
        }

        public async Task UpdateGameState()
        {
            if (!_gameState.IsGameStarted)
                return;

            // Update ball position
            var ball = _gameState.Ball;
            var newPosition = ball.Position + ball.Velocity;
            var newVelocity = ball.Velocity * FRICTION;

            // Ball collision with walls
            if (newPosition.Y <= 0 || newPosition.Y >= FIELD_HEIGHT)
            {
                newVelocity.Y *= -1;
                newPosition.Y = Math.Clamp(newPosition.Y, 0, FIELD_HEIGHT);
            }

            // Check for goals
            if (newPosition.X <= 0)
            {
                var rightPlayer = _gameState.Players.Values.FirstOrDefault(p => p.Side == PlayerSide.Right);
                if (rightPlayer != null && Math.Abs(newPosition.Y - FIELD_HEIGHT / 2) <= GOAL_WIDTH / 2)
                {
                    rightPlayer.Score++;
                    await Clients.All.SendAsync("GoalScored", rightPlayer);
                    if (rightPlayer.Score >= _gameState.WinningScore)
                    {
                        _gameState.Winner = rightPlayer.Id;
                        await Clients.All.SendAsync("GameOver", _gameState);
                        return;
                    }
                }
                ResetBall();
            }
            else if (newPosition.X >= FIELD_WIDTH)
            {
                var leftPlayer = _gameState.Players.Values.FirstOrDefault(p => p.Side == PlayerSide.Left);
                if (leftPlayer != null && Math.Abs(newPosition.Y - FIELD_HEIGHT / 2) <= GOAL_WIDTH / 2)
                {
                    leftPlayer.Score++;
                    await Clients.All.SendAsync("GoalScored", leftPlayer);
                    if (leftPlayer.Score >= _gameState.WinningScore)
                    {
                        _gameState.Winner = leftPlayer.Id;
                        await Clients.All.SendAsync("GameOver", _gameState);
                        return;
                    }
                }
                ResetBall();
            }

            // Ball collision with side walls
            if (newPosition.X <= 0 || newPosition.X >= FIELD_WIDTH)
            {
                newVelocity.X *= -1;
                newPosition.X = Math.Clamp(newPosition.X, 0, FIELD_WIDTH);
            }

            ball.Position = newPosition;
            ball.Velocity = newVelocity;

            await Clients.All.SendAsync("GameStateUpdated", _gameState);
        }

        private void ResetBall()
        {
            _gameState.Ball.Position = new Vector2(FIELD_WIDTH / 2, FIELD_HEIGHT / 2);
            _gameState.Ball.Velocity = Vector2.Zero;
        }

        public async Task ResetGame()
        {
            if (!_gameState.IsGameStarted)
                return;

            _gameState.Winner = null;
            _gameState.Ball.Position = new Vector2(FIELD_WIDTH / 2, FIELD_HEIGHT / 2);
            _gameState.Ball.Velocity = Vector2.Zero;

            foreach (var player in _gameState.Players.Values)
            {
                player.Score = 0;
                player.Position = new Vector2(
                    player.Side == PlayerSide.Left ? 2f : FIELD_WIDTH - 2f,
                    FIELD_HEIGHT / 2f
                );
                player.Velocity = Vector2.Zero;
            }

            await Clients.All.SendAsync("GameStateUpdated", _gameState);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_gameState.Players.TryGetValue(Context.ConnectionId, out var player))
            {
                _gameState.Players.Remove(Context.ConnectionId);
                _gameState.IsGameStarted = false;
                ResetBall();
                await Clients.All.SendAsync("PlayerLeft", player.Id);
                await Clients.All.SendAsync("GameStateUpdated", _gameState);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
