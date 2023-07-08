namespace Hst.Imaging
{
    using System;
    using System.Collections.Generic;

    public static class ImageConverter
    {
        public static Image To8Bpp(Image image)
        {
            if (image.BitsPerPixel == 8)
            {
                throw new InvalidOperationException("Image is already 8 bpp");
            }
            
            var pixelData = new byte[image.Width * image.Height];

            var colors = new List<Color>();
            var colorsIndex = new Dictionary<uint, int>();

            var transparentColor = -1;

            var pixelDataIterator = new ImagePixelDataIterator(image);

            var offset = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!pixelDataIterator.Next())
                    {
                        throw new InvalidOperationException();
                    }

                    var pixel = pixelDataIterator.Current;

                    var colorId = (uint)pixel.R << 24 | (uint)pixel.G << 16 | (uint)pixel.B << 8 | (uint)pixel.A;
                    if (!colorsIndex.ContainsKey(colorId))
                    {
                        colors.Add(new Color((byte)pixel.R, (byte)pixel.G, (byte)pixel.B, (byte)pixel.A));
                        colorsIndex[colorId] = colors.Count - 1;
                    }

                    var color = colorsIndex[colorId];

                    // first transparent color is used as transparent color
                    if (transparentColor == -1 && pixel.A == 0)
                    {
                        transparentColor = color;
                    }

                    pixelData[offset++] = (byte)color;
                }
            }

            if (colors.Count > 256)
            {
                throw new ArgumentException(
                    $"Image has {colors.Count} colors and NewIcon only allows max 256 colors",
                    nameof(image));
            }

            var colorsArray = colors.ToArray();
            SwapTransparentColor(colorsArray, transparentColor);

            return new Image(image.Width, image.Height, 8, new Palette(colors, transparentColor), pixelData);
        }

        private static void SwapTransparentColor(Color[] colors, int transparentColor)
        {
            // return, if transparent color is not set or is first color
            if (transparentColor <= 0)
            {
                return;
            }

            // swap transparent color and color zero via deconstruction
            (colors[transparentColor], colors[0]) = (colors[0], colors[transparentColor]);
        }
    }
}