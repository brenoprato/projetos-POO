using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Pong.Models;
using Pong.UI;
using Raylib_cs;

namespace Pong.Game;

public enum GameState
{
    NotStarted,
    Running,
    Paused,
    GameOver
}

public sealed class PongGame : IDisposable
{
    public const int WinningScore = 5;

    private readonly Font _font;
    private readonly Texture2D _tableTexture;
    private readonly Texture2D _racketTexture;
    private readonly Texture2D _ballTexture;

    private readonly Ball _ball;
    private readonly Player _player1;
    private readonly Player _player2;

    private readonly Button _btnStart;
    private readonly Button _btnPause;
    private readonly Button _btnRestart;

    // Geometria da mesa e da área de ação (com espaço lateral para as raquetes fora da mesa)
    private readonly Rectangle _tableDestRect;
    private readonly float _playMinY;
    private readonly float _playMaxY;
    private readonly float _scoreLeftX;
    private readonly float _scoreRightX;
    private readonly Vector2 _tableCenter;

    // Sincronização e controle da Thread de processamento do jogo
    private readonly Thread _gameThread;
    private readonly ManualResetEventSlim _pauseEvent;
    private readonly CancellationTokenSource _cts;
    private readonly object _stateLock = new();
    private readonly ConcurrentQueue<Action> _uiActions = new();

    private GameState _state = GameState.NotStarted;
    private string _statusMessage = "Clique em 'Iniciar Jogo' ou pressione ESPAÇO para começar";
    private bool _disposed;

    public GameState State
    {
        get { lock (_stateLock) return _state; }
    }

    public PongGame(
        Font font,
        Texture2D tableTexture,
        Texture2D racketTexture,
        Texture2D ballTexture,
        int screenWidth,
        int screenHeight)
    {
        _font = font;
        _tableTexture = tableTexture;
        _racketTexture = racketTexture;
        _ballTexture = ballTexture;

        // Mesa centralizada (escala 6x do viewBox 140x84 -> 840x504)
        const float tableScale = 6.0f;
        float tableWidth = 140f * tableScale;
        float tableHeight = 84f * tableScale;
        float tableX = (screenWidth - tableWidth) / 2f;
        float tableY = 148f;
        _tableDestRect = new Rectangle(tableX, tableY, tableWidth, tableHeight);

        // Limites reais da superfície da mesa dentro do SVG (x: 2..138 borda externa, y: 6..78 área útil)
        float tableLeftEdge = tableX + 2f * tableScale;
        float tableRightEdge = tableX + 138f * tableScale;
        _playMinY = tableY + 6f * tableScale;
        _playMaxY = tableY + 78f * tableScale;
        _tableCenter = new Vector2(tableX + tableWidth / 2f, (_playMinY + _playMaxY) / 2f);

        // Raquetes posicionadas fora da mesa com espaçamento lateral de 26px
        const float paddleWidth = 27f;
        const float paddleHeight = 90f;
        const float paddleSpeed = 460f;
        const float lateralGap = 26f;

        float paddleStartY = _tableCenter.Y - paddleHeight / 2f;
        float paddle1X = tableLeftEdge - lateralGap - paddleWidth;
        float paddle2X = tableRightEdge + lateralGap;

        _scoreLeftX = paddle1X - 28f;
        _scoreRightX = paddle2X + paddleWidth + 28f;

        var paddle1 = new Paddle(paddle1X, paddleStartY, paddleWidth, paddleHeight, paddleSpeed);
        var paddle2 = new Paddle(paddle2X, paddleStartY, paddleWidth, paddleHeight, paddleSpeed);

        _player1 = new Player("Jogador 1", paddle1, KeyboardKey.W, KeyboardKey.S);
        _player2 = new Player("Jogador 2", paddle2, KeyboardKey.Up, KeyboardKey.Down);

        _ball = new Ball(_tableCenter, radius: 12f, baseSpeed: 440f, maxSpeed: 820f);
        _ball.Reset(_tableCenter, horizontalDirection: 1);

        // Botões de controle no painel superior
        const float btnWidth = 152f;
        const float btnHeight = 40f;
        const float btnGap = 14f;
        float totalBtnsWidth = btnWidth * 3f + btnGap * 2f;
        float btnStartX = (screenWidth - totalBtnsWidth) / 2f;
        const float btnY = 54f;

        _btnStart = new Button(btnStartX, btnY, btnWidth, btnHeight, "Iniciar Jogo", new Color(40, 138, 82, 255));
        _btnPause = new Button(btnStartX + btnWidth + btnGap, btnY, btnWidth, btnHeight, "Pausar Jogo", new Color(186, 122, 32, 255));
        _btnRestart = new Button(btnStartX + (btnWidth + btnGap) * 2f, btnY, btnWidth, btnHeight, "Reiniciar Jogo", new Color(168, 52, 52, 255));
        SyncButtonStates();

        // Inicializa primitivas de sincronização e inicia a Thread dedicada de física/lógica
        _pauseEvent = new ManualResetEventSlim(false);
        _cts = new CancellationTokenSource();
        _gameThread = new Thread(GameLoopThread)
        {
            Name = "PongPhysicsThread",
            IsBackground = true
        };
        _gameThread.Start();
    }

