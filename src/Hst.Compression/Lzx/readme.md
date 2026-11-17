# Lzx

Lzx directory contains classes to read and extract Lzx archives. It's based on "unlzx" at https://aminet.net/misc/unix/unlzx.c.gz written by Erik Meusel and uses a sample lzx archive "xpk_compress.lzx" from http://justsolve.archiveteam.org/wiki/LZX for unit tests.

LZX is both a compression algorithm (of the Lempel-Ziv family) and an archiving program (and file format). The archiving program and compression algorithm were both created by Jonathan Forbes and Tomi Poutanen in Canada, and the archiver was released for the Amiga computer in both shareware and registered versions. When the authors ended support for the program in 1997, they released a key for the registered version so that anybody could use it free.

Original license and readme files are located in `licenses` directory.

## Usage

Example of listing entries in a lzx archive:

```
using var lzxArchive = new LzxArchive(File.OpenRead("test.lzx"));
var entries = (await lzxArchive.EntriesAsync()).ToList();
```

Example of extracting files from a lzx archive by iterating entries. This is the fastest way to extract all entries, as data is read sequentially:

```
using var lzxArchive = new LzxArchive(File.OpenRead("test.lzx"));

LzxEntry entry;
while ((entry = await lzxArchive.NextAsync()) != null)
{
    await using var output = File.OpenWrite(entry.Name);
    await lzxArchive.ExtractCurrentAsync(output);
}
```

Example of extracting a single entry from a lzx archive:

```
using var lzxArchive = new LzxArchive(File.OpenRead("test.lzx"));

var entry = (await lzxArchive.EntriesAsync()).First(x => x.Name == "test.txt");

await using var output = File.OpenWrite(entry.Name);
await lzxArchive.ExtractAsync(entry, output);
```


Example of opening an entry in a lzx archive as a stream, which decompresses data as it's read:

```
using var lzxArchive = new LzxArchive(File.OpenRead("test.lzx"));

var entry = (await lzxArchive.EntriesAsync()).First(x => x.Name == "test.txt");

await using var entryStream = await lzxArchive.OpenEntryAsync(entry);
using var reader = new StreamReader(entryStream);
var text = await reader.ReadToEndAsync();
```

The archive disposes the stream, when it's disposed. Use `new LzxArchive(stream, options, leaveOpen: true)` to keep the stream open. All async methods accept an optional `CancellationToken`.
