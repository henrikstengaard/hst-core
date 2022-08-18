namespace Hst.Imaging.Tests
{
    using System;
    using Xunit;

    public class GivenPalette
    {
        [Fact]
        public void WhenAddPaletteColorTo24BppImageThenErrorIsThrown()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 24;

            // arrange - create image sized 2 x 2 with 24 bits per pixel
            var image = new Image(width, height, bitsPerPixel);

            // act and assert - add color throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => image.Palette.AddColor(255, 0, 0));
        }

        [Fact]
        public void WhenAddPaletteColorTo32BppImageThenErrorIsThrown()
        {
            const int width = 2;
            const int height = 2;
            const int bitsPerPixel = 32;

            // arrange - create image sized 2 x 2 with 32 bits per pixel
            var image = new Image(width, height, bitsPerPixel);

            // act and assert - add color throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => image.Palette.AddColor(255, 0, 0));
        }
        
        [Fact]
        public void WhenAddMoreThen256PaletteColorsTo8BppThenErrorIsThrown()
        {
            const int bitsPerPixel = 8;

            var palette = new Palette(Convert.ToInt32(Math.Pow(2, bitsPerPixel)));

            // act - add 256 palette colors
            for (var i = 0; i <= 255; i++)
            {
                palette.AddColor(255, 0, 0);
            }

            
            // act and assert - add color throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => palette.AddColor(255, 0, 0));
        }

        [Fact]
        public void WhenCreate8BppImageWithMoreThan256PaletteColorsThenErrorIsThrown()
        {
            var colors = new Color[257];

            // act and assert - add create image throws exception
            Assert.Throws<ArgumentOutOfRangeException>(() => new Palette(colors));
        }
    }
}