# Hst Compression Lha

Hst Compression Lha is a library for creating lha archives and extracting -lh2- and -lh3- methods. It's a C# port of parts of "LHa for UNIX with Autoconf" at https://github.com/jca02266/lha and is used together with Hst.Compression, which reads and extracts lha archives.

Hst Compression Lha is distributed under the LHa for UNIX redistribution terms, see [license.txt](license.txt), not the MIT license used by the other Hst Core packages. Original readme files of LHa for UNIX with Autoconf are located in `licenses` directory.

## History and credits

The original source code has following history and credits for maintainers:
- Original version by Y.Tagawa
- 1991.12.16, Modified by M.Oki
- 1993.10.01, Ver. 1.10 by N.Watazaki: Symbolic Link added
- 1994.08.22, Ver. 1.13b by N.Watazaki: Symbolic Link Bug Fix
- 1995.01.14, Ver. 1.14 by N.Watazaki: Source All changed
- 2000.10.06, Ver. 1.14i by t.okamoto: bug fixed
- 2002.06.29, Ver. 1.14i by Hiroto Sakai: Contributed UTF-8 convertion for Mac OS X
- 2003.02.23, Ver. 1.14i by Koji Arai: autoconfiscated & rewritten

Modifications made in Hst Compression Lha are described in [license.txt](license.txt).

## Supported methods

| Method  | Description                                      | Compress | Extract |
|---------|--------------------------------------------------|----------|---------|
| `-lh0-` | Stored, no compression                           | Yes      | No (1)  |
| `-lh1-` | 4KB sliding dictionary, dynamic huffman          | Yes      | No (1)  |
| `-lh2-` | 8KB sliding dictionary, dynamic huffman          | No       | Yes     |
| `-lh3-` | 8KB sliding dictionary, static huffman           | No       | Yes     |
| `-lh4-` | 4KB sliding dictionary, static huffman           | Yes      | No (1)  |
| `-lh5-` | 8KB sliding dictionary, static huffman           | Yes      | No (1)  |
| `-lh6-` | 32KB sliding dictionary, static huffman          | Yes      | No (1)  |
| `-lh7-` | 64KB sliding dictionary, static huffman          | Yes      | No (1)  |
| `-lhd-` | Directory, no data                               | Yes      | No (1)  |

(1) Extracted by Hst.Compression.

Compressed data is identical to LHa for UNIX for `-lh5-`, `-lh6-` and `-lh7-`. If compressed data isn't smaller than the original data, it's stored with method `-lh0-` instead. Headers level 0, 1 and 2 can be written.

## Usage

Example of creating a lha archive with a directory and files compressed with method `-lh5-` and header level 2 (default):

```
await using var stream = File.Create("test.lha");
using var lhaWriter = new LhaWriter(stream, new LhaWriterOptions
{
    Method = Constants.LZHUFF5_METHOD,
    HeaderLevel = 2
});

lhaWriter.AddDirectory("dir", DateTime.Now);

await using var data = File.OpenRead("test.txt");
lhaWriter.AddFile(@"dir\test.txt", data, File.GetLastWriteTime("test.txt"));

// end of archive mark is written when lha writer is disposed or finished
lhaWriter.Finish();
```

Example of extracting a lha archive with -lh2- and -lh3- methods by adding decoders to options:

```
await using var stream = File.OpenRead("test.lzh");
var lhaArchive = new LhaArchive(stream, new LhaOptions().AddLhaForUnixDecoders());

foreach (var entry in await lhaArchive.Entries())
{
    await using var output = File.OpenWrite(entry.Name);
    lhaArchive.Extract(entry, output);
}
```
