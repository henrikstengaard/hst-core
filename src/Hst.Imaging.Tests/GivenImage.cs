namespace Hst.Imaging.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class GivenImage
    {
        [Fact]
        public void WhenCreate8BppImageThenImageHas1BytePerPixel()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 8;

            // act - create image sized 2 x 2 with 8 bits per pixel
            var image = new Image(width, height, bitsPerPixel);
            
            // assert - image uses 4 bytes to represent image
            Assert.Equal(new byte[4], image.PixelData);
        }

        [Fact]
        public void WhenCreate24BppImageThenImageHas3BytePerPixel()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 24;
            
            // act - create image sized 2 x 2 with 24 bits per pixel
            var image = new Image(width, height, bitsPerPixel);
            
            // assert - image uses 12 bytes to represent image
            Assert.Equal(new byte[12], image.PixelData);
        }
        
        [Fact]
        public void WhenCreate32BppImageThenImageHas3BytePerPixel()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 32;
            
            // act - create image sized 2 x 2 with 32 bits per pixel
            var image = new Image(width, height, bitsPerPixel);
            
            // assert - image uses 16 bytes to represent image
            Assert.Equal(new byte[16], image.PixelData);
        }
        
        [Fact]
        public void WhenSetPixelsWith8BppImageThenImageDataIsUpdated()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 8;

            // act - create image sized 2 x 2 with 8 bits per pixel
            var image = new Image(width, height, bitsPerPixel);
            
            // act - add colors
            image.AddColor(0, 0, 0);
            image.AddColor(255, 0, 0);
            image.AddColor(0, 255, 0);
            
            // act - set pixels
            image.SetPixel(0, 0, 1);
            image.SetPixel(1, 0, 1);
            image.SetPixel(0, 1, 2);
            image.SetPixel(1, 1, 2);
            
            // assert - image data is equal to expected image data
            var expectedImageData = new List<byte>
            {
                1, // pixel 0,0 palette color 1
                1, // pixel 1,0 palette color 1
                2, // pixel 0,1 palette color 2
                2, // pixel 1,1 palette color 2
            };
            Assert.Equal(expectedImageData.ToArray(), image.PixelData);
        }
        
        [Fact]
        public void WhenSetPixelsWith24BppImageThenImageDataIsUpdated()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 24;

            // act - create image sized 2 x 2 with 8 bits per pixel
            var image = new Image(width, height, bitsPerPixel);
            
            // act - set pixels
            image.SetPixel(0, 0, 255, 0, 0);
            image.SetPixel(1, 0, 255, 0, 0);
            image.SetPixel(0, 1, 255, 255, 0);
            image.SetPixel(1, 1, 255, 255, 0);

            // assert - image data is equal to expected image data
            var expectedImageData = new List<byte>
            {
                255, // pixel 0,0 red
                0, // pixel 0,0 green
                0, // pixel 0,0 blue
                
                255, // pixel 1,0 red
                0, // pixel 1,0 green
                0, // pixel 1,0 blue

                255, // pixel 0,1 red
                255, // pixel 0,1 green
                0, // pixel 0,1 blue

                255, // pixel 1,1 red
                255, // pixel 1,1 green
                0 // pixel 1,1 blue
            };
            Assert.Equal(expectedImageData.ToArray(), image.PixelData);
        }
        
        [Fact]
        public void WhenSetPixelsWith32BppImageThenImageDataIsUpdated()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 32;

            // act - create image sized 2 x 2 with 8 bits per pixel
            var image = new Image(width, height, bitsPerPixel);
            
            // act - set pixels
            image.SetPixel(0, 0, 255, 0, 0);
            image.SetPixel(1, 0, 255, 0, 0);
            image.SetPixel(0, 1, 255, 255, 0);
            image.SetPixel(1, 1, 255, 255, 0);

            // assert - image data is equal to expected image data
            var expectedImageData = new List<byte>
            {
                255, // pixel 0,0 red
                0, // pixel 0,0 green
                0, // pixel 0,0 blue
                255, // pixel 0,0 alpha
                
                255, // pixel 1,0 red
                0, // pixel 1,0 green
                0, // pixel 1,0 blue
                255, // pixel 1,0 alpha

                255, // pixel 0,1 red
                255, // pixel 0,1 green
                0, // pixel 0,1 blue
                255, // pixel 0,1 alpha

                255, // pixel 1,1 red
                255, // pixel 1,1 green
                0, // pixel 1,1 blue
                255 // pixel 1,1 alpha
            };
            Assert.Equal(expectedImageData.ToArray(), image.PixelData);
        }

        [Fact]
        public void WhenAddPaletteColorTo24BppThenErrorIsThrown()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 24;

            // arrange - create image sized 2 x 2 with 24 bits per pixel
            var image = new Image(width, height, bitsPerPixel);

            // act and assert - add color throws exception
            Assert.Throws<InvalidOperationException>(() => image.AddColor(255, 0, 0));
        }
        
        [Fact]
        public void WhenAddMoreThen256PaletteColorsTo8BppThenErrorIsThrown()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 8;

            // act - create image sized 2 x 2 with 8 bits per pixel
            var image = new Image(width, height, bitsPerPixel);

            // act - add 256 palette colors
            for (var i = 0; i <= 255; i++)
            {
                image.AddColor(255, 0, 0);
            }
            
            // act and assert - add color throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => image.AddColor(255, 0, 0));
        }
        
        [Fact]
        public void WhenCreate8BppImageWithMoreThan256PaletteColorsThenErrorIsThrown()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 8;

            var palette = new Color[257];
            var pixelData = new byte[width * height * (bitsPerPixel / 8)];
            
            // act and assert - add create image throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => new Image(width, height, bitsPerPixel, palette, pixelData));
        }
        
        [Fact]
        public void WhenCreate8BppImageWithIncorrectPixelDataThenErrorIsThrown()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 8;

            var palette = new Color[255];
            var pixelData = new byte[2000]; // incorrect
            
            // act and assert - add create image throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => new Image(width, height, bitsPerPixel, palette, pixelData));
        }
    }
}