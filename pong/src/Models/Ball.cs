using System.Numerics;

namespace Pong.Models;

public class Ball
{
    private Vector2 _position;
    private Vector2 _velocity;

    public Vector2 Position => _position;
    public Vector2 Velocity => _velocity;
    public float Radius { get; }
    public float BaseSpeed { get; }
    public float MaxSpeed { get; }

    public Ball(Vector2 startPosition, float radius, float baseSpeed, float maxSpeed = 820f)
    {
        _position = startPosition;
        Radius = radius;
        BaseSpeed = baseSpeed;
        MaxSpeed = maxSpeed;
        _velocity = Vector2.Zero;
    }

    public void Update(float deltaTime)
    {
        _position += _velocity * deltaTime;
    }

    // Rebate horizontalmente e ajusta o ângulo vertical conforme o ponto de impacto na raquete (-1 a +1)
    public void ReverseX(float hitFactor)
    {
        float currentSpeed = MathF.Min(_velocity.Length() * 1.05f, MaxSpeed);
        if (currentSpeed < BaseSpeed)
            currentSpeed = BaseSpeed;

        float directionX = _velocity.X < 0 ? 1f : -1f;
        float clampedFactor = Math.Clamp(hitFactor, -1f, 1f);
        float bounceAngle = clampedFactor * (MathF.PI / 3.2f); // Máx ~56 graus

        _velocity = new Vector2(
            directionX * currentSpeed * MathF.Cos(bounceAngle),
            currentSpeed * MathF.Sin(bounceAngle)
        );
    }

    public void ReverseY()
    {
        _velocity = new Vector2(_velocity.X, -_velocity.Y);
    }

    public void SetPositionX(float x)
    {
        _position = new Vector2(x, _position.Y);
    }

    public void SetPositionY(float y)
    {
        _position = new Vector2(_position.X, y);
    }

    public void Reset(Vector2 center, int horizontalDirection)
    {
        _position = center;

        // Define um ângulo inicial suave para não sair em linha perfeitamente reta
        float angle = (Random.Shared.NextSingle() * 0.7f - 0.35f);
        float dirX = horizontalDirection >= 0 ? 1f : -1f;

        _velocity = new Vector2(
            dirX * BaseSpeed * MathF.Cos(angle),
            BaseSpeed * MathF.Sin(angle)
        );
    }
}
