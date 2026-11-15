# Lha

Lha directory contains classes to read, extract and create Lha archives. It's based on "LHa for UNIX with Autoconf" at https://github.com/jca02266/lha.

Original license and readme files are located in `licenses` directory.

## Supported methods

| Method  | Description                                      | Extract | Compress |
|---------|--------------------------------------------------|---------|----------|
| `-lh0-` | Stored, no compression                           | Yes     | Yes      |
| `-lh1-` | 4KB sliding dictionary, dynamic huffman          | Yes     | Yes      |
| `-lh2-` | 8KB sliding dictionary, dynamic huffman          | Yes     | No       |
| `-lh3-` | 8KB sliding dictionary, static huffman           | Yes     | No       |
| `-lh4-` | 4KB sliding dictionary, static huffman           | Yes     | Yes      |
| `-lh5-` | 8KB sliding dictionary, static huffman           | Yes     | Yes      |
| `-lh6-` | 32KB sliding dictionary, static huffman          | Yes     | Yes      |
| `-lh7-` | 64KB sliding dictionary, static huffman          | Yes     | Yes      |
| `-lzs-` | LArc 2KB sliding dictionary                      | Yes     | No       |
| `-lz4-` | LArc stored, no compression                      | Yes     | No       |
| `-lz5-` | LArc 4KB sliding dictionary                      | Yes     | No       |
| `-lhd-` | Directory or symbolic link, no data              | Yes     | Yes      |
| `-pm0-` | PMarc stored, no compression                     | Yes     | No       |
| `-pm2-` | PMarc2 8KB sliding dictionary                    | Yes     | No       |

Compression methods are the same as supported by LHa for UNIX, which produces identical compressed data for `-lh5-`, `-lh6-` and `-lh7-`. If compressed data isn't smaller than the original data, it's stored with method `-lh0-` instead.

Headers level 0, 1, 2 and 3 can be read and headers level 0, 1 and 2 can be written.

PMarc2 (`-pm2-`) decoding is based on pm2.c, pm2hist.c and pm2tree.c by Maarten ter Huurne included in LHa for UNIX.

## History and credits

The original source code has following history and credits for maintainers:
- Original version by Y.Tagawa
- 1991.12.16, Modified by M.Oki
- 1993.10.01, Ver. 1.10 by N.Watazaki: Symbolic Link added
- 1994.08.22, Ver. 1.13b by N.Watazaki: Symbolic Link Bug Fix
- 1995.01.14, Ver. 1.14 by N.Watazaki: Source All changed
- 2000.10.06, Ver. 1.14i by t.okamoto: bug fixed
- 2002.06.29, Ver. 1.14i by Hiroto Sakai: Contributed UTF-8 convertion for Mac OS X                    */
- 2003.02.23, Ver. 1.14i by Koji Arai: autoconfiscated & rewritten

## Usage

Example of extracting files from a lha archive:

```
await using var stream = File.OpenRead("test.lha");
var lhaArchive = new LhaArchive(stream);

foreach (var entry in await lhaArchive.Entries())
{
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
