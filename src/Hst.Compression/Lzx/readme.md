# Lzx

Lzx directory contains classes to read and extract Lzx archives. It's based on "unlzx" at https://aminet.net/misc/unix/unlzx.c.gz written by Erik Meusel and uses a sample lzx archive "xpk_compress.lzx" from http://justsolve.archiveteam.org/wiki/LZX for unit tests.

LZX is both a compression algorithm (of the Lempel-Ziv family) and an archiving program (and file format). The archiving program and compression algorithm were both created by Jonathan Forbes and Tomi Poutanen in Canada, and the archiver was released for the Amiga computer in both shareware and registered versions. When the authors ended support for the program in 1997, they released a key for the registered version so that anybody could use it free.

Original license and readme files are located in `licenses` directory.

## Usage

Example of listing entries in a lzx archive:

```
await using var stream = File.OpenRead("test.lzx");
var lzxArchive = new LzxArchive(stream);
var entries = (await lzxArchive.Entries()).ToList();
```

Example of extracting files from a lzx archive:

```
await using var stream = File.OpenRead("test.lzx");
var lzxArchive = new LzxArchive(stream);

LzxEntry entry;
while ((entry = await lzxArchive.Next()) != null)
{
    await using var output = File.OpenWrite(entry.Name);
    await lzxArchive.Extract(output);
}
```

