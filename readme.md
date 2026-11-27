# Hst Core

Hst Core is a collection of .NET libraries with common classes, compression and imaging used by Hst Imager, Hst Amiga and other Hst projects.

All libraries support .NET Standard 2.1, .NET 8, .NET 9 and .NET 10 and are available as NuGet packages.

| Library | NuGet package | Description |
|---------|---------------|-------------|
| [Hst Core](src/Hst.Core) | [Hst.Core](https://www.nuget.org/packages/Hst.Core) | Common classes and extensions. |
| [Hst Compression](src/Hst.Compression) | [Hst.Compression](https://www.nuget.org/packages/Hst.Compression) | Read, extract and create lha and lzx archives. |
| [Hst Imaging](src/Hst.Imaging) | [Hst.Imaging](https://www.nuget.org/packages/Hst.Imaging) | Create and manipulate indexed and true color images. |
| [Hst Imaging Pngcs](src/Hst.Imaging.Pngcs) | [Hst.Imaging.Pngcs](https://www.nuget.org/packages/Hst.Imaging.Pngcs) | Read and write PNG images. |

## Hst Core

Features:
- `Result` and `Result<T>` with `Error` to return a value or an error without throwing exceptions.
- Stream extensions to read bytes, read and write big-endian 16 and 32 bit integers and find a byte pattern in a stream. Reads handle partial reads and throw `EndOfStreamException` when the stream ends early.
- Big-endian, little-endian and signed byte converters for integer values and byte arrays.
- Layered stream that writes changes to a layer stream on top of a base stream, which can be flushed to the base stream or discarded.
- Cached stream that caches blocks of an underlying stream for reads and writes.
- Block memory stream that represents a large stream in memory by storing only blocks with data written to them.
- Hybrid stream that switches from one stream to another when a size threshold is reached, for example from memory to a file.
- Process extensions to run a process and return its standard output, with cancellation that kills the process.
- Operating system helpers to detect Windows, macOS and Linux and check whether the process runs as administrator.
- Size extensions for KB, MB and GB, and rounding sizes to sector size or a universal size that fits CF/SD cards and SSDs of different brands.
- Array, byte array and format extensions to slice, chunk, copy, search and format bytes as hex.

See more at [Hst Core](src/Hst.Core) section.

## Hst Compression

Features:
- Read and extract lha archives with methods -lh0-, -lh1-, -lh4- to -lh7-, -lhx-, -lk7-, -lzs-, -lz4-, -lz5-, -pm0-, -pm1- and -pm2-.
- Read lha archives with header levels 0, 1, 2 and 3.
- Create lha archives with methods -lh0-, -lh4-, -lh5-, -lh6- and -lh7- and header levels 0, 1 and 2.
- Create Amiga lha archives with protection bits and comments, and create headers for changed entries to rewrite archives without compressing their data again.
- Support additional lha methods by adding custom decoders.
- Read and extract lzx archives, including merged entries.
- List archive entries with name, date, sizes, attributes and lzx comments.
- Extract an entry to a stream or open it as a read-only stream.
- Verify CRC of extracted data and throw `IOException` on CRC errors.
- Async methods with cancellation tokens and option to leave the archive stream open.
- Configurable text encoding for entry names.

See more at [Hst Compression](src/Hst.Compression) section.

## Hst Imaging

Features:
- Create images with 1, 4 and 8 bits per pixel using a palette, and 24 and 32 bits per pixel using RGB and RGBA colors.
- Get and set pixels by palette color or RGBA color.
- Palettes with transparent color for indexed images and transparent color for true color images.
- Convert images to 8 bits per pixel with palette or to true color.
- Read and write bitmap images with 1, 4, 8, 24 and 32 bits per pixel.

See more at [Hst Imaging](src/Hst.Imaging) section.

## Hst Imaging Pngcs

Features:
- Read indexed PNG images with palette and transparency as indexed images.
- Read grayscale and RGB PNG images with or without alpha as 32 bits per pixel images.
- Write PNG images with 1, 4 and 8 bits per pixel with palette and 24 and 32 bits per pixel.
- Write transparent color for indexed and 24 bits per pixel images.

See more at [Hst Imaging Pngcs](src/Hst.Imaging.Pngcs) section.

## Licenses

Hst Core, Hst Compression, Hst Imaging and Hst Imaging Pngcs are licensed under the MIT license, see [license.txt](license.txt). Hst Compression includes code based on unlzx, see [THIRD-PARTY-NOTICES.txt](src/Hst.Compression/THIRD-PARTY-NOTICES.txt).
