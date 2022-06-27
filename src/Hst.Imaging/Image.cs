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
        public IReadOnlyList<Color> Palette;
        public byte[] Data;

        public Image(int width, int height, int bitsPerPixel, IEnumerable<Color> palette,
            IEnumerable<byte> data) : this(width, height, bitsPerPixel)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            
            this.palette = palette.ToList();
            Palette = new ReadOnlyCollection<Color>(this.palette);
            Data = data.ToArray();
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

            this.maxColors = bitsPerPixel <= 8 ? Convert.ToInt32(Math.Pow(2, bitsPerPixel) - 1) : 0;
            this.palette = new List<Color>();
            Palette = new ReadOnlyCollection<Color>(this.palette);
            Data = new byte[Scanline * height];
        }

        public void AddColor(int r, int g, int b, int a = 255)
        {
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
                var paletteColor = Data[offset];
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
                R = Data[offset + 2],
                G = Data[offset + 1],
                B = Data[offset],
                A = BitsPerPixel == 32 ? Data[offset + 3] : 0,
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
            Data[pixelOffset] = (byte)paletteColor;
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

            Data[pixelOffset] = (byte)pixel.R;
            Data[pixelOffset + 1] = (byte)pixel.G;
            Data[pixelOffset + 2] = (byte)pixel.B;

            if (BitsPerPixel <= 24)
            {
                return;
            }

            Data[pixelOffset + 3] = (byte)pixel.A;
        }
    }
}