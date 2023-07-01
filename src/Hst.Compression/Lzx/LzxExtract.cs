namespace Hst.Compression.Lzx
{
    using System.IO;

    public static class LzxExtract
    {
        public static void ExtractStore(LzxArchive lzxArchive, Stream entryStream)
        {
            //lzxArchiveStream.Position = entry.Offset;
            var entry = lzxArchive.currentEntry;
            var dataCrc = 0;
            var unpackSize = entry.UnpackedSize;
            var buffer = new byte[16384];

            while (unpackSize > 0)
            {
                var count = unpackSize > buffer.Length ? buffer.Length : unpackSize;
                var bytesRead = lzxArchive.stream.Read(buffer, 0, count);
                if (bytesRead != count)
                {
                    throw new IOException("Failed to read entry data");
                }

                dataCrc = CrcHelper.crc_calc(buffer, 0, count, dataCrc);
                
                entryStream.Write(buffer, 0, bytesRead);
                
                unpackSize -= count;
            }
            
            if (entry.DataCrc != dataCrc)
            {
                throw new IOException("Failed to read entry data: Invalid crc");
            }
        }

        public static void ExtractNormal(LzxArchive lzxArchive, Stream entryStream)
        {
            // var global_control = 0U; /* initial control word */
            // var global_shift = -16;
            // var last_offset = 1U;
            // var huffmanTable = new HuffmanTable(0U, -16, 1U);
            
            //var unpack_size = 0U;
            //var decrunch_length = 0U;

            // var read_buffer = new byte[16384];
            // var decrunch_buffer = new byte[258+65536+258];
            
            // char decrunch_buffer[258+65536+258]
            //var pack_size = entry.PackedSize;
            
            int source_pos;
            int destination_pos;
            //var pos = lzxArchive.destination_end;

            var entry = lzxArchive.currentEntry;
            var sum = 0; /* reset CRC */

            var unpack_size = entry.UnpackedSize;
            var count = 0;
            var temp = 0;

            while (unpack_size > 0)
            {
                if (lzxArchive.pos == lzxArchive.destination_pos) /* time to fill the buffer? */
                {
                    /* check if we have enough data and read some if not */
                    if (lzxArchive.source_pos >= lzxArchive.source_end) /* have we exhausted the current read buffer? */
                    {
                        temp = 0;                           
                        count = temp - lzxArchive.source_pos + 16384;
                        if (count > 0)
                        {
                            do /* copy the remaining overrun to the start of the buffer */
                            {
                                lzxArchive.read_buffer[temp++] = lzxArchive.read_buffer[lzxArchive.source_pos++];
                            } while (--count > 0);
                        }

                        lzxArchive.source_pos = 0;
                        count = lzxArchive.source_pos - temp + 16384;

                        if (lzxArchive.pack_size < count) count = lzxArchive.pack_size; /* make sure we don't read too much */

                        var bytesRead = lzxArchive.stream.Read(lzxArchive.read_buffer, 0, count);
                        if (bytesRead != count)
                        {
                            throw new IOException("Failed to read data");
                        }

                        lzxArchive.pack_size -= count;
                        temp += count;
                        if (lzxArchive.source_pos >= temp) break; /* argh! no more data! */
                    } /* if(source >= source_end) */

                    /* check if we need to read the tables */
                    if (lzxArchive.huffmanTable.decrunch_length <= 0)
                    {
                        source_pos = lzxArchive.source_pos;
                        lzxArchive.huffmanTable.read_literal_table(lzxArchive.read_buffer, ref source_pos);// break; /* argh! can't make huffman tables! */
                        lzxArchive.source_pos = source_pos;
                    }

                    /* unpack some data */
                    if (lzxArchive.destination_pos >= 258 + 65536)
                    {
                        if ((count = lzxArchive.destination_pos - 65536) > 0)
                        {
                            lzxArchive.destination_pos = 0;
                            temp = lzxArchive.destination_pos + 65536;
                            do /* copy the overrun to the start of the buffer */
                            {
                                lzxArchive.decrunch_buffer[lzxArchive.destination_pos++] = lzxArchive.decrunch_buffer[temp++];
                            } while (--count > 0);
                        }

                        lzxArchive.pos = lzxArchive.destination_pos;
                    }

                    lzxArchive.destination_end = (int)(lzxArchive.destination_pos + lzxArchive.huffmanTable.decrunch_length);
                    if (lzxArchive.destination_end > 258 + 65536)
                        lzxArchive.destination_end = 258 + 65536;
                    temp = lzxArchive.destination_pos;

                    source_pos = lzxArchive.source_pos;
                    destination_pos = lzxArchive.destination_pos;
                    lzxArchive.huffmanTable.decrunch(lzxArchive.read_buffer, ref source_pos, lzxArchive.source_end, 
                        lzxArchive.decrunch_buffer, ref destination_pos, lzxArchive.destination_end);
                    lzxArchive.source_pos = source_pos;
                    lzxArchive.destination_pos = destination_pos;

                    lzxArchive.huffmanTable.decrunch_length -= (uint)(lzxArchive.destination_pos - temp);
                }
                
                /* calculate amount of data we can use before we need to fill the buffer again */
                count = lzxArchive.destination_pos - lzxArchive.pos;
                if (count > unpack_size) count = unpack_size; /* take only what we need */

                sum = CrcHelper.crc_calc(lzxArchive.decrunch_buffer, lzxArchive.pos, count, sum);

                entryStream.Write(lzxArchive.decrunch_buffer, lzxArchive.pos, count);

                unpack_size -= count;
                lzxArchive.pos += count;
            }

            if (sum != entry.DataCrc)
            {
                throw new IOException("Crc error");
            }
        }
    }
}