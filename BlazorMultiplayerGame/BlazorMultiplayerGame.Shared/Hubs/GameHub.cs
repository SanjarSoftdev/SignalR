using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using BlazorMultiplayerGame.Shared.Models;
using System.Numerics;

namespace BlazorMultiplayerGame.Shared.Hubs
{
    public class GameHub : Hub
    {
        private static GameState _gameState = new();
        private const float FIELD_WIDTH = 10f;
        private const float FIELD_HEIGHT = 20f;
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

            // Center players vertically in their respective halves
            var startY = side == PlayerSide.Left ? FIELD_HEIGHT * 0.25f : FIELD_HEIGHT * 0.75f;
            var startX = FIELD_WIDTH / 2f;

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

            // Normalize direction if not zero
            if (direction != Vector2.Zero)
            {
                direction = Vector2.Normalize(direction);
            }

            // Update player velocity based on input
            player.Velocity = direction * PLAYER_SPEED;

            // Calculate new position
            var newPosition = player.Position + player.Velocity;

            // Boundary checks with buffer
            const float boundaryBuffer = 0.5f;

            // Horizontal bounds (same for both players)
            newPosition.X = Math.Clamp(newPosition.X, boundaryBuffer, FIELD_WIDTH - boundaryBuffer);

            // Vertical bounds based on player side
            if (player.Side == PlayerSide.Left)
            {
                newPosition.Y = Math.Clamp(newPosition.Y, boundaryBuffer, FIELD_HEIGHT / 2 - boundaryBuffer);
            }
            else
            {
                newPosition.Y = Math.Clamp(newPosition.Y, FIELD_HEIGHT / 2 + boundaryBuffer, FIELD_HEIGHT - boundaryBuffer);
            }

            player.Position = newPosition;
            await Clients.All.SendAsync("PlayerMoved", player);

            // Check for ball collision
            await CheckBallCollision(player);
        }

        private async Task CheckBallCollision(Player player)
        {
            var ball = _gameState.Ball;
            var distance = Vector2.Distance(player.Position, ball.Position);
            var collisionDistance = 1.5f; // Increased collision distance for better gameplay
            
            if (distance < collisionDistance)
            {
                // Calculate ball velocity based on player's movement and position
                var direction = ball.Position - player.Position;
                if (direction != Vector2.Zero)
                {
                    direction = Vector2.Normalize(direction);
                    var speed = player.Velocity.Length() * BALL_SPEED;
                    ball.Velocity = direction * speed;
                    await Clients.All.SendAsync("BallMoved", ball);
                }
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

            // Ball collision with top and bottom walls
            if (newPosition.Y <= 0 || newPosition.Y >= FIELD_HEIGHT)
            {
                newVelocity.Y *= -0.8f; // Add some energy loss
                newPosition.Y = Math.Clamp(newPosition.Y, 0, FIELD_HEIGHT);
            }

            // Check for goals (left and right walls)
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
                newVelocity.X *= -0.8f; // Add some energy loss
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
                    FIELD_WIDTH / 2f,
                    player.Side == PlayerSide.Left ? 2f : FIELD_HEIGHT - 2f
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
