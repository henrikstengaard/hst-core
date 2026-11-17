# Hst Compression

Hst Compression is a library for reading, extracting and creating compression file formats.

Features:
- Read and extract lha archives with methods -lh0-, -lh1-, -lh4- to -lh7-, -lhx-, -lk7-, -lzs-, -lz4-, -lz5-, -pm0-, -pm1- and -pm2-.
- Read lha archives with header levels 0, 1, 2 and 3.
- Create lha archives with methods -lh0-, -lh4-, -lh5-, -lh6- and -lh7- and header levels 0, 1 and 2.
- Support additional lha methods by adding custom decoders.
- Read and extract lzx archives, including merged entries.
- List archive entries with name, date, sizes, attributes and lzx comments.
- Extract an entry to a stream or open it as a read-only stream.
- Verify CRC of extracted data and throw `IOException` on CRC errors.
- Async methods with cancellation tokens and option to leave the archive stream open.
- Configurable text encoding for entry names.

Hst Compression is licensed under the MIT license. The lzx extractor is based on unlzx, see THIRD-PARTY-NOTICES.txt.

See [Lha](Lha) and [Lzx](Lzx) for usage examples.
