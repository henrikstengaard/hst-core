namespace Hst.Imaging.Pngcs.Tests
{
    using System.IO;
    using System.Threading.Tasks;
    using Imaging.Tests;
    using Xunit;

    public class GivenPngReader : ImageTestBase
    {
        [Fact]
        public async Task WhenReadPngWith4BppThenImageMatch()
        {
            // arrange - create new image
            var expectedImage = Create4BppImage(true);
            
            // act - read image from png
            await using var stream = File.OpenRead(Path.Combine("TestData", "Pngs", "4bpp.png"));
            var image = PngReader.Read(stream);
            
            // assert - 4 bits per pixel image has 16 colors
            Assert.Equal(16, image.Palette.Colors.Count);
            
            // assert - palette colors match
            for (var i = 0; i < expectedImage.Palette.Colors.Count; i++)
            {
                Assert.Equal(expectedImage.Palette.Colors[i].R, image.Palette.Colors[i].R);
                Assert.Equal(expectedImage.Palette.Colors[i].G, image.Palette.Colors[i].G);
                Assert.Equal(expectedImage.Palette.Colors[i].B, image.Palette.Colors[i].B);
                Assert.Equal(expectedImage.Palette.Colors[i].A, image.Palette.Colors[i].A);
            }
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);            
        }

        [Fact]
        public async Task WhenReadPngWith8BppThenImageMatch()
        {
            // arrange - create new image
            var expectedImage = Create8BppImage(true);
            
            // act - read image from png
            await using var stream = File.OpenRead(Path.Combine("TestData", "Pngs", "8bpp.png"));
            var image = PngReader.Read(stream);
            
            // assert - 8 bits per pixel image has 256 colors
            Assert.Equal(256, image.Palette.Colors.Count);
            
            // assert - palette colors match
            for (var i = 0; i < expectedImage.Palette.Colors.Count; i++)
            {
                Assert.Equal(expectedImage.Palette.Colors[i].R, image.Palette.Colors[i].R);
                Assert.Equal(expectedImage.Palette.Colors[i].G, image.Palette.Colors[i].G);
                Assert.Equal(expectedImage.Palette.Colors[i].B, image.Palette.Colors[i].B);
                Assert.Equal(expectedImage.Palette.Colors[i].A, image.Palette.Colors[i].A);
            }
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);            
        }
        
        [Fact]
        public async Task WhenReadPngWith24BppThenImageMatch()
        {
            // arrange - create new image
            var expectedImage = Create32BppImage(true);
            
            // act - read image from png
            await using var stream = File.OpenRead(Path.Combine("TestData", "Pngs", "24bpp.png"));
            var image = PngReader.Read(stream);
            
            // assert - image doesn't have any palette colors
            Assert.Equal(0, image.Palette.Colors.Count);
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
        
        [Fact]
        public async Task WhenReadPngWith24BppAndAlphaChannelThenImageMatch()
        {
            // arrange - create new image
            var expectedImage = Create32BppImage(true);

            // act - read image from png
            await using var stream = File.OpenRead(Path.Combine("TestData", "Pngs", "24bpp_alpha.png"));
            var image = PngReader.Read(stream);
            
            // assert - image doesn't have any palette colors
            Assert.Equal(0, image.Palette.Colors.Count);
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
    }
}