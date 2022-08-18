namespace Hst.Imaging.Tests.BitmapTests
{
    using System.IO;
    using System.Threading.Tasks;
    using Bitmaps;
    using Xunit;

    public class GivenBitmapReader : ImageTestBase
    {
        [Fact]
        public async Task WhenReadBitmapWith8BppThenImageMatch()
        {
            // arrange - create new image 
            var expectedImage = Create8BppImage(false);
            
            // act - read image from bitmap
            await using var stream = File.OpenRead(Path.Combine("TestData", "Bitmaps", "8bpp.bmp"));
            var image = BitmapReader.Read(stream);
            
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
        public async Task WhenReadBitmapWith24BppThenImageMatch()
        {
            // arrange - create new image 
            var expectedImage = Create24BppImage(false);
            
            // act - read image from bitmap
            await using var stream = File.OpenRead(Path.Combine("TestData", "Bitmaps", "24bpp.bmp"));
            var image = BitmapReader.Read(stream);
            
            // assert - 24 bits per pixel image doesn't have any palette colors
            Assert.Equal(0, image.Palette.Colors.Count);
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
        
        [Fact]
        public async Task WhenReadBitmapWith32BppThenImageMatch()
        {
            // arrange - create new image 
            var expectedImage = Create32BppImage(true);
            
            // act - read image from bitmap
            await using var stream = File.OpenRead(Path.Combine("TestData", "Bitmaps", "32bpp.bmp"));
            var image = BitmapReader.Read(stream);
            
            // assert - 32 bits per pixel image doesn't have any palette colors
            Assert.Equal(0, image.Palette.Colors.Count);
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
    }
}