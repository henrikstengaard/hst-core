# Hst Imaging Pngcs

Hst Imaging Pngcs is a library for reading and writing PNG images as Hst Imaging images using Hjg.Pngcs.

Features:
- Read indexed PNG images with palette and transparency as indexed images.
- Read grayscale and RGB PNG images with or without alpha as 32 bits per pixel images.
- Write PNG images with 1, 4 and 8 bits per pixel with palette and 24 and 32 bits per pixel.
- Write transparent color for indexed and 24 bits per pixel images.

## Usage

Reading a PNG image:

```
await using var stream = File.OpenRead("test.png");
var image = PngReader.Read(stream);
```

Writing a PNG image:

```
await using var stream = File.Create("test.png");
PngWriter.Write(stream, image);
```
