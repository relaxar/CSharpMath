﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpMath {
  public static class DisplayEnumerableExtensions {
    public static float CollectionAscent<TGlyph>(this IEnumerable<IDisplay<TGlyph>> displays) {
      float r = 0;
      foreach (var display in displays) {
        r = Math.Max(r, display.Ascent + display.Position.Y);
      }
      return r;
    }
    public static float CollectionDescent<TGlyph>(this IEnumerable<IDisplay<TGlyph>> displays) {
      float r = 0;
      foreach (var display in displays) {
        r = Math.Max(r, display.Descent - display.Position.Y);
      }
      return r;
    }

    public static float CollectionX<TGlyph>(this IEnumerable<IDisplay<TGlyph>> displays)
    {
      float r = 0;
      if (displays.IsNonempty()) {
        r = displays.Min(d => d.Position.X);
      }
      return r;
    }
    public static float CollectionMaxX<TGlyph>(this IEnumerable<IDisplay<TGlyph>> displays)
    {
      float r = 0;
      if (displays.IsNonempty())
      {
        r = displays.Max(d => d.Position.X + d.Width);
      }
      return r;
    }

    public static float CollectionWidth<TGlyph>(this IEnumerable<IDisplay<TGlyph>> displays)
      => displays.CollectionMaxX() - displays.CollectionX();
  }
}
