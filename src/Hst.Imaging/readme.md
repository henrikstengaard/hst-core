# Hst Imaging

Hst Imaging is a library for creating and manipulating images with basic support for reading and writing image file formats.

Main reason for writing this imaging library is System.Drawing.Common for .NET 6 and newer only supports Windows platform and SixLabors.ImageSharp reads indexed images, but translates palette into RGB.

This makes it difficult to create and manipulate indexed images with palettes for cross-platform usage.

Features:
- Create images with 1, 4 and 8 bits per pixel using a palette, and 24 and 32 bits per pixel using RGB and RGBA colors.
- Get and set pixels by palette color or RGBA color.
- Palettes with transparent color for indexed images and transparent color for true color images.
- Convert images to 8 bits per pixel with palette or to true color.
- Read and write bitmap images with 1, 4, 8, 24 and 32 bits per pixel.

Read and write PNG images with [Hst Imaging Pngcs](../Hst.Imaging.Pngcs).

## Usage

### Creating images

Create new image with dimension 2 * 2 and 8 bits per pixel:

```
var image = new Image(2, 2, 8);
```

Create new image with dimension 2 * 2 and 32 bits per pixel:

```
var image = new Image(2, 2, 32);
```

Create new image with dimension 2 * 2, 8 bits per pixel, a palette and pixel data:

```
var palette = new Palette(new[] { new Color(0, 0, 0), new Color(255, 0, 0) });
var image = new Image(2, 2, 8, palette, new byte[] { 0, 1, 1, 0 });
```

### Using palette

Add red color to palette:

```
image.Palette.AddColor(new Color(255, 0, 0));
```

Set 3rd color as transparent color for an indexed image:

```
image.Palette.TransparentColor = 2;
```

Set black as transparent color for a 24 or 32 bits per pixel image:

```
image.TransparentColor = new Color(0, 0, 0);
```

### Get and set pixels

Coordinates are zero-based, so x = 1 and y = 1 is the pixel in 2nd column and 2nd row.

Get pixel at coordinate x = 1 and y = 1:

```
var pixel = image.GetPixel(1, 1);
```

Set pixel at coordinate x = 1 and y = 1 to palette's 3rd color:

```
image.SetPixel(1, 1, 2);
```

Setting pixels to palette colors are only supported for 1, 4 and 8 bits per pixel images.

Set pixel at coordinate x = 1 and y = 1 to red color using color instance:

```
image.SetPixel(1, 1, new Color(255, 0, 0));
```

Set pixel at coordinate x = 1 and y = 1 to red color using RGB values:

```
image.SetPixel(1, 1, 255, 0, 0);
```

Setting pixels to color instance or RGBA values are only supported for 24 and 32 bits per pixel images.

Pixel data of 1, 4 and 8 bits per pixel images contains one byte with palette color per pixel. Directly manipulate image with dimension 2 * 2, 8 bits per pixel image pixel data and set pixel at coordinate x = 1 and y = 1 to 2nd palette color:

```
var pixelOffset = 1 * image.Scanline + 1; // y * scanline + x
image.PixelData[pixelOffset] = 1; // 2nd palette color
```

Directly manipulate image with dimension 2 * 2, 24 bits per pixel image pixel data and set pixel at coordinate x = 1 and y = 1 to red color using RGB values:

```
var pixelOffset = 1 * image.Scanline + 1 * 3; // y * scanline + x * 3 bytes per pixel
image.PixelData[pixelOffset] = 255; // red channel
image.PixelData[pixelOffset + 1] = 0; // green channel
image.PixelData[pixelOffset + 2] = 0; // blue channel
```

### Converting images

Convert image to 8 bits per pixel with palette. Image must not have more than 256 colors:

```
var indexedImage = ImageConverter.To8Bpp(image);
```

Convert indexed image to true color, 24 bits per pixel or 32 bits per pixel if image is transparent:

```
var trueColorImage = ImageConverter.ToTrueColor(image);
```

### Reading and writing bitmap images

Bitmap reader and writer supports 1, 4, 8, 24 and 32 bits per pixel images. Compressed bitmaps are not supported.

Only 32 bits per pixel bitmaps support transparency.

Reading a .bmp image:

```
await using var stream = File.OpenRead("test.bmp");
var image = BitmapReader.Read(stream);
```

Writing a .bmp image:

```
await using var stream = File.Create("test.bmp");
BitmapWriter.Write(stream, image);
```

### Reading and writing png images

Reading and writing png images is supported by [Hst Imaging Pngcs](../Hst.Imaging.Pngcs).
