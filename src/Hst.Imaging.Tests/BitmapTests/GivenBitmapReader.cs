namespace Hst.Imaging.Tests.BitmapTests
{
    using System.IO;
    using System.Threading.Tasks;
    using Bitmaps;
    using Xunit;

    public class GivenBitmapReader
    {
        [Fact]
        public async Task WhenReadBitmapWith8BppThenImageMatch()
        {
            // arrange - create new bitmap image 
            var expectedImage = new Image(2, 2, 8);
            
            // arrange - add palette colors
            expectedImage.AddColor(new Color
            {
                R = 0,
                G = 0,
                B = 0
            });
            expectedImage.AddColor(new Color
            {
                R = 255,
                G = 0,
                B = 0
            });

            // arrange - set pixels
            expectedImage.SetPixel(0, 0, 1); // pixel 0,0 red
            expectedImage.SetPixel(1, 1, 1); // pixel 1,1 red
            
            // act - read image from bitmap
            await using var stream = File.OpenRead(Path.Combine("TestData", "Bitmaps", "8bpp.bmp"));
            var image = BitmapReader.Read(stream);
            
            // assert - 8 bits per pixel image has 256 colors
            Assert.Equal(256, image.Palette.Count);
            
            // assert - palette colors match
            for (var i = 0; i < expectedImage.Palette.Count; i++)
            {
                Assert.Equal(expectedImage.Palette[i].R, image.Palette[i].R);
                Assert.Equal(expectedImage.Palette[i].G, image.Palette[i].G);
                Assert.Equal(expectedImage.Palette[i].B, image.Palette[i].B);
                Assert.Equal(expectedImage.Palette[i].A, image.Palette[i].A);
            }
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
        
        [Fact]
        public async Task WhenReadBitmapWith24BppThenImageMatch()
        {
            // arrange - create new bitmap image 
            var expectedImage = new Image(2, 2, 24);
            
            // arrange - set pixels
            expectedImage.SetPixel(0, 0, 255, 0, 0); // pixel 0,0 red
            expectedImage.SetPixel(1, 1, 255, 0, 0); // pixel 1,1 red

            // act - read image from bitmap
            await using var stream = File.OpenRead(Path.Combine("TestData", "Bitmaps", "24bpp.bmp"));
            var image = BitmapReader.Read(stream);
            
            // assert - 24 bits per pixel image doesn't have any palette colors
            Assert.Equal(0, image.Palette.Count);
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
        
        [Fact]
        public async Task WhenReadBitmapWith32BppThenImageMatch()
        {
            // arrange - create new bitmap image 
            var expectedImage = new Image(2, 2, 32);
            
            // arrange - set pixels
            expectedImage.SetPixel(0, 0, 255, 0, 0, 255); // pixel 0,0 red
            expectedImage.SetPixel(1, 0, 0, 0, 0, 0); // pixel 1,0 transparent
            expectedImage.SetPixel(0, 1, 0, 0, 0, 0); // pixel 0,1 transparent
            expectedImage.SetPixel(1, 1, 255, 0, 0, 255); // pixel 1,1 red

            // act - read image from bitmap
            await using var stream = File.OpenRead(Path.Combine("TestData", "Bitmaps", "32bpp.bmp"));
            var image = BitmapReader.Read(stream);
            
            // assert - 32 bits per pixel image doesn't have any palette colors
            Assert.Equal(0, image.Palette.Count);
            
            // assert - image data matches expected image data
            Assert.Equal(expectedImage.PixelData, image.PixelData);
        }
    }
}