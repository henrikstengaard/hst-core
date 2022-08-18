namespace Hst.Imaging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Image
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int BitsPerPixel;
        public readonly int BytesPerPixel;
        public readonly int Scanline;

        public readonly Palette Palette;
        public readonly byte[] PixelData;

        /// <summary>
        /// Image uses a transparent background color
        /// </summary>
        public readonly bool IsTransparent;

        /// <summary>
        /// Color used as transparent background
        /// </summary>
        public readonly Color TransparentColor;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="bitsPerPixel">Bits per pixel used in image</param>
        /// <param name="isTransparent">Image uses a transparent background color</param>
        /// <param name="transparentColor">Color used as transparent background</param>
        /// <param name="palette">Palette to use 1, 4 and 8 bits per pixel for image. Empty, if image doesn't use palette for 24 and 32 bits per pixel images.</param>
        /// <param name="pixelData"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Image(int width, int height, int bitsPerPixel, bool isTransparent, Color transparentColor,
            Palette palette,
            IEnumerable<byte> pixelData) : this(width, height, bitsPerPixel, isTransparent, transparentColor)
        {
            if (bitsPerPixel > 8 && palette.Colors.Count > 0)
            {
                throw new ArgumentException("Palette must not have any colors for 24 and 32 bits per pixel images",
                    nameof(palette));
            }

            Palette = palette;
            PixelData = pixelData.ToArray();

            var pixelDataSize = width * height * ((bitsPerPixel <= 8 ? 8 : bitsPerPixel) / 8);
            if (PixelData.Length != pixelDataSize)
            {
                throw new ArgumentOutOfRangeException(
                    $"Image with dimension {width} x {height} and {BitsPerPixel} bits per pixel must have pixel data size of {pixelDataSize} bytes");
            }
        }

        /// <summary>
        /// Create new image
        /// </summary>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="bitsPerPixel">Bits per pixel used in image</param>
        public Image(int width, int height, int bitsPerPixel)
            : this(width, height, bitsPerPixel, false, Color.Transparent)
        {
        }

        /// <summary>
        /// Create new image
        /// </summary>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="bitsPerPixel">Bits per pixel used in image</param>
        /// <param name="isTransparent">Image uses a transparent background color</param>
        /// <param name="transparentColor">Color used as transparent background</param>
        /// <exception cref="ArgumentException"></exception>
        public Image(int width, int height, int bitsPerPixel, bool isTransparent, Color transparentColor)
        {
            if (bitsPerPixel != 1 && bitsPerPixel != 4 && bitsPerPixel != 8 && bitsPerPixel != 24 && bitsPerPixel != 32)
            {
                throw new ArgumentException("Only 1, 4, 8, 24 and 32 bits per pixel is supported",
                    nameof(bitsPerPixel));
            }

            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            IsTransparent = isTransparent;
            TransparentColor = transparentColor;
            BytesPerPixel = bitsPerPixel <= 8 ? 1 : bitsPerPixel / 8;
            Scanline = bitsPerPixel <= 8 ? width : width * BytesPerPixel;
            Palette = bitsPerPixel <= 8 ? new Palette(Convert.ToInt32(Math.Pow(2, bitsPerPixel)), isTransparent) : new Palette();
            PixelData = new byte[Scanline * height];

            // add transparent color to palette, if bits per pixel is less than or equal 8
            if (isTransparent && bitsPerPixel <= 8)
            {
                Palette.AddColor(transparentColor);
            }
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
                var color = Palette.Colors[paletteColor];
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
            if (BitsPerPixel > 8)
            {
                throw new ArgumentException("Only 1, 2, 4, 8 bits per pixel images can set pixel using palette color", nameof(paletteColor));
            }
            
            var pixelOffset = (Scanline * y) + x;
            PixelData[pixelOffset] = (byte)paletteColor;
        }

        public void SetPixel(int x, int y, Color color)
        {
            SetPixel(x, y, new Pixel
            {
                R = color.R,
                G = color.G,
                B = color.B,
                A = color.A
            });
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
            if (BitsPerPixel <= 8)
            {
                SetPixel(x, y, pixel.PaletteColor);
                return;
            }
            
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