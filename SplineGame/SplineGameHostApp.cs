using DerpLib.DI;
using DerpLib.Text;
using DerpEngine = DerpLib.Derp;

namespace SplineGame;

internal sealed class SplineGameHostApp : IDisposable
{
    private readonly SplineGameComposition.Factory _gameFactory;
    private readonly int _curveSamplesPerSplineSegment;
    private readonly float _playerCollisionRadius;
    private readonly int _triggerCooldownFrames;

    private SplineGameComposition? _gameScope;
    private SplineGameApp? _game;
    private Font _uiFont = null!;
    private bool _disposed;

    public SplineGameHostApp(
        SplineGameComposition.Factory gameFactory,
        [Arg("curveSamplesPerSplineSegment")] int curveSamplesPerSplineSegment,
        [Arg("playerCollisionRadius")] float playerCollisionRadius,
        [Arg("triggerCooldownFrames")] int triggerCooldownFrames)
    {
        _gameFactory = gameFactory;
        _curveSamplesPerSplineSegment = curveSamplesPerSplineSegment;
        _playerCollisionRadius = playerCollisionRadius;
        _triggerCooldownFrames = triggerCooldownFrames;
    }

    public void Run()
    {
        DerpEngine.InitWindow(1280, 720, "SplineGame");
        DerpEngine.InitSdf();
        _uiFont = DerpEngine.LoadFont("arial");
        DerpEngine.SetSdfFontAtlas(_uiFont.Atlas);

        try
        {
            StartNewGameScope();
            while (!DerpEngine.WindowShouldClose())
            {
                _game!.RunFrame();
                if (_game.TryConsumeRestartRequest())
                {
                    StartNewGameScope();
                }
            }
        }
        finally
        {
            DisposeCurrentGameScope();
            DerpEngine.CloseWindow();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeCurrentGameScope();
    }

    private void StartNewGameScope()
    {
        DisposeCurrentGameScope();
        _gameScope = _gameFactory.Create(
            _curveSamplesPerSplineSegment,
            _playerCollisionRadius,
            _triggerCooldownFrames);
        _game = _gameScope.App;
        _game.Initialize(_uiFont);
    }

    private void DisposeCurrentGameScope()
    {
        _game = null;

        if (_gameScope == null)
        {
            return;
        }

        _gameScope.Dispose();
        _gameScope = null;
    }
}
