namespace Hst.Imaging
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public class Palette
    {
        public bool IsTransparent => TransparentColor >= 0 && TransparentColor < maxColors;
        public int TransparentColor { get; set; }

        private readonly int maxColors;
        private readonly IList<Color> colors;
        public readonly IReadOnlyList<Color> Colors;

        /// <summary>
        /// Create palette without colors
        /// </summary>
        public Palette()
            : this(-1)
        {
        }

        /// <summary>
        /// Create new palette with maximum number of colors allowed to add
        /// </summary>
        public Palette(int maxColors)
        {
            if (maxColors > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(maxColors), "Palette can maximum have 256 colors");
            }
            
            this.maxColors = maxColors;
            this.colors = new List<Color>();
            Colors = new ReadOnlyCollection<Color>(this.colors);
            TransparentColor = -1;
        }
        
        /// <summary>
        /// Create new palette with predefined colors
        /// </summary>
        /// <param name="colors"></param>
        public Palette(IEnumerable<Color> colors)
        {
            this.colors = colors.ToList();
            
            if (this.colors.Count > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(maxColors), "Palette can maximum have 256 colors");
            }
            

            this.maxColors = this.colors.Count;
            TransparentColor = -1;
            Colors = new ReadOnlyCollection<Color>(this.colors);
        }

        /// <summary>
        /// Create new palette with predefined colors and set transparent color
        /// </summary>
        /// <param name="colors"></param>
        /// <param name="transparentColor"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Palette(IEnumerable<Color> colors, int transparentColor) : this(colors)
        {
            TransparentColor = transparentColor;
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