namespace Hst.Imaging.Tests.BitmapTests
{
    using System;
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

        [Fact]
        public async Task WhenWriteAndReadBitmapWith8BppThenImageMatch()
        {
            // arrange - expected image 2 x 2 image with 8 bits per pixel
            const bool isTransparent = false; // 8 bits per pixel bitmap can't be transparent
            var expectedImage = new Image(2, 2, 8);
            
            // arrange - add palette color
            expectedImage.Palette.AddColor(new Color(10, 20, 30));
            
            // arrange - set pixels
            SetPixels(expectedImage, isTransparent);

            // act - write image as bitmap
            await using var stream = new MemoryStream();
            BitmapWriter.Write(stream, expectedImage);

            // act - read image from bitmap
            stream.Position = 0;
            var actualImage = BitmapReader.Read(stream);

            // assert - palette colors match
            for (var i = 0; i < Math.Min(expectedImage.Palette.Colors.Count, actualImage.Palette.Colors.Count); i++)
            {
                Assert.Equal(expectedImage.Palette.Colors[i].R, actualImage.Palette.Colors[i].R);
                Assert.Equal(expectedImage.Palette.Colors[i].G, actualImage.Palette.Colors[i].G);
                Assert.Equal(expectedImage.Palette.Colors[i].B, actualImage.Palette.Colors[i].B);
                Assert.Equal(expectedImage.Palette.Colors[i].A, actualImage.Palette.Colors[i].A);
            }
            
            // assert - pixel data match
            Assert.Equal(expectedImage.PixelData.Length, actualImage.PixelData.Length);
            Assert.Equal(expectedImage.PixelData, actualImage.PixelData);
        }
    }
}