namespace Hst.Imaging.Tests.PngTests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Hjg.Pngcs;
    using Xunit;
    using PngWriter = Pngs.PngWriter;

    public class GivenPngWriter : ImageTestBase
    {
        [Fact]
        public async Task WhenWriteImageWith8BitsPerPixelThenReadImageFromPngMatches()
        {
            // arrange - create transparent 8 bits per pixel image
            var image = Create8BppImage(true);

            // act - write image to png stream
            var pngStream = new MemoryStream();
            PngWriter.Write(pngStream, image);

            // act - write png stream to file
            await File.WriteAllBytesAsync("8bpp.png", pngStream.ToArray());

            // assert - png stream is equal to image
            AssertImageEqual(image, new MemoryStream(pngStream.ToArray()));
        }

        [Fact]
        public async Task WhenWriteImageWith24BitsPerPixelThenReadImageFromPngMatches()
        {
            // arrange - create transparent 32 bits per pixel image
            var image = Create24BppImage(true);

            // act - write image to png stream
            var pngStream = new MemoryStream();
            PngWriter.Write(pngStream, image);

            // act - write png stream to file
            await File.WriteAllBytesAsync("24bpp.png", pngStream.ToArray());

            // assert - png stream is equal to image
            AssertImageEqual(image, new MemoryStream(pngStream.ToArray()));
        }

        [Fact]
        public async Task WhenWriteImageWith32BitsPerPixelThenReadImageFromPngMatches()
        {
            // arrange - create transparent 32 bits per pixel image
            var image = Create32BppImage(true);

            // act - write image to png stream
            using var pngStream = new MemoryStream();
            PngWriter.Write(pngStream, image);

            // act - write png stream to file
            await File.WriteAllBytesAsync("32bpp.png", pngStream.ToArray());

            // assert - png stream is equal to image
            AssertImageEqual(image, new MemoryStream(pngStream.ToArray()));
        }

        private void AssertImageEqual(Image expectedImage, Stream pngStream)
        {
            var pngReader = new PngReader(pngStream);
            var imgInfo = pngReader.ImgInfo;

            Assert.Equal(expectedImage.Width, imgInfo.Cols);
            Assert.Equal(expectedImage.Height, imgInfo.Rows);

            Assert.Equal(imgInfo.Alpha, expectedImage.BitsPerPixel == 32);

            var paletteChunk = pngReader.GetMetadata().GetPLTE();
            var transparencyChunk = pngReader.GetMetadata().GetTRNS();

            if (expectedImage.IsTransparent)
            {
                Assert.NotNull(transparencyChunk);

                switch (expectedImage.BitsPerPixel)
                {
                    case 1:
                    case 2:
                    case 4:
                    case 8:
                        var alphaEntries = transparencyChunk.GetPalletteAlpha();
                        Assert.Equal(expectedImage.Palette.Colors.Select(x => x.A), alphaEntries);
                        break;
                    case 24:
                        var transparentRgb = transparencyChunk.GetRGB();
                        Assert.Equal(expectedImage.TransparentColor.R, transparentRgb[0]);
                        Assert.Equal(expectedImage.TransparentColor.G, transparentRgb[1]);
                        Assert.Equal(expectedImage.TransparentColor.B, transparentRgb[2]);
                        break;
                }
            }

            if (expectedImage.BitsPerPixel <= 8)
            {
                Assert.Equal(1, imgInfo.Channels);
                Assert.Equal(expectedImage.Palette.Colors.Count, paletteChunk.GetNentries());

                for (var i = 0; i < expectedImage.Palette.Colors.Count; i++)
                {
                    var rgb = new int[4];
                    paletteChunk.GetEntryRgb(i, rgb);

                    Assert.Equal(expectedImage.Palette.Colors[i].R, rgb[0]);
                    Assert.Equal(expectedImage.Palette.Colors[i].G, rgb[1]);
                    Assert.Equal(expectedImage.Palette.Colors[i].B, rgb[2]);
                }
            }
            
            var pixelDataOffset = 0;
            for (var y = 0; y < imgInfo.Rows; y++)
            {
                var imageLine = pngReader.ReadRowInt(y);

                if (!imageLine.SamplesUnpacked)
                {
                    imageLine = imageLine.unpackToNewImageLine();
                }

                var imageLineOffset = 0;
                for (var x = 0; x < imgInfo.Cols; x++)
                {
                    for (var c = 0; c < imageLine.ImgInfo.Channels; c++)
                    {
                        Assert.Equal(expectedImage.PixelData[pixelDataOffset++],
                            (byte)imageLine.Scanline[imageLineOffset++]);
                    }
                }
            }
        }
    }
}