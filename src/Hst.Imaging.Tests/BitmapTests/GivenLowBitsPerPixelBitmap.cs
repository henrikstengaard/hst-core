namespace Hst.Imaging.Tests.BitmapTests
{
    using System;
    using System.IO;
    using System.Linq;
    using Bitmaps;
    using Xunit;

    public class GivenLowBitsPerPixelBitmap
    {
        private static Image CreateIndexedImage(int width, int height, int bitsPerPixel)
        {
            var colors = 1 << bitsPerPixel;
            var palette = new Palette(Enumerable.Range(0, colors)
                .Select(i => new Color(i * 255 / (colors - 1), 255 - i * 255 / (colors - 1), i % 2 * 255, 255)));

            // pixel pattern using all palette colors, varying by row to detect row order errors
            var pixelData = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixelData[y * width + x] = (byte)((x + y * 3) % colors);
                }
            }

            return new Image(width, height, bitsPerPixel, palette, pixelData);
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 13, 3)]
        [InlineData(1, 32, 2)]
        [InlineData(1, 33, 5)]
        [InlineData(4, 1, 1)]
        [InlineData(4, 7, 3)]
        [InlineData(4, 8, 2)]
        [InlineData(4, 9, 4)]
        public void WhenWritingAndReadingBitmapThenImageIsEqual(int bitsPerPixel, int width, int height)
        {
            // arrange
            var image = CreateIndexedImage(width, height, bitsPerPixel);
            using var stream = new MemoryStream();

            // act
            BitmapWriter.Write(stream, image);
            stream.Position = 0;
            var readImage = BitmapReader.Read(stream);

            // assert
            Assert.Equal(bitsPerPixel, readImage.BitsPerPixel);
            Assert.Equal(width, readImage.Width);
            Assert.Equal(height, readImage.Height);
            Assert.Equal(image.PixelData, readImage.PixelData);
            Assert.Equal(image.Palette.Colors.Select(c => (c.R, c.G, c.B)),
                readImage.Palette.Colors.Select(c => (c.R, c.G, c.B)));
        }

        [Fact]
        public void WhenWriting1BitPerPixelBitmapThenPixelsArePackedMostSignificantBitFirstWithPadding()
        {
            // arrange - 10 x 1 image with pixels 1,0,1,1,0,0,0,1 | 1,1
            var pixelData = new byte[] { 1, 0, 1, 1, 0, 0, 0, 1, 1, 1 };
            var palette = new Palette(new[] { new Color(0, 0, 0, 255), new Color(255, 255, 255, 255) });
            var image = new Image(10, 1, 1, palette, pixelData);
            using var stream = new MemoryStream();

            // act
            BitmapWriter.Write(stream, image);

            // assert - pixel data is last scanline, 10 bits padded to 4 bytes
            var bytes = stream.ToArray();
            var scanline = bytes.Skip(bytes.Length - 4).ToArray();
            Assert.Equal(new byte[] { 0b10110001, 0b11000000, 0, 0 }, scanline);
        }

        [Fact]
        public void WhenReadingBitmapWithUnsupportedBitsPerPixelThenNotSupportedExceptionIsThrown()
        {
            // arrange - bitmap header with 16 bits per pixel
            using var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write((ushort)0x4d42); // "BM"
            writer.Write(0U); // file size
            writer.Write(0U); // reserved
            writer.Write(54U); // pixel data offset
            writer.Write(40U); // header size
            writer.Write(1); // width
            writer.Write(1); // height
            writer.Write((ushort)1); // planes
            writer.Write((ushort)16); // bits per pixel
            writer.Write(new byte[24]); // compression and remaining fields
            stream.Position = 0;

            // act & assert
            Assert.Throws<NotSupportedException>(() => BitmapReader.Read(stream));
        }
    }
}
