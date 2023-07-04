# Hst Imaging

Hst Imaging is a library for creating and manipulating images with basic support for reading and writing image file formats.

Main reason for writing this imaging library is System.Common.Drawing for .NET 6 only supports Windows platform and SixLabors.ImageSharp reads indexed images, but translates palette into RGB.

This makes it difficult to create and manipulate indexed images with a palettes for cross-platform usage.

Features:
- Create and manipulate images.
- Read and write bitmap images.