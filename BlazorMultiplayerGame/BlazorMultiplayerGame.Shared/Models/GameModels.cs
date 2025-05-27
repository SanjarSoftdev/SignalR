using System.Numerics;

namespace BlazorMultiplayerGame.Shared.Models
{
    public class Player
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public PlayerSide Side { get; set; }
        public int Score { get; set; }
    }

    public class Ball
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; } = 0.5f;
    }

    public enum PlayerSide
    {
        Left,
        Right
    }

    public class GameState
    {
        public Dictionary<string, Player> Players { get; set; } = new();
        public Ball Ball { get; set; } = new Ball();
        public bool IsGameStarted { get; set; }
        public int WinningScore { get; set; } = 3;
        public string? Winner { get; set; }
    }
} 