# Lha

Lha directory contains classes to read, extract and create Lha archives. The reader, decoders and writer are an independent implementation of the lha file format licensed under the MIT license like the rest of Hst Compression. They don't contain code from LHa for UNIX or Lhasa, and the writer produces different, but compatible compressed data.

The archives in the test data directory `lhasa` are from the test suite of [Lhasa](https://github.com/fragglet/lhasa) and are only used for testing.

## Supported methods

| Method  | Description                                      | Extract | Create |
|---------|--------------------------------------------------|---------|--------|
| `-lh0-` | Stored, no compression                           | Yes     | Yes    |
| `-lh1-` | 4KB sliding dictionary, dynamic huffman          | Yes     | No     |
| `-lh2-` | 8KB sliding dictionary, dynamic huffman          | No      | No     |
| `-lh3-` | 8KB sliding dictionary, static huffman           | No      | No     |
| `-lh4-` | 4KB sliding dictionary, static huffman           | Yes     | Yes    |
| `-lh5-` | 8KB sliding dictionary, static huffman           | Yes     | Yes    |
| `-lh6-` | 32KB sliding dictionary, static huffman          | Yes     | Yes    |
| `-lh7-` | 64KB sliding dictionary, static huffman          | Yes     | Yes    |
| `-lhx-` | UNLHA32 512KB sliding dictionary, static huffman | Yes     | No     |
| `-lk7-` | LHARK's `-lh7-`                                  | Yes     | No     |
| `-lzs-` | LArc 2KB sliding dictionary                      | Yes     | No     |
| `-lz4-` | LArc stored, no compression                      | Yes     | No     |
| `-lz5-` | LArc 4KB sliding dictionary                      | Yes     | No     |
| `-lhd-` | Directory or symbolic link, no data              | Yes     | Yes    |
| `-pm0-` | PMarc stored, no compression                     | Yes     | No     |
| `-pm1-` | PMarc 16KB history                               | Yes     | No     |
| `-pm2-` | PMarc2 8KB sliding dictionary                    | Yes     | No     |

`-lh2-` and `-lh3-` were short-lived experimental methods, which are rarely found in archives.

Headers level 0, 1, 2 and 3 can be read and headers level 0, 1 and 2 can be written. LHARK archives using `-lh7-` are detected and reported as method `-lk7-`.

Additional methods can be supported by adding decoders implementing `ILhaDecoder` to `LhaOptions.Decoders`.

## Usage

Example of extracting files from a lha archive:

```
using var lhaArchive = new LhaArchive(File.OpenRead("test.lha"));

foreach (var entry in await lhaArchive.EntriesAsync())
{
    if (entry.Method == LhaMethod.Lhd)
    {
        Directory.CreateDirectory(entry.Name);
        continue;
    }

    var dirName = Path.GetDirectoryName(entry.Name) ?? string.Empty;

    if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
    {
        Directory.CreateDirectory(dirName);
    }

    await using var output = File.OpenWrite(entry.Name);
    await lhaArchive.ExtractAsync(entry, output);
}
```

Example of opening an entry in a lha archive as a stream, which decompresses data as it's read:

```
using var lhaArchive = new LhaArchive(File.OpenRead("test.lha"));

var entry = (await lhaArchive.EntriesAsync()).First(x => x.Name == "test.txt");

await using var entryStream = await lhaArchive.OpenEntryAsync(entry);
using var reader = new StreamReader(entryStream);
var text = await reader.ReadToEndAsync();
```

The CRC of the extracted data is verified when the end of the entry stream is reached and an `IOException` is thrown, if it doesn't match.

The archive disposes the stream, when it's disposed. Use `new LhaArchive(stream, options, leaveOpen: true)` to keep the stream open. All async methods accept an optional `CancellationToken`.

Example of creating a lha archive with a directory and a file compressed with `-lh5-` using header level 2:

```
await using var stream = File.Create("test.lha");
using var lhaWriter = new LhaWriter(stream, new LhaWriterOptions
{
    Method = LhaMethod.Lh5,
    HeaderLevel = 2
});

lhaWriter.AddDirectory("dir");

await using var data = File.OpenRead("test.txt");
lhaWriter.AddFile("dir/test.txt", data, File.GetLastWriteTime("test.txt"));
```

Data that can't be compressed to a smaller size is stored using method `-lh0-`. The end of archive marker is written, when the lha writer is disposed or `Finish()` is called.

Example of creating an Amiga lha archive with Amiga protection bits (as stored on disk) and comments like Amiga LhA. `LhaWriterOptions.Amiga` writes operating system type Amiga, ISO-8859-1 encoded names and no unix extended headers; comments are stored after a zero character in the name:

```
await using var stream = File.Create("amiga.lha");
using var lhaWriter = new LhaWriter(stream, LhaWriterOptions.Amiga);

lhaWriter.AddDirectory("dir", new LhaEntryOptions { Comment = "Directory comment" });

await using var data = File.OpenRead("test.txt");
lhaWriter.AddFile("dir/test.txt", data, new LhaEntryOptions
{
    LastModified = File.GetLastWriteTime("test.txt"),
    Attributes = 0x02, // delete protected
    Comment = "File comment"
});
```

`LhaWriter.CreateHeader(entry)` creates the bytes of a header for an entry read with `LhaArchive`, e.g. with a changed name, date, attributes or comment, which can be written followed by the entry's unchanged packed data to rewrite an archive without compressing data again.

## Public api

| Type                 | Description                                                                     |
|----------------------|---------------------------------------------------------------------------------|
| `LhaArchive`         | Read entries and extract or open entries as streams.                           |
| `LhaReader`          | Read entries one by one from a stream.                                          |
| `LhaEntry`           | Entry with name, method, sizes, date, crc, attributes and unix metadata.        |
| `LhaEntryStream`     | Stream, which decompresses entry data as it's read and verifies crc at the end. |
| `LhaOptions`         | Encoding of names and additional decoders.                                      |
| `ILhaDecoder`        | Interface for additional decoders created by `LhaDecoderFactory`.               |
| `LhaWriter`          | Create lha archives by adding directories and files.                            |
| `LhaWriterOptions`   | Encoding, header level, method, generic format and os type used for writing.   |
| `LhaEntryOptions`    | Date, method, attributes and comment of an entry added with `LhaWriter`.        |
| `LhaMethod`          | Compression method identifiers, e.g. `LhaMethod.Lh5` for `-lh5-`.               |
| `LhaOsType`          | Operating system types stored in headers, e.g. `LhaOsType.Amiga`.               |
| `LhaUnixMode`        | Unix file type and permission bits of `LhaEntry.UnixMode`.                      |
