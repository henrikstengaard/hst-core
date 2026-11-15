# Lha

Lha directory contains classes to read, extract and create Lha archives. The header reader and decoders are ported from [Lhasa](https://github.com/fragglet/lhasa) by Simon Howard, licensed under the ISC license (see `licenses/lhasa-license.md`).

The writer (`LhaWriter`, `HeaderWriter`, `LhEncoder`, `HuffmanCode` and `BitStreamWriter`) is an independent implementation written from the lha file format and the bitstream read by the Lhasa decoders. It's not derived from LHa for UNIX and produces different, but compatible compressed data.

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

`-lh2-` and `-lh3-` were short-lived experimental methods, which are rarely found in archives and aren't supported by Lhasa either.

Headers level 0, 1, 2 and 3 can be read and headers level 0, 1 and 2 can be written. LHARK archives using `-lh7-` are detected and reported as method `-lk7-`.

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

Example of creating a lha archive with a directory and a file compressed with `-lh5-` using header level 2:

```
await using var stream = File.Create("test.lha");
using var lhaWriter = new LhaWriter(stream, new LhaWriterOptions
{
    Method = Constants.LZHUFF5_METHOD,
    HeaderLevel = 2
});

lhaWriter.AddDirectory("dir");

await using var data = File.OpenRead("test.txt");
lhaWriter.AddFile("dir/test.txt", data, File.GetLastWriteTime("test.txt"));
```

Data that can't be compressed to a smaller size is stored using method `-lh0-`. The end of archive marker is written, when the lha writer is disposed or `Finish()` is called.
