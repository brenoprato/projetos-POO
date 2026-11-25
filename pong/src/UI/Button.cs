using System.Numerics;
using Raylib_cs;

namespace Pong.UI;

public class Button
{
    public Rectangle Bounds { get; }
    public string Text { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Color BaseColor { get; set; }

    public Button(float x, float y, float width, float height, string text, Color baseColor)
    {
        Bounds = new Rectangle(x, y, width, height);
        Text = text;
        BaseColor = baseColor;
    }

    public bool IsHovered()
    {
        return IsEnabled && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
    }

    public bool IsClicked()
    {
        return IsHovered() && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw(Font font)
    {
        Color fill = !IsEnabled
            ? new Color(55, 62, 74, 180)
            : IsHovered()
                ? new Color(
                    (byte)Math.Min(255, BaseColor.R + 28),
                    (byte)Math.Min(255, BaseColor.G + 28),
                    (byte)Math.Min(255, BaseColor.B + 28),
                    (byte)255)
                : BaseColor;

        Color border = !IsEnabled
            ? new Color(90, 98, 112, 180)
            : IsHovered()
                ? new Color(255, 220, 110, 255)
                : new Color(220, 230, 242, 210);

        Raylib.DrawRectangleRounded(Bounds, 0.28f, 8, fill);
        Raylib.DrawRectangleRoundedLinesEx(Bounds, 0.28f, 8, 2f, border);

        const float fontSize = 20f;
        const float spacing = 1f;
        Vector2 textSize = Raylib.MeasureTextEx(font, Text, fontSize, spacing);
        Vector2 textPos = new(
            Bounds.X + (Bounds.Width - textSize.X) / 2f,
            Bounds.Y + (Bounds.Height - textSize.Y) / 2f
        );

        Color textColor = IsEnabled ? Color.White : new Color(150, 158, 170, 200);
        Raylib.DrawTextEx(font, Text, textPos, fontSize, spacing, textColor);
    }
}
