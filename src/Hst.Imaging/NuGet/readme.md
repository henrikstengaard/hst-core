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

Read and write PNG images with [Hst Imaging Pngcs](https://www.nuget.org/packages/Hst.Imaging.Pngcs).
