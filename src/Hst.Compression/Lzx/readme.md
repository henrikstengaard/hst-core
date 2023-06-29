# Lzx

Lzx directory contains classes to read and extract Lzx archives. It's based on "unlzx" at http://aminet.net/misc/unix/unlzx.c.gz.

emeusel at cs.uni-magdeburg.de (Erik Meusel)

http://justsolve.archiveteam.org/wiki/LZX


lzx archive structured like this:
- entry 1
- entry 2
- entry 3
- entry 4
- entry 5
- entry 6
- entry 7
- entry 8
- entry 9
- compressed data for entry 1-9
- entry 10
- entry 11
- entry 12
- entry 13
- entry 14
- entry 15
- entry 16
- entry 17
- entry 18
- compressed data for entry 10-18
- ... and so on

therefore the extract method has to decompress through all compressed data even if just only one entry is extracted.
