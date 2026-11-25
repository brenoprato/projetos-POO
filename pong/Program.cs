using Pong.Game;
using Raylib_cs;

namespace Pong;

public static class Program
{
    public const int ScreenWidth = 1280;
    public const int ScreenHeight = 720;

    public static void Main()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "Pong");
        Raylib.SetTargetFPS(60);

        string fontPath = ResolveAssetPath("font.otf");
        string tablePath = ResolveAssetPath("table_tennis.png");
        string racketPath = ResolveAssetPath("racket.png");
        string ballPath = ResolveAssetPath("ball.png");

        // Fonte vetorial suavizada com suporte a acentuação Latin-1 (português), travessão e setas
        int[] codepoints = [.. Enumerable.Range(32, 224), 0x2014, 0x2191, 0x2193];
        Font customFont = File.Exists(fontPath)
            ? Raylib.LoadFontEx(fontPath, 64, codepoints, codepoints.Length)
            : Raylib.GetFontDefault();
        Raylib.SetTextureFilter(customFont.Texture, TextureFilter.Bilinear);

        // Texturas pixel-art com filtro Point para manter os pixels nítidos
        Texture2D tableTexture = Raylib.LoadTexture(tablePath);
        Texture2D racketTexture = Raylib.LoadTexture(racketPath);
        Texture2D ballTexture = Raylib.LoadTexture(ballPath);
        Raylib.SetTextureFilter(tableTexture, TextureFilter.Point);
        Raylib.SetTextureFilter(racketTexture, TextureFilter.Point);
        Raylib.SetTextureFilter(ballTexture, TextureFilter.Point);

        using (var game = new PongGame(customFont, tableTexture, racketTexture, ballTexture, ScreenWidth, ScreenHeight))
        {
            while (!Raylib.WindowShouldClose())
            {
                game.UpdateUI();

                Raylib.BeginDrawing();
                game.Draw(ScreenWidth, ScreenHeight);
                Raylib.EndDrawing();
            }
        }

        Raylib.UnloadTexture(ballTexture);
        Raylib.UnloadTexture(racketTexture);
        Raylib.UnloadTexture(tableTexture);
        if (File.Exists(fontPath))
            Raylib.UnloadFont(customFont);

        Raylib.CloseWindow();
    }

    private static string ResolveAssetPath(string fileName)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string candidate = Path.Combine(baseDir, "assets", fileName);
        return File.Exists(candidate) ? candidate : Path.Combine("assets", fileName);
    }
}
