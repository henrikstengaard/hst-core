namespace Hst.Imaging.Tests
{
    using Xunit;

    public class GivenImageGetPixel
    {
        [Theory]
        [InlineData(24)]
        [InlineData(32)]
        public void WhenSettingPixelThenGetPixelReturnsSameColor(int bitsPerPixel)
        {
            var image = new Image(5, 4, bitsPerPixel);

            image.SetPixel(3, 2, 10, 20, 30, 40);

            var pixel = image.GetPixel(3, 2);
            Assert.Equal(10, pixel.R);
            Assert.Equal(20, pixel.G);
            Assert.Equal(30, pixel.B);
            Assert.Equal(bitsPerPixel == 32 ? 40 : 255, pixel.A);

            // other pixels are untouched
            var otherPixel = image.GetPixel(2, 3);
            Assert.Equal(0, otherPixel.R);
            Assert.Equal(0, otherPixel.G);
            Assert.Equal(0, otherPixel.B);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(8)]
        public void WhenSettingPaletteColorThenGetPixelReturnsPaletteColor(int bitsPerPixel)
        {
            var image = new Image(5, 4, bitsPerPixel);
            image.Palette.AddColor(0, 0, 0);
            image.Palette.AddColor(200, 100, 50);

            image.SetPixel(4, 3, 1);

            var pixel = image.GetPixel(4, 3);
            Assert.Equal(1, pixel.PaletteColor);
            Assert.Equal(200, pixel.R);
            Assert.Equal(100, pixel.G);
            Assert.Equal(50, pixel.B);
        }
    }
}
