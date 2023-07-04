# Hst Imaging

Hst Imaging is a library for creating and manipulating images with basic support for reading and writing image file formats.

Main reason for writing this imaging library is System.Common.Drawing for .NET 6 only supports Windows platform and SixLabors.ImageSharp reads indexed images, but translates palette into RGB.

This makes it difficult to create and manipulate indexed images with a palettes for cross-platform usage.

Features:
- Create and manipulate images.
- Read and write bitmap images.

## Usage

### Creating images

Create new image with dimension 2 * 2 and 8 bits per pixel:

```
var image = new Image(2, 2, 8);
```

Create new image with dimension 2 * 2 and 8 bits per pixel with black color as transparency:

```
var image = new Image(2, 2, 8, true, new Color(0, 0, 0));
```

Black color is added as first color in palette and set as transparent color.

Create new image with dimension 2 * 2 and 32 bits per pixel:

```
var image = new Image(2, 2, 32);
```

### Using palette

Add red color to palette:

```
image.Palette.AddColor(new Color(255, 0, 0));
```

Set 3rd color as transparency color:

```
image.Palette.TransparentColor = 2;
```

### Get and set pixels

Get pixel at coordinate x = 2 and y = 2:

```
var pixel = image.GetPixel(1, 1);
```

Set pixel at coordinate x = 2 and y = 2 to palette's 3rd color:

```
image.SetPixel(1, 1, 2);
```

Setting pixels to palette colors are only supported for 1, 2, 4 and 8 bits per pixel images.

Set pixel at coordinate x = 2 and y = 2 to red color using color instance:

```
image.SetPixel(1, 1, new Color(255, 0, 0));
```

Set pixel at coordinate x = 2 and y = 2 to red color using RGB values:

```
image.SetPixel(1, 1, 255, 0, 0);
```

Setting pixels to color instance or RGBA values are only supported for 24 and 32 bits per pixel images.

Directly manipulate image with dimension 2 * 2, 8 bits per pixel image pixel data and set pixel at coordinate x = 2 and y = 2 to 2nd palette color:

```
var pixelOffset = (1 * 2 + 1) * 1; // y * width + x * 1 byte per pixel
image.PixelData[pixelOffset] = 1; // 2nd palette color
```

Directly manipulate image with dimension 2 * 2, 24 bits per pixel image pixel data and set pixel at coordinate x = 2 and y = 2 to red color using RGB values:

```
var pixelOffset = (1 * 2 + 1) * 3; // y * width + x * 3 bytes per pixel
image.PixelData[pixelOffset] = 255; // red channel
image.PixelData[pixelOffset + 1] = 0; // green channel
image.PixelData[pixelOffset + 2] = 0; // blue channel
```

### Reading and writing png images

Png reader and writer supports 2, 4, 8, 24 and 32 bits per pixel images.

Reading a png image:

```
await using var stream = File.OpenRead("test.png");
var image = PngReader.Read(stream);
```

Writing a png image:

```
await using var stream = File.OpenWrite("test.png");
PngWriter.Write(stream, image);
```

### Reading and writing bitmap images

Bitmap reader and writer supports 1, 4, 8, 24 and 32 bits per pixel images.

Only 32 bits per pixel bitmaps support transparency.

Reading a .bmp image:

```
await using var stream = File.OpenRead("test.bmp");
var image = BmpReader.Read(stream);
```

Writing a .bmp image:

```
await using var stream = File.OpenWrite("test.bmp");
BmpWriter.Write(stream, image);
```