﻿using System;
using System.Drawing;

using CSharpMath.Display;
using CSharpMath.Enumerations;
using CSharpMath.FrontEnd;
using CSharpMath.Atoms;
using CSharpMath.Interfaces;
using TFont = CSharpMath.SkiaSharp.SkiaMathFont;

using Glyph = Typography.OpenFont.Glyph;

using SkiaSharp;
using NColor = SkiaSharp.SKColor;
using NColors = SkiaSharp.SKColors;

namespace CSharpMath.SkiaSharp {
  public readonly struct Thickness {
    public Thickness(float uniformSize) { Left = Right = Top = Bottom = uniformSize; }
    public Thickness(float horizontalSize, float verticalSize) { Left = Right = horizontalSize; Top = Bottom = verticalSize; }
    public Thickness(float left, float top, float right, float bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }

    public float Top { get; }
    public float Bottom { get; }
    public float Left { get; }
    public float Right { get; }

    public void Deconstruct(out float left, out float top, out float right, out float bottom) =>
      (left, top, right, bottom) = (Left, Top, Right, Bottom);
  }
  public class SkiaLatexPainter {
    public SkiaLatexPainter(Action invalidate, SKSize bounds, float fontSize = 20f) {
      Invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
      Bounds = bounds;
      FontSize = fontSize;
    }
    public SkiaLatexPainter(Action invalidate, float width, float height, float fontSize = 20f) {
      Invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
      Bounds = new SKSize(width, height);
      FontSize = fontSize;
    }

    //_field == private field, __field == property-only field
    protected void Redisplay<T>(T assignment) => _displayChanged = true;
    protected bool _displayChanged = false;
    protected MathListDisplay<TFont, Glyph> _displayList;
    protected SkiaGraphicsContext _skiaContext;
    protected static readonly TypesettingContext<TFont, Glyph> _typesettingContext = SkiaTypesetters.LatinMath;

    public Action Invalidate { get; }

    public string ErrorMessage { get; private set; }
    public SKSize Bounds { get; set; }

    Thickness __padding; public Thickness Padding { get => __padding; set => Redisplay(__padding = value); }
    bool __inline = true; public bool DisplayErrorInline { get => __inline; set => Redisplay(__inline = value); }
    /// <summary>
    /// Unit of measure: points
    /// </summary>
    public float FontSize { get => __size; set => Redisplay(__size = value); } float __size = 20f; 
    /// <summary>
    /// Unit of measure: points;
    /// Defaults to <see cref="FontSize"/>.
    /// </summary>
    public float? ErrorFontSize { get => __erf; set => Redisplay(__erf = value); } float? __erf = null; 
    NColor __color = NColors.Black; public NColor TextColor { get => __color; set => Redisplay(__color = value); }
    NColor __back = NColors.Transparent; public NColor BackgroundColor { get => __back; set => Redisplay(__back = value); }
    NColor __error = NColors.Red; public NColor ErrorColor { get => __error; set => Redisplay(__error = value); }
    SkiaTextAlignment __align = SkiaTextAlignment.Centre; public SkiaTextAlignment TextAlignment { get => __align; set => Redisplay(__align = value); }
    SKPaintStyle __style = SKPaintStyle.StrokeAndFill; public SKPaintStyle PaintStyle { get => __style; set => Redisplay(__style = value); }
    bool __boxes; public bool DrawGlyphBoxes { get => __boxes; set => Redisplay(__boxes = value); }

    /// <summary>
    /// Defults to <see cref="null"/>, which signals <see cref="UpdateOrigin"/> to update its value by calculating the text alignment.
    /// </summary>
    public float? OriginX { get; set; }
    /// <summary>
    /// Defults to <see cref="null"/>, which signals <see cref="UpdateOrigin"/> to update its value by calculating the text alignment.
    /// </summary>
    public float? OriginY { get; set; }
    public float Magnification { get; set; } = 1;

    private SKSize ToSKSize(SizeF size) => new SKSize(size.Width, size.Height);
    public SKSize? DrawingSize => _displayList == null ? default(SKSize?) :
      SKSize.Add(ToSKSize(_displayList.ComputeDisplayBounds().Size), new SKSize(Padding.Left + Padding.Right, Padding.Top + Padding.Bottom));

