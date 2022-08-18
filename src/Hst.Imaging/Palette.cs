namespace Hst.Imaging
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public class Palette
    {
        public readonly bool IsTransparent;
        public int TransparentColor { get; set; }

        private readonly int maxColors;
        private readonly IList<Color> colors;
        public readonly IReadOnlyList<Color> Colors;

        /// <summary>
        /// Create palette without colors
        /// </summary>
        public Palette()
            : this(0)
        {
        }

        /// <summary>
        /// Create new palette with maximum number of colors allowed to add
        /// </summary>
        public Palette(int maxColors, bool isTransparent = false)
        {
            if (maxColors > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(maxColors), "Palette can maximum have 256 colors");
            }
            
            this.IsTransparent = isTransparent;
            this.maxColors = maxColors;
            this.colors = new List<Color>();
            Colors = new ReadOnlyCollection<Color>(this.colors);
        }
        
        /// <summary>
        /// Create new palette with predefined colors
        /// </summary>
        /// <param name="isTransparent"></param>
        /// <param name="colors"></param>
        public Palette(IEnumerable<Color> colors, bool isTransparent = false)
        {
            this.colors = colors.ToList();
            
            if (this.colors.Count > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(maxColors), "Palette can maximum have 256 colors");
            }
            
            this.IsTransparent = isTransparent;
            this.maxColors = this.colors.Count;
            Colors = new ReadOnlyCollection<Color>(this.colors);
        }

        /// <summary>
        /// Add color from rgba
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddColor(int r, int g, int b, int a = 255)
        {
            AddColor(new Color(r, g, b, a));
        }

        /// <summary>
        /// Add color
        /// </summary>
        /// <param name="color"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void AddColor(Color color)
        {
            if (colors.Count >= maxColors)
            {
                throw new ArgumentOutOfRangeException($"Palette can only have maximum {maxColors} colors");
            }

            this.colors.Add(color);
        }
    }
}