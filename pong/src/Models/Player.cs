using Raylib_cs;

namespace Pong.Models;

public class Player
{
    public string Name { get; }
    public int Score { get; private set; }
    public Paddle Paddle { get; }
    public KeyboardKey UpKey { get; }
    public KeyboardKey DownKey { get; }

    public Player(string name, Paddle paddle, KeyboardKey upKey, KeyboardKey downKey)
    {
        Name = name;
        Paddle = paddle;
        UpKey = upKey;
        DownKey = downKey;
        Score = 0;
    }

    public void UpdateInput(float deltaTime, float minY, float maxY)
    {
        if (Raylib.IsKeyDown(UpKey))
            Paddle.MoveUp(deltaTime, minY);

        if (Raylib.IsKeyDown(DownKey))
            Paddle.MoveDown(deltaTime, maxY);
    }

    public void AddPoint()
    {
        Score++;
    }

    public void ResetScore()
    {
        Score = 0;
    }
}