    private IMathList _mathList;
    public IMathList MathList {
      get => _mathList;
      set {
        _mathList = value ?? new MathList();
        _latex = MathListBuilder.MathListToString(_mathList);
        _displayChanged = true;
        Invalidate();
      }
    }

    private string _latex;
    public string LaTeX {
      get => _latex;
      set {
        _latex = value ?? "";
        var buildResult = MathLists.BuildResultFromString(_latex);
        _mathList = buildResult.MathList;
        ErrorMessage = buildResult.Error;
        _displayChanged = true;
        Invalidate();
      }
    }

    public void UpdateOrigin() {
      if (_mathList != null) {
        float displayWidth = _displayList.Width;
        if (OriginX == null) {
          if ((TextAlignment & SkiaTextAlignment.Left) != 0)
            OriginX = Padding.Left;
          else if ((TextAlignment & SkiaTextAlignment.Right) != 0)
            OriginX = Bounds.Width - Padding.Right - displayWidth;
          else
            OriginX = Padding.Left + (Bounds.Width - Padding.Left - Padding.Right - displayWidth) / 2;
        }
        if (OriginY == null) {
          float contentHeight = _displayList.Ascent + _displayList.Descent;
          if (contentHeight < FontSize / 2) {
            contentHeight = FontSize / 2;
          }
          if ((TextAlignment & SkiaTextAlignment.Top) != 0)
            OriginY = Padding.Top;
          else if ((TextAlignment & SkiaTextAlignment.Bottom) != 0)
            OriginY = Bounds.Height - Padding.Bottom - contentHeight;
          else {
            float availableHeight = Bounds.Height - Padding.Top - Padding.Bottom;
            OriginY = ((availableHeight - contentHeight) / 2) + Padding.Top + _displayList.Descent;
          }
        }
      }
    }

    public void Draw(SKCanvas canvas) {
      if (_mathList != null) {
        canvas.Save();
        //invert the canvas vertically
        canvas.Scale(1, -1);
        canvas.Translate(0, -Bounds.Height);
        canvas.Scale(Magnification);
        if (_displayChanged) {
          var fontSize = FontSize;
          var skiaFont = SkiaFontManager.LatinMath(fontSize);
          _displayList = _typesettingContext.CreateLine(_mathList, skiaFont, LineStyle.Display);
          UpdateOrigin();
          _displayList.Position = new PointF(OriginX.Value, OriginY.Value);
          _skiaContext = new SkiaGraphicsContext() {
            DrawGlyphBoxes = DrawGlyphBoxes
          };
          _displayList.Draw(_skiaContext);
          _displayChanged = false;
        } else {
          UpdateOrigin();
          canvas.Translate(OriginX.Value, OriginY.Value);
        }
        canvas.DrawColor(BackgroundColor);
        var paths = _skiaContext.Paths;
        var paint = new SKPaint { IsStroke = true, StrokeCap = SKStrokeCap.Round, Style = PaintStyle };
        foreach (var (path, pos, color) in paths) {
          paint.Color = color ?? TextColor;
          canvas.Save();
          canvas.Translate(pos);
          canvas.DrawPath(path, paint);
          canvas.Restore();
        }
        paint.Style = SKPaintStyle.Stroke;
        var boxPaths = _skiaContext.BoxPaths;
        foreach (var (path, color) in boxPaths) {
          paint.Color = color;
          canvas.DrawPath(path, paint);
        }
        paint.Color = TextColor;
        var lines = _skiaContext.LinePaths;
        foreach (var (from, to, thickness) in lines) {
          paint.StrokeWidth = thickness;
          canvas.DrawLine(from, to, paint);
        }
        canvas.Restore();
      } else if (ErrorMessage.IsNonEmpty()) {
        canvas.Save();
        canvas.DrawColor(BackgroundColor);
        var size = ErrorFontSize ?? FontSize;
        canvas.DrawText(ErrorMessage, 0, size,
          new SKPaint { Color = ErrorColor, Typeface = SKFontManager.Default.MatchCharacter('A'), TextSize = size });
        canvas.Restore();
      }
    }
  }
}