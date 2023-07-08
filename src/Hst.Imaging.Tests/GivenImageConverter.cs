namespace Hst.Imaging.Tests;

using System;
using Xunit;

public class GivenImageConverter : ImageTestBase
{
    [Fact]
    public void WhenConvert8BppTo8BppThenImageMatch()
    {
        // arrange - create 8 bpp image
        var expectedImage = Create8BppImage(true);

        // act & assert - convert 8 bpp image to 8 bpp throws exception
        Assert.Throws<InvalidOperationException>(() => ImageConverter.To8Bpp(expectedImage));
    }

    [Fact]
    public void WhenConvert24BppTo8BppThenImageMatch()
    {
        // arrange - create 24 bpp image
        var expectedImage = Create24BppImage(true);

        // act - convert 24 bpp image to 8 bpp
        var convertedImage = ImageConverter.To8Bpp(expectedImage);

        // assert - expected and converted image match
        Assert.Equal(8, convertedImage.BitsPerPixel);
        AssertEqual(expectedImage, convertedImage);
    }
    
    [Fact]
    public void WhenConvert32BppTo8BppThenImageMatch()
    {
        // arrange - create 32 bpp image
        var expectedImage = Create32BppImage(true);

        // act - convert 32 bpp image to 8 bpp
        var convertedImage = ImageConverter.To8Bpp(expectedImage);

        // assert - expected and converted image match
        Assert.Equal(8, convertedImage.BitsPerPixel);
        AssertEqual(expectedImage, convertedImage);
    }

    private void AssertEqual(Image expectedImage, Image convertedImage)
    {
        Assert.Equal(expectedImage.Width, convertedImage.Width);
        Assert.Equal(expectedImage.Height, convertedImage.Height);
    
        var expectedImageIterator = new ImagePixelDataIterator(expectedImage);
        var convertedImageIterator = new ImagePixelDataIterator(convertedImage);

        var pixelCount = 0;
        while (expectedImageIterator.Next() && convertedImageIterator.Next())
        {
            pixelCount++;
            var expectedPixel = expectedImageIterator.Current;
            var convertedPixel = convertedImageIterator.Current;
                
            Assert.Equal(expectedPixel.R, convertedPixel.R);
            Assert.Equal(expectedPixel.G, convertedPixel.G);
            Assert.Equal(expectedPixel.B, convertedPixel.B);
            Assert.Equal(expectedPixel.A, convertedPixel.A);
        }

        Assert.NotEqual(0, pixelCount);
        Assert.Equal(expectedImage.Width * expectedImage.Height, pixelCount);
    }
}