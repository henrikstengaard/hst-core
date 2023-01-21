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

        private Color transparentColor;

        /// <summary>
        /// Image uses a transparent background color
        /// </summary>
        public bool IsTransparent
        {
            get
            {
                if (BitsPerPixel <= 8)
                {
                    return Palette.IsTransparent;
                }
                
                return TransparentColor != null;
            }
        }

        /// <summary>
        /// Color used as transparent background
        /// </summary>
        public Color TransparentColor
        {
            get => transparentColor;
            set
            {
                if (BitsPerPixel <= 8)
                {
                    throw new ArgumentException("Transparent color must be set in palette for an indexed image", nameof(TransparentColor));
                }

                transparentColor = value;
            }
        }

        /// <summary>
        /// Create new image
        /// </summary>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="bitsPerPixel">Bits per pixel used in image</param>
        public Image(int width, int height, int bitsPerPixel)
        {
            if (!IsBitsPerPixelValid(bitsPerPixel))
            {
                throw new ArgumentException("Only 1, 4, 8, 24 and 32 bits per pixel is supported",
                    nameof(bitsPerPixel));
            }
            
            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            BytesPerPixel = bitsPerPixel <= 8 ? 1 : bitsPerPixel / 8;
            Scanline = bitsPerPixel <= 8 ? width : width * BytesPerPixel;
            transparentColor = null;
            Palette = bitsPerPixel <= 8 ? new Palette(Convert.ToInt32(Math.Pow(2, bitsPerPixel))) : new Palette();
            PixelData = new byte[Scanline * height];
        }

        private static bool IsBitsPerPixelValid(int bitsPerPixel)
        {
            return bitsPerPixel == 1 ||
                   bitsPerPixel == 4 || bitsPerPixel == 8 || bitsPerPixel == 24 || bitsPerPixel == 32;
        }
        
        /// <summary>
        /// Create new image without no palette
        /// </summary>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="bitsPerPixel">Bits per pixel used in image</param>
        /// <param name="pixelData">Pixel data containing palette color index for 1, 4 and 8 bits per pixel images, rgb colors for 24 bits per pixel images or rgba for 32 bits per pixel images</param>
        public Image(int width, int height, int bitsPerPixel, IEnumerable<byte> pixelData)
            : this(width, height, bitsPerPixel, new Palette(), pixelData)
        {
        }
        
        /// <summary>
        /// Create new image with palette and pixel data
        /// </summary>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="bitsPerPixel">Bits per pixel used in image</param>
        /// <param name="palette">Palette to use for 1, 4 and 8 bits per pixel images. Palette must be empty for 24 and 32 bits per pixel images.</param>
        /// <param name="pixelData">Pixel data containing palette color index for 1, 4 and 8 bits per pixel images, rgb colors for 24 bits per pixel images or rgba for 32 bits per pixel images</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Image(int width, int height, int bitsPerPixel, Palette palette, IEnumerable<byte> pixelData)
        {
            if (!IsBitsPerPixelValid(bitsPerPixel))
            {
                throw new ArgumentException("Only 1, 4, 8, 24 and 32 bits per pixel is supported",
                    nameof(bitsPerPixel));
            }
            
            if (bitsPerPixel > 8 && palette.Colors.Count > 0)
            {
                throw new ArgumentException("Palette must not have any colors for 24 and 32 bits per pixel images",
                    nameof(palette));
            }

            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            BytesPerPixel = bitsPerPixel <= 8 ? 1 : bitsPerPixel / 8;
            Scanline = bitsPerPixel <= 8 ? width : width * BytesPerPixel;
            transparentColor = null;
            Palette = palette;
            PixelData = pixelData.ToArray();

            var pixelDataSize = width * height * ((bitsPerPixel <= 8 ? 8 : bitsPerPixel) / 8);
            if (PixelData.Length != pixelDataSize)
            {
                throw new ArgumentOutOfRangeException(
                    $"Image with dimension {width} x {height} and {BitsPerPixel} bits per pixel must have pixel data size of {pixelDataSize} bytes");
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