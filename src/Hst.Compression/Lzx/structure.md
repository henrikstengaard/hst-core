# Structure

Lzx archives consists of 3 chunks:
- Info header
- Archive header
- Data

Chunks in lzx archive are structured like this:
- Info header
- Archive header 1
- Archive header 2
- Archive header 3
- Archive header 4
- Archive header 5
- Data for Archive header 1-5
- Archive header 6
- Archive header 7
- Archive header 8
- Archive header 9
- Archive header 10
- Data for Archive header 6-10
- ... and so on

The data chunks contain multiple archive headers data merged one after each other.
Therefore uncompressing the data for each archive header must uncompress through all data even if just only one archive header is extracted.

## Info header

Info header chunk is 10 bytes.

| Offset | Data type | Name           | Comment             |
|--------|-----------|----------------|---------------------|
| 0x000  | ULONG     | File signature | 76 90 88 00 = "LZX" |

## Archive header

Archive header chunk is 31 bytes.

| Offset               | Data type               | Name            | Comment                                   |
|----------------------|-------------------------|-----------------|-------------------------------------------|
| 0x000                | UCHAR                   | Attributes      | File protection modes: HSPARWED           |
| 0x002                | ULONG                   | Unpack size     | Size of entry unpacked                    |
| 0x006                | ULONG                   | Pack size       | Size of entry packed                      |
| 0x00b                | UCHAR                   | Pack mode       | 0 = Store, 2 = Normal                     |
| 0x00c                | UCHAR                   | Merged          | 1 = Merged                                |
| 0x00e                | UCHAR                   | Comment length  |                                           |
| 0x012                | ULONG                   | Date            |                                           |
| 0x016                | ULONG                   | Data CRC        | Crc for data uncompressed                 |
| 0x01a                | ULONG                   | Header CRC      | Crc for archive header                    |
| 0x01e                | UCHAR                   | Filename length |                                           |
| 0x01f                | UCHAR * Filename length | Filename        |                                           |
| 0x01f + Filename length | UCHAR * Comment length  | Comment         | Present, if comment length greater than 0 |