    public void StartGame()
    {
        lock (_stateLock)
        {
            if (_state == GameState.GameOver)
            {
                ResetMatchStateLocked();
            }

            if (_state != GameState.Running)
            {
                _state = GameState.Running;
                _statusMessage = string.Empty;
                SyncButtonStates();
                _pauseEvent.Set();
            }
        }
    }

    public void PauseGame()
    {
        lock (_stateLock)
        {
            if (_state == GameState.Running)
            {
                _state = GameState.Paused;
                _statusMessage = "JOGO PAUSADO — Clique em 'Iniciar Jogo' para continuar";
                _pauseEvent.Reset();
                SyncButtonStates();
            }
        }
    }

    public void RestartGame()
    {
        lock (_stateLock)
        {
            ResetMatchStateLocked();
            _state = GameState.Running;
            _statusMessage = string.Empty;
            SyncButtonStates();
            _pauseEvent.Set();
        }
    }

    private void ResetMatchStateLocked()
    {
        _player1.ResetScore();
        _player2.ResetScore();

        float startY = _tableCenter.Y - _player1.Paddle.Height / 2f;
        _player1.Paddle.Reset(startY);
        _player2.Paddle.Reset(startY);

        int initialDir = Random.Shared.Next(0, 2) == 0 ? -1 : 1;
        _ball.Reset(_tableCenter, initialDir);
    }

    private void SyncButtonStates()
    {
        _btnStart.IsEnabled = _state != GameState.Running;
        _btnStart.Text = _state == GameState.Paused ? "Retomar Jogo" : "Iniciar Jogo";
        _btnPause.IsEnabled = _state == GameState.Running;
        _btnRestart.IsEnabled = true;
    }

    // Despacha ações da thread de processamento para execução na thread principal da UI (equivalente a BeginInvoke)
    private void InvokeOnUiThread(Action action)
    {
        _uiActions.Enqueue(action);
    }

