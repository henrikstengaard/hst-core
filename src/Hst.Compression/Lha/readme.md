# Lha

Lha directory contains classes to read and extract Lha archives. The header reader and decoders are ported from [Lhasa](https://github.com/fragglet/lhasa) by Simon Howard, licensed under the ISC license (see `licenses/lhasa-license.md`).

Creating lha archives and extracting `-lh2-` and `-lh3-` methods is provided by the separate package Hst.Compression.Lha, which is based on LHa for UNIX and distributed under the LHa for UNIX redistribution terms.

## Supported methods

| Method  | Description                                      | Extract                        |
|---------|--------------------------------------------------|--------------------------------|
| `-lh0-` | Stored, no compression                           | Yes                            |
| `-lh1-` | 4KB sliding dictionary, dynamic huffman          | Yes                            |
| `-lh2-` | 8KB sliding dictionary, dynamic huffman          | With Hst.Compression.Lha       |
| `-lh3-` | 8KB sliding dictionary, static huffman           | With Hst.Compression.Lha       |
| `-lh4-` | 4KB sliding dictionary, static huffman           | Yes                            |
| `-lh5-` | 8KB sliding dictionary, static huffman           | Yes                            |
| `-lh6-` | 32KB sliding dictionary, static huffman          | Yes                            |
| `-lh7-` | 64KB sliding dictionary, static huffman          | Yes                            |
| `-lhx-` | UNLHA32 512KB sliding dictionary, static huffman | Yes                            |
| `-lk7-` | LHARK's `-lh7-`                                  | Yes                            |
| `-lzs-` | LArc 2KB sliding dictionary                      | Yes                            |
| `-lz4-` | LArc stored, no compression                      | Yes                            |
| `-lz5-` | LArc 4KB sliding dictionary                      | Yes                            |
| `-lhd-` | Directory or symbolic link, no data              | Yes                            |
| `-pm0-` | PMarc stored, no compression                     | Yes                            |
| `-pm1-` | PMarc 16KB history                               | Yes                            |
| `-pm2-` | PMarc2 8KB sliding dictionary                    | Yes                            |

Headers level 0, 1, 2 and 3 can be read. LHARK archives using `-lh7-` are detected and reported as method `-lk7-`.

Additional methods can be supported by adding decoders implementing `ILhaDecoder` to `LhaOptions.Decoders`.

## Usage

Example of extracting files from a lha archive:

```
await using var stream = File.OpenRead("test.lha");
var lhaArchive = new LhaArchive(stream);

foreach (var entry in await lhaArchive.Entries())
{
    if (entry.Method == Constants.LZHDIRS_METHOD)
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
    lhaArchive.Extract(entry, output);
}
```

Example of opening an entry in a lha archive as a stream, which decompresses data as it's read:

```
await using var stream = File.OpenRead("test.lha");
var lhaArchive = new LhaArchive(stream);

var entry = (await lhaArchive.Entries()).First(x => x.Name == "test.txt");

await using var entryStream = lhaArchive.OpenEntry(entry);
using var reader = new StreamReader(entryStream);
var text = await reader.ReadToEndAsync();
```

The CRC of the extracted data is verified when the end of the entry stream is reached and an `IOException` is thrown, if it doesn't match.
