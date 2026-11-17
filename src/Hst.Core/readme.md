# Hst Core

Hst Core is a library with common classes and extensions used by Hst Imager, Hst Amiga and other Hst projects.

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

Supports .NET Standard 2.1, .NET 8, .NET 9 and .NET 10.

See [IO](IO) for details on the layered stream format.
