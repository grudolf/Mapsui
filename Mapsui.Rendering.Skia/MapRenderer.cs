using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Providers;
using Mapsui.Styles;
using SkiaSharp;

namespace Mapsui.Rendering.Skia
{
    public class MapRenderer : IRenderer
    {
        private const int TilesToKeepMultiplier = 3;
        private readonly IDictionary<int, SKBitmapInfo> _symbolTextureCache = new Dictionary<int, SKBitmapInfo>();

        private readonly IDictionary<object, SKBitmapInfo> _tileTextureCache =
            new Dictionary<object, SKBitmapInfo>(new IdentityComparer<object>());

        private long _currentIteration;

        static MapRenderer()
        {
            DefaultRendererFactory.Create = () => new MapRenderer();
        }

        public void Render(object target, IViewport viewport, IEnumerable<ILayer> layers, Color background = null)
        {
            Render((SKCanvas) target, viewport, layers, background);
        }

        public MemoryStream RenderToBitmapStream(IViewport viewport, IEnumerable<ILayer> layers, Color background = null)
        {
            try
            {
                // todo: Use SKColorType.Rgba8888 when it does not crash anymore
                using (
                    var bitmap = new SKBitmap((int) viewport.Width, (int) viewport.Height, SKColorType.Rgb565,
                        SKAlphaType.Premul))
                {
                    using (var canvas = new SKCanvas(bitmap))
                    {
                        Render(canvas, viewport, layers, background);
                        using (var image = SKImage.FromBitmap(bitmap))
                        {
                            using (var data = image.Encode())
                            {
                                var memoryStream = new MemoryStream();
                                data.SaveTo(memoryStream);
                                return memoryStream;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex.Message);
                return null;
            }
        }

        private void Render(SKCanvas canvas, IViewport viewport, IEnumerable<ILayer> layers,
            Color background)
        {
            if (background != null)
            {
                var c = new SKColor((byte) background.R, (byte) background.G, (byte) background.B, (byte) background.A);
                canvas.Clear(c);
            }
            else
            {
               // canvas.Clear(Color.White.ToSkia());
                canvas.Clear(new SKColor(255, 255, 255));
            }

            layers = layers.ToList();

            SetAllTextureInfosToUnused();

            VisibleFeatureIterator.IterateLayers(viewport, layers, (v, l, s) => { RenderFeature(canvas, v, l, s); });

            RemoveUnusedTextureInfos();

            _currentIteration++;
        }

        public void DeleteAllBoundTextures()
        {
            DeleteAllTileTextures();
            DeleteAllSymbolTextures();
        }

        private void DeleteAllSymbolTextures()
        {
            foreach (var key in _symbolTextureCache.Keys)
                _symbolTextureCache[key].Bitmap.Dispose();
            _symbolTextureCache.Clear();
        }

        private void DeleteAllTileTextures()
        {
            foreach (var key in _tileTextureCache.Keys)
                _tileTextureCache[key].Bitmap.Dispose();
            _tileTextureCache.Clear();
        }

        private void RemoveUnusedTextureInfos()
        {
            var numberOfTilesUsedInCurrentIteration =
                _tileTextureCache.Values.Count(i => i.IterationUsed == _currentIteration);

            var orderedKeys = _tileTextureCache.OrderBy(kvp => kvp.Value.IterationUsed).Select(kvp => kvp.Key).ToList();

            var counter = 0;
            var tilesToKeep = orderedKeys.Count*TilesToKeepMultiplier;
            var numberToRemove = numberOfTilesUsedInCurrentIteration - tilesToKeep;
            foreach (var key in orderedKeys)
            {
                if (counter > numberToRemove)
                    break;
                var textureInfo = _tileTextureCache[key];
                _tileTextureCache.Remove(key);
                textureInfo.Bitmap.Dispose();
                counter++;
            }
        }

        private void SetAllTextureInfosToUnused()
        {
            foreach (var key in _tileTextureCache.Keys.ToList())
            {
                var textureInfo = _tileTextureCache[key];
                textureInfo.IterationUsed = _currentIteration;
                _tileTextureCache[key] = textureInfo;
            }
        }

        private void RenderFeature(SKCanvas canvas, IViewport viewport, IStyle style, IFeature feature)
        {
            if (feature.Geometry is Point)
                PointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, _symbolTextureCache);
            else if (feature.Geometry is MultiPoint)
                MultiPointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, _symbolTextureCache);
            else if (feature.Geometry is LineString)
                LineStringRenderer.Draw(canvas, viewport, style, feature.Geometry);
            else if (feature.Geometry is MultiLineString)
                MultiLineStringRenderer.Draw(canvas, viewport, style, feature.Geometry);
            else if (feature.Geometry is Polygon)
                PolygonRenderer.Draw(canvas, viewport, style, feature.Geometry);
            else if (feature.Geometry is MultiPolygon)
                MultiPolygonRenderer.Draw(canvas, viewport, style, feature.Geometry);
            else if (feature.Geometry is IRaster)
                RasterRenderer.Draw(canvas, viewport, style, feature, _tileTextureCache, _currentIteration);
        }
    }

    public class IdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T obj, T otherObj)
        {
            return obj == otherObj;
        }

        public int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }
    }
}