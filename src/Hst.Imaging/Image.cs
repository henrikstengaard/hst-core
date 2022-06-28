namespace Hst.Imaging
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using HstWbInstaller.Core.IO.Images.Bitmap;

    public class Image
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int BitsPerPixel;
        public readonly int Scanline;

        private readonly int maxColors;
        private readonly IList<Color> palette;
        public readonly IReadOnlyList<Color> Palette;
        public readonly byte[] PixelData;

        public Image(int width, int height, int bitsPerPixel, IEnumerable<Color> palette,
            IEnumerable<byte> pixelData) : this(width, height, bitsPerPixel)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            
            this.palette = palette.ToList();

            if (bitsPerPixel <= 8 && this.palette.Count > this.maxColors)
            {
                throw new ArgumentOutOfRangeException($"Bits per pixel {BitsPerPixel} can only have maximum {maxColors} colors");
            }
            
            Palette = new ReadOnlyCollection<Color>(this.palette);
            PixelData = pixelData.ToArray();
            
            var pixelDataSize = width * height * ((bitsPerPixel <= 8 ? 8 : bitsPerPixel) / 8);
            if (PixelData.Length != pixelDataSize)
            {
                throw new ArgumentOutOfRangeException($"Image with dimension {width} x {height} and {BitsPerPixel} bits per pixel must have pixel data size of {pixelDataSize} bytes");
            }
        }

        public Image(int width, int height, int bitsPerPixel)
        {
            if (bitsPerPixel != 1 && bitsPerPixel != 4 && bitsPerPixel != 8 && bitsPerPixel != 24 && bitsPerPixel != 32)
            {
                throw new ArgumentException($"{bitsPerPixel} bits per pixel is not supported", nameof(bitsPerPixel));
            }

            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            Scanline = bitsPerPixel <= 8 ? width : width * bitsPerPixel / 8;

            this.maxColors = bitsPerPixel <= 8 ? Convert.ToInt32(Math.Pow(2, bitsPerPixel)) : 0;
            this.palette = new List<Color>();
            Palette = new ReadOnlyCollection<Color>(this.palette);
            PixelData = new byte[Scanline * height];
        }

        /// <summary>
        /// Add color to palette. Only supported for bits per pixels of 8 or less
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddColor(int r, int g, int b, int a = 255)
        {
            if (BitsPerPixel > 8)
            {
                throw new InvalidOperationException("Only bits per pixel of 8 or less supports palette colors");
            }

            if (palette.Count >= maxColors)
            {
                throw new ArgumentOutOfRangeException($"Bits per pixel {BitsPerPixel} can only have maximum {maxColors} colors");
            }
            
            AddColor(new Color
            {
                R = r,
                G = g,
                B = b,
                A = a,
            });
        }

        public void AddColor(Color color)
        {
            this.palette.Add(color);
        }
        
        public Pixel GetPixel(int x, int y)
        {
            if (BitsPerPixel < 8)
            {
                throw new NotSupportedException();
            }

            var offset = (Scanline * y) + x;
            if (BitsPerPixel == 8)
            {
                var paletteColor = PixelData[offset];
                var color = Palette[paletteColor];
                return new Pixel
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A,
                    PaletteColor = paletteColor
                };
            }

            offset *= BitsPerPixel / 8;

            return new Pixel
            {
                R = PixelData[offset + 2],
                G = PixelData[offset + 1],
                B = PixelData[offset],
                A = BitsPerPixel == 32 ? PixelData[offset + 3] : 0,
                PaletteColor = 0
            };
        }

        public void SetPixel(int x, int y, int paletteColor)
        {
            if (BitsPerPixel != 8)
            {
                throw new NotSupportedException();
            }

            var pixelOffset = (Scanline * y) + x;
            PixelData[pixelOffset] = (byte)paletteColor;
        }

        public void SetPixel(int x, int y, int r, int g, int b, int a = 255)
        {
            SetPixel(x, y, new Pixel
            {
                R = r,
                G = g,
                B = b,
                A = a
            });
        }

        public void SetPixel(int x, int y, Pixel pixel)
        {
            var pixelOffset = Scanline * y + x * (BitsPerPixel / 8);

            PixelData[pixelOffset] = (byte)pixel.R;
            PixelData[pixelOffset + 1] = (byte)pixel.G;
            PixelData[pixelOffset + 2] = (byte)pixel.B;

            if (BitsPerPixel <= 24)
            {
                return;
            }

            PixelData[pixelOffset + 3] = (byte)pixel.A;
        }
    }
}