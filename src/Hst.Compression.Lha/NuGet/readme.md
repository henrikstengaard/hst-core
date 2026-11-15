# Hst Compression Lha

Hst Compression Lha is a library for creating lha archives and extracting -lh2- and -lh3- methods. It's a C# port of parts of LHa for UNIX with Autoconf (https://github.com/jca02266/lha) and is used together with Hst.Compression, which reads and extracts lha archives.

Features:
- Create lha archives with methods -lh0-, -lh1-, -lh4-, -lh5-, -lh6- and -lh7- and header levels 0, 1 and 2. Compressed data is identical to LHa for UNIX.
- Extract -lh2- and -lh3- methods by adding decoders to Hst.Compression lha options with `AddLhaForUnixDecoders()`.

## License

Hst Compression Lha is not licensed under the MIT license. It's distributed under the LHa for UNIX redistribution terms, see license.txt, which include:
- Copyright notices must not be removed.
- Binary-only distribution is not permitted.
- Programs incorporating it must not call themselves LHa.
- Commercial use in which it's the main part is prohibited and commercial users must provide support.

Please read license.txt before using Hst Compression Lha.