    public void UpdateUI()
    {
        while (_uiActions.TryDequeue(out Action? uiAction))
        {
            uiAction.Invoke();
        }

        if (_btnStart.IsClicked())
            StartGame();
        else if (_btnPause.IsClicked())
            PauseGame();
        else if (_btnRestart.IsClicked())
            RestartGame();

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (State == GameState.Running)
                PauseGame();
            else
                StartGame();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            RestartGame();
        }
    }

    // Loop contínuo executado na Thread secundária (_gameThread) a ~120 Hz
    private void GameLoopThread()
    {
        var stopwatch = Stopwatch.StartNew();
        long previousTicks = stopwatch.ElapsedTicks;

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // Bloqueia a thread sem gastar CPU enquanto o jogo estiver pausado ou não iniciado
                _pauseEvent.Wait(_cts.Token);

                long currentTicks = stopwatch.ElapsedTicks;
                float deltaTime = (float)(currentTicks - previousTicks) / Stopwatch.Frequency;
                previousTicks = currentTicks;

                // Evita saltos bruscos de tempo logo após retomar de uma pausa
                if (deltaTime <= 0f || deltaTime > 0.05f)
                    deltaTime = 1f / 120f;

                lock (_stateLock)
                {
                    if (_state == GameState.Running)
                    {
                        UpdatePhysicsLocked(deltaTime);
                    }
                }

                Thread.Sleep(8);
            }
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal solicitado pelo CancellationToken ao fechar a janela
        }
    }

    private void UpdatePhysicsLocked(float deltaTime)
    {
        _player1.UpdateInput(deltaTime, _playMinY, _playMaxY);
        _player2.UpdateInput(deltaTime, _playMinY, _playMaxY);
        _ball.Update(deltaTime);

        CheckCollisionsLocked();
        CheckScoringLocked();
    }

    private void CheckCollisionsLocked()
    {
        // Colisão com as bordas superior e inferior da mesa
        if (_ball.Position.Y - _ball.Radius <= _playMinY)
        {
            _ball.SetPositionY(_playMinY + _ball.Radius);
            if (_ball.Velocity.Y < 0)
                _ball.ReverseY();
        }
        else if (_ball.Position.Y + _ball.Radius >= _playMaxY)
        {
            _ball.SetPositionY(_playMaxY - _ball.Radius);
            if (_ball.Velocity.Y > 0)
                _ball.ReverseY();
        }

        // Colisão com a raquete do Jogador 1 (esquerda)
        Rectangle p1Bounds = _player1.Paddle.GetBounds();
        if (_ball.Velocity.X < 0 &&
            Raylib.CheckCollisionCircleRec(_ball.Position, _ball.Radius, p1Bounds))
        {
            _ball.SetPositionX(p1Bounds.X + p1Bounds.Width + _ball.Radius);
            float paddleCenterY = p1Bounds.Y + p1Bounds.Height / 2f;
            float hitFactor = (_ball.Position.Y - paddleCenterY) / (p1Bounds.Height / 2f);
            _ball.ReverseX(hitFactor);
        }

        // Colisão com a raquete do Jogador 2 (direita)
        Rectangle p2Bounds = _player2.Paddle.GetBounds();
        if (_ball.Velocity.X > 0 &&
            Raylib.CheckCollisionCircleRec(_ball.Position, _ball.Radius, p2Bounds))
        {
            _ball.SetPositionX(p2Bounds.X - _ball.Radius);
            float paddleCenterY = p2Bounds.Y + p2Bounds.Height / 2f;
            float hitFactor = (_ball.Position.Y - paddleCenterY) / (p2Bounds.Height / 2f);
            _ball.ReverseX(hitFactor);
        }
    }

    private void CheckScoringLocked()
    {
        // Bola ultrapassou a raquete do Jogador 1 -> Ponto para o Jogador 2
        if (_ball.Position.X + _ball.Radius < _scoreLeftX)
        {
            _player2.AddPoint();
            HandlePointScoredLocked(_player2, nextDirection: 1);
        }
        // Bola ultrapassou a raquete do Jogador 2 -> Ponto para o Jogador 1
        else if (_ball.Position.X - _ball.Radius > _scoreRightX)
        {
            _player1.AddPoint();
            HandlePointScoredLocked(_player1, nextDirection: -1);
        }
    }

    private void HandlePointScoredLocked(Player scorer, int nextDirection)
    {
        if (scorer.Score >= WinningScore)
        {
            _state = GameState.GameOver;
            _pauseEvent.Reset();
            _ball.Reset(_tableCenter, nextDirection);

            string winnerName = scorer.Name;
            int s1 = _player1.Score;
            int s2 = _player2.Score;
            InvokeOnUiThread(() =>
            {
                lock (_stateLock)
                {
                    _statusMessage = $"FIM DE JOGO! {winnerName.ToUpper()} VENCEU A PARTIDA ({s1} x {s2})!";
                    SyncButtonStates();
                }
            });
        }
        else
        {
            _ball.Reset(_tableCenter, nextDirection);
        }
    }

    public void Draw(int screenWidth, int screenHeight)
    {
        // Lê um snapshot consistente do estado compartilhado com a thread de física
        Vector2 ballPos;
        float ballRadius;
        Rectangle p1Bounds;
        Rectangle p2Bounds;
        int score1;
        int score2;
        string statusMsg;

        lock (_stateLock)
        {
            ballPos = _ball.Position;
            ballRadius = _ball.Radius;
            p1Bounds = _player1.Paddle.GetBounds();
            p2Bounds = _player2.Paddle.GetBounds();
            score1 = _player1.Score;
            score2 = _player2.Score;
            statusMsg = _statusMessage;
        }

        // Fundo da arena (piso escuro ao redor da mesa de tênis de mesa)
        Raylib.ClearBackground(new Color(22, 28, 38, 255));

        DrawHeaderBar(screenWidth, score1, score2);
        DrawArenaAndEntities(ballPos, ballRadius, p1Bounds, p2Bounds);
        DrawFooterAndOverlays(screenWidth, screenHeight, statusMsg);
    }

    private void DrawHeaderBar(int screenWidth, int score1, int score2)
    {
        Raylib.DrawRectangle(0, 0, screenWidth, 112, new Color(15, 20, 28, 255));
        Raylib.DrawLineEx(new Vector2(0, 112), new Vector2(screenWidth, 112), 2f, new Color(46, 58, 78, 255));

        DrawCenteredText("PONG", screenWidth / 2f, 12f, 26f, new Color(215, 225, 240, 255));

        // Placar Jogador 1 (Esquerda)
        Raylib.DrawRectangleRounded(new Rectangle(36, 18, 280, 76), 0.22f, 8, new Color(26, 34, 48, 255));
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(36, 18, 280, 76), 0.22f, 8, 2f, new Color(210, 65, 65, 200));
        Raylib.DrawTextEx(_font, "JOGADOR 1", new Vector2(54, 26), 22f, 1f, new Color(255, 120, 120, 255));
        Raylib.DrawTextEx(_font, "Teclas: W / S", new Vector2(54, 54), 18f, 1f, new Color(170, 182, 198, 255));

        string s1Text = score1.ToString();
        Vector2 s1Size = Raylib.MeasureTextEx(_font, s1Text, 52f, 1f);
        Raylib.DrawTextEx(_font, s1Text, new Vector2(292 - s1Size.X, 28), 52f, 1f, Color.White);

        // Placar Jogador 2 (Direita)
        Raylib.DrawRectangleRounded(new Rectangle(screenWidth - 316, 18, 280, 76), 0.22f, 8, new Color(26, 34, 48, 255));
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(screenWidth - 316, 18, 280, 76), 0.22f, 8, 2f, new Color(70, 150, 240, 200));
        Raylib.DrawTextEx(_font, "JOGADOR 2", new Vector2(screenWidth - 220, 26), 22f, 1f, new Color(115, 185, 255, 255));
        Raylib.DrawTextEx(_font, "Teclas: ↑ / ↓", new Vector2(screenWidth - 220, 54), 18f, 1f, new Color(170, 182, 198, 255));

        string s2Text = score2.ToString();
        Raylib.DrawTextEx(_font, s2Text, new Vector2(screenWidth - 292, 28), 52f, 1f, Color.White);

        _btnStart.Draw(_font);
        _btnPause.Draw(_font);
        _btnRestart.Draw(_font);
    }

    private void DrawArenaAndEntities(Vector2 ballPos, float ballRadius, Rectangle p1Bounds, Rectangle p2Bounds)
    {
        // Zonas laterais sutis indicando a área de movimentação das raquetes fora da mesa
        float zoneHeight = _playMaxY - _playMinY;
        Raylib.DrawRectangleRounded(
            new Rectangle(p1Bounds.X - 6f, _playMinY, p1Bounds.Width + 12f, zoneHeight),
            0.3f, 6, new Color(255, 255, 255, 12));
        Raylib.DrawRectangleRounded(
            new Rectangle(p2Bounds.X - 6f, _playMinY, p2Bounds.Width + 12f, zoneHeight),
            0.3f, 6, new Color(255, 255, 255, 12));

        // Sombra sob a mesa
        Raylib.DrawRectangleRounded(
            new Rectangle(_tableDestRect.X + 12f, _tableDestRect.Y + 26f, _tableDestRect.Width - 24f, _tableDestRect.Height - 40f),
            0.03f, 6, new Color(0, 0, 0, 95));

        // Desenha a mesa (table_tennis.png: 700x420)
        Rectangle tableSrc = new(0, 0, _tableTexture.Width, _tableTexture.Height);
        Raylib.DrawTexturePro(_tableTexture, tableSrc, _tableDestRect, Vector2.Zero, 0f, Color.White);

        // Desenha as raquetes fora da mesa usando a área visível exata (x: 5..35, y: 0..100 no PNG 40x100)
        Rectangle racket1Src = new(5f, 0f, 30f, 100f);
        Rectangle racket2Src = new(5f, 0f, -30f, 100f); // Espelhada para a borracha vermelha ficar voltada para a mesa
        Raylib.DrawTexturePro(_racketTexture, racket1Src, p1Bounds, Vector2.Zero, 0f, Color.White);
        Raylib.DrawTexturePro(_racketTexture, racket2Src, p2Bounds, Vector2.Zero, 0f, Color.White);

        // Sombra sutil e desenho da bola usando a área visível exata (x: 4..36, y: 4..36 no PNG 40x40)
        Raylib.DrawCircleV(ballPos + new Vector2(3f, 4f), ballRadius, new Color(0, 0, 0, 85));
        Rectangle ballSrc = new(4f, 4f, 32f, 32f);
        Rectangle ballDest = new(ballPos.X - ballRadius, ballPos.Y - ballRadius, ballRadius * 2f, ballRadius * 2f);
        Raylib.DrawTexturePro(_ballTexture, ballSrc, ballDest, Vector2.Zero, 0f, Color.White);
    }

    private void DrawFooterAndOverlays(int screenWidth, int screenHeight, string statusMsg)
    {
        if (!string.IsNullOrEmpty(statusMsg))
        {
            const float fontSize = 24f;
            Vector2 msgSize = Raylib.MeasureTextEx(_font, statusMsg, fontSize, 1f);
            float boxWidth = msgSize.X + 48f;
            float boxHeight = 54f;
            Rectangle bannerRect = new(
                (screenWidth - boxWidth) / 2f,
                _tableCenter.Y - boxHeight / 2f,
                boxWidth,
                boxHeight);

            Raylib.DrawRectangleRounded(bannerRect, 0.3f, 8, new Color(12, 16, 24, 230));
            Raylib.DrawRectangleRoundedLinesEx(bannerRect, 0.3f, 8, 2f, new Color(255, 205, 85, 230));
            DrawCenteredText(statusMsg, screenWidth / 2f, bannerRect.Y + (boxHeight - msgSize.Y) / 2f, fontSize, new Color(255, 230, 140, 255));
        }

        string rulesInfo = $"Meta da partida: {WinningScore} pontos  |  [ESPAÇO] Iniciar/Pausar  |  [R] Reiniciar";
        DrawCenteredText(rulesInfo, screenWidth / 2f, screenHeight - 32f, 18f, new Color(165, 178, 196, 255));
    }

    private void DrawCenteredText(string text, float centerX, float y, float fontSize, Color color)
    {
        Vector2 size = Raylib.MeasureTextEx(_font, text, fontSize, 1f);
        Raylib.DrawTextEx(_font, text, new Vector2(centerX - size.X / 2f, y), fontSize, 1f, color);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _pauseEvent.Set(); // Libera a thread caso esteja aguardando no Wait()

        if (_gameThread.IsAlive)
        {
            _gameThread.Join(TimeSpan.FromSeconds(2));
        }

        _pauseEvent.Dispose();
        _cts.Dispose();
    }
}
