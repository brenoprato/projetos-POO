using System.Numerics;
using Raylib_cs;

namespace Pong.Models;

public class Paddle
{
    private Vector2 _position;

    public Vector2 Position => _position;
    public float Width { get; }
    public float Height { get; }
    public float Speed { get; }

    public Paddle(float x, float y, float width, float height, float speed)
    {
        _position = new Vector2(x, y);
        Width = width;
        Height = height;
        Speed = speed;
    }

    public void MoveUp(float deltaTime, float minY)
    {
        float nextY = _position.Y - Speed * deltaTime;
        _position = new Vector2(_position.X, MathF.Max(minY, nextY));
    }

    public void MoveDown(float deltaTime, float maxY)
    {
        float nextY = _position.Y + Speed * deltaTime;
        _position = new Vector2(_position.X, MathF.Min(maxY - Height, nextY));
    }

    public void Reset(float y)
    {
        _position = new Vector2(_position.X, y);
    }

    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, Width, Height);
    }
}
