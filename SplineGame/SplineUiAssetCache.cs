using System.Numerics;
using Derp.UI;
using DerpLib.AssetPipeline;
using DerpLib.Rendering;
using DerpLib.Text;

namespace SplineGame;

public sealed class SplineUiAssetCache : IDisposable
{
    private const float PrefabHalfScale = 0.5f;
    private const float PrefabQuarterScale = 0.25f;
    private const float AdaptiveHalfPreviewScaleThreshold = 0.6f;
    private const float AdaptiveQuarterPreviewScaleThreshold = 0.3f;

    private readonly ContentManager _contentManager;
    private readonly Font _font;
    private readonly List<CachedAsset> _cachedAssets = new(16);

    public SplineUiAssetCache(ContentManager contentManager, Font font)
    {
        _contentManager = contentManager;
        _font = font;

        UiRuntimeContent.Register(_contentManager);
    }

    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        for (int assetIndex = 0; assetIndex < _cachedAssets.Count; assetIndex++)
        {
            _cachedAssets[assetIndex].DisposeSurfaces();
        }

        _cachedAssets.Clear();
    }

    public void Preload(string uiAssetPath)
    {
        if (string.IsNullOrWhiteSpace(uiAssetPath))
        {
            return;
        }

        for (int assetIndex = 0; assetIndex < _cachedAssets.Count; assetIndex++)
        {
            if (string.Equals(_cachedAssets[assetIndex].AssetPath, uiAssetPath, StringComparison.Ordinal))
            {
                return;
            }
        }

        _cachedAssets.Add(new CachedAsset(uiAssetPath));
    }

    public bool TryGetVisual(string uiAssetPath, float drawScale, out UiAssetVisual visual)
    {
        visual = default;
        if (string.IsNullOrWhiteSpace(uiAssetPath))
        {
            return false;
        }

        PreviewRenderMode renderMode = ResolveAdaptiveRenderMode(drawScale);
        for (int assetIndex = 0; assetIndex < _cachedAssets.Count; assetIndex++)
        {
            CachedAsset cachedAsset = _cachedAssets[assetIndex];
            if (!string.Equals(cachedAsset.AssetPath, uiAssetPath, StringComparison.Ordinal))
            {
                continue;
            }

            return TryBuildVisualFromCache(cachedAsset, renderMode, out visual);
        }

        var newAsset = new CachedAsset(uiAssetPath);
        _cachedAssets.Add(newAsset);
        return TryBuildVisualFromCache(newAsset, renderMode, out visual);
    }

    private bool TryBuildVisualFromCache(CachedAsset cachedAsset, PreviewRenderMode renderMode, out UiAssetVisual visual)
    {
        visual = default;
        if (cachedAsset.TryGetSurface(renderMode, out CanvasSurface? cachedSurface) &&
            cachedSurface != null)
        {
            visual = new UiAssetVisual(cachedSurface.Texture, cachedAsset.PrefabSize);
            return true;
        }

        if (!TryLoadAsset(cachedAsset, renderMode))
        {
            return false;
        }

        if (!cachedAsset.TryGetSurface(renderMode, out CanvasSurface? loadedSurface) ||
            loadedSurface == null)
        {
            return false;
        }

        visual = new UiAssetVisual(loadedSurface.Texture, cachedAsset.PrefabSize);
        return true;
    }

    private bool TryLoadAsset(CachedAsset cachedAsset, PreviewRenderMode renderMode)
    {
        CanvasSurface? surface = null;
        try
        {
            CompiledUi compiledUi = _contentManager.Load<CompiledUi>(cachedAsset.AssetPath);
            var runtime = new UiRuntime();
            runtime.SetFont(_font);
            runtime.Load(compiledUi);

            Vector2 prefabSize = new(64f, 64f);
            if (runtime.TryGetActivePrefabCanvasSize(out Vector2 activePrefabCanvasSize) &&
                activePrefabCanvasSize.X > 0f &&
                activePrefabCanvasSize.Y > 0f)
            {
                prefabSize = activePrefabCanvasSize;
            }

            float renderScale = ResolveRenderScale(renderMode);
            int renderWidth = Math.Max(1, (int)MathF.Ceiling(prefabSize.X * renderScale));
            int renderHeight = Math.Max(1, (int)MathF.Ceiling(prefabSize.Y * renderScale));

            if (renderMode == PreviewRenderMode.PrefabSize)
            {
                _ = runtime.TrySetActivePrefabCanvasSize(renderWidth, renderHeight, resolveLayout: true);
                _ = runtime.TryAutoFitActivePrefabToCanvas(
                    renderWidth,
                    renderHeight,
                    paddingFraction: 0f,
                    minZoom: 1f,
                    maxZoom: 1f);
            }
            else
            {
                _ = runtime.TryAutoFitActivePrefabToCanvas(
                    renderWidth,
                    renderHeight,
                    paddingFraction: 0f);
            }

            runtime.Tick(0u, new UiPointerFrameInput(
                pointerValid: false,
                pointerWorld: default,
                primaryDown: false,
                wheelDelta: 0f,
                hoveredStableId: 0));

            surface = new CanvasSurface();
            surface.SetFontAtlas(_font.Atlas);
            runtime.BuildFrame(surface, renderWidth, renderHeight);
            if (surface.Buffer.Count <= 0)
            {
                surface.Dispose();
                return false;
            }

            surface.DispatchToTexture();

            Texture texture = surface.Texture;
            if (texture.Width <= 0 || texture.Height <= 0)
            {
                surface.Dispose();
                return false;
            }

            cachedAsset.SetSurface(renderMode, surface);
            cachedAsset.PrefabSize = prefabSize;
            return true;
        }
        catch
        {
            surface?.Dispose();
            return false;
        }
    }

    private static PreviewRenderMode ResolveAdaptiveRenderMode(float drawScale)
    {
        if (!float.IsFinite(drawScale) || drawScale <= 0f)
        {
            return PreviewRenderMode.PrefabSize;
        }

        if (drawScale <= AdaptiveQuarterPreviewScaleThreshold)
        {
            return PreviewRenderMode.PrefabQuarter;
        }

        if (drawScale <= AdaptiveHalfPreviewScaleThreshold)
        {
            return PreviewRenderMode.PrefabHalf;
        }

        return PreviewRenderMode.PrefabSize;
    }

    private static float ResolveRenderScale(PreviewRenderMode renderMode)
    {
        return renderMode switch
        {
            PreviewRenderMode.PrefabHalf => PrefabHalfScale,
            PreviewRenderMode.PrefabQuarter => PrefabQuarterScale,
            _ => 1f,
        };
    }

    public readonly struct UiAssetVisual
    {
        public UiAssetVisual(Texture texture, Vector2 prefabSize)
        {
            Texture = texture;
            PrefabSize = prefabSize;
        }

        public Texture Texture { get; }
        public Vector2 PrefabSize { get; }
    }

    private sealed class CachedAsset
    {
        public CachedAsset(string assetPath)
        {
            AssetPath = assetPath;
            PrefabSize = Vector2.Zero;
        }

        public string AssetPath { get; }
        public CanvasSurface? PrefabSurface { get; set; }
        public CanvasSurface? HalfSurface { get; set; }
        public CanvasSurface? QuarterSurface { get; set; }
        public Vector2 PrefabSize { get; set; }

        public bool TryGetSurface(PreviewRenderMode renderMode, out CanvasSurface? surface)
        {
            surface = renderMode switch
            {
                PreviewRenderMode.PrefabHalf => HalfSurface,
                PreviewRenderMode.PrefabQuarter => QuarterSurface,
                _ => PrefabSurface,
            };

            return surface != null;
        }

        public void SetSurface(PreviewRenderMode renderMode, CanvasSurface surface)
        {
            switch (renderMode)
            {
                case PreviewRenderMode.PrefabHalf:
                {
                    HalfSurface?.Dispose();
                    HalfSurface = surface;
                    break;
                }
                case PreviewRenderMode.PrefabQuarter:
                {
                    QuarterSurface?.Dispose();
                    QuarterSurface = surface;
                    break;
                }
                default:
                {
                    PrefabSurface?.Dispose();
                    PrefabSurface = surface;
                    break;
                }
            }
        }

        public void DisposeSurfaces()
        {
            PrefabSurface?.Dispose();
            PrefabSurface = null;

            HalfSurface?.Dispose();
            HalfSurface = null;

            QuarterSurface?.Dispose();
            QuarterSurface = null;
        }
    }

    private enum PreviewRenderMode
    {
        PrefabSize,
        PrefabHalf,
        PrefabQuarter,
    }
}
