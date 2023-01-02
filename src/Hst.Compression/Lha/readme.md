# Lha

Lha directory contains classes to read and extract Lha archives. It's based on "LHa for UNIX with Autoconf" at https://github.com/jca02266/lha.

Original license and readme files are located in `licenses` directory.

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