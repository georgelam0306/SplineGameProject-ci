using DerpLib.DI;
using DerpLib.Ecs;
using DerpLib.Text;

namespace SplineGame;

public sealed class SplineGameApp : IDisposable
{
    private readonly SplineGameFrameContext _frameContext;
    private readonly SplineAppWorld _appWorld;
    private readonly EcsSystemPipeline<SplineAppWorld> _framePipeline;
    private bool _disposed;

    public SplineGameApp(
        SplineGameFrameContext frameContext,
        SplineAppWorld appWorld,
        [Tag("Frame")] EcsSystemPipeline<SplineAppWorld> framePipeline)
    {
        _frameContext = frameContext;
        _appWorld = appWorld;
        _framePipeline = framePipeline;
    }

    public void Initialize(Font uiFont)
    {
        _frameContext.Initialize(uiFont);
    }

    public void RunFrame()
    {
        _framePipeline.RunFrame(_appWorld);
    }

    public bool TryConsumeRestartRequest()
    {
        return _frameContext.TryConsumeRestartRequest();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frameContext.Dispose();
    }
}
