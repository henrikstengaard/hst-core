namespace Hst.Compression.Lzx
{
    using System.IO;

    public static class LzxExtract
    {
        public static void Extract(LzxArchive lzxArchive, LzxEntry entry, Stream entryStream)
        {
            if (entry.PackedSize == 0)
            {
                return;
            }
            switch (entry.PackMode)
            {
                case 0:
                    ExtractStore(lzxArchive.stream, entry, entryStream);
                    break;
                case 2:
                    ExtractNormal(lzxArchive, entry, entryStream);
                    break;
                default:
                    throw new IOException($"Unknown pack mode {entry.PackMode} for entry '{entry.Name}'");
            }
        }

        private static void ExtractStore(Stream lzxArchiveStream, LzxEntry entry, Stream entryStream)
        {
            lzxArchiveStream.Position = entry.Offset;
            var dataCrc = 0;
            var unpackSize = entry.UnpackedSize;
            var buffer = new byte[16384];

            while (unpackSize > 0)
            {
                var count = unpackSize > buffer.Length ? buffer.Length : unpackSize;
                var bytesRead = lzxArchiveStream.Read(buffer, 0, count);
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

        private static void ExtractNormal(LzxArchive lzxArchive, LzxEntry entry, Stream entryStream)
        {
            lzxArchive.stream.Position = entry.Offset;
            
            // var global_control = 0U; /* initial control word */
            // var global_shift = -16;
            // var last_offset = 1U;
            var huffmanTable = new HuffmanTable(0U, -16, 1U);
            
            //var unpack_size = 0U;
            //var decrunch_length = 0U;

            var read_buffer = new byte[16384];
            var decrunch_buffer = new byte[258+65536+258];
            
            // char decrunch_buffer[258+65536+258]
            var pack_size = entry.PackedSize;
            
            var source_pos = 0;
            var source_end = source_pos = 16384 - 1024;
            var destination_pos = 258 + 65536;
            var destination_end = destination_pos;
            var pos = destination_end;

            var sum = 0; /* reset CRC */

            var unpack_size = entry.UnpackedSize;
            var count = 0;
            var temp = 0;

            while (unpack_size > 0)
            {
                if (pos == destination_pos) /* time to fill the buffer? */
                {
                    /* check if we have enough data and read some if not */
                    if (source_pos >= source_end) /* have we exhausted the current read buffer? */
                    {
                        temp = 0;
                        count = temp - source_pos + 16384;
                        if (count > 0)
                        {
                            do /* copy the remaining overrun to the start of the buffer */
                            {
                                read_buffer[temp++] = read_buffer[source_pos++];
                            } while (--count > 0);
                        }

                        source_pos = 0;
                        count = source_pos - temp + 16384;

                        if (pack_size < count) count = pack_size; /* make sure we don't read too much */

                        var bytesRead = lzxArchive.stream.Read(read_buffer, 0, count);
                        if (bytesRead != count)
                        {
                            throw new IOException("Failed to read data");
                        }

                        pack_size -= count;
                        temp += count;
                        if (source_pos >= temp) break; /* argh! no more data! */
                    } /* if(source >= source_end) */

                    /* check if we need to read the tables */
                    if (huffmanTable.decrunch_length <= 0)
                    {
                        huffmanTable.read_literal_table(read_buffer, ref source_pos);// break; /* argh! can't make huffman tables! */
                    }

                    /* unpack some data */
                    if (destination_pos >= 258 + 65536)
                    {
                        if ((count = destination_pos - 65536) > 0)
                        {
                            destination_pos = 0;
                            temp = destination_pos + 65536;
                            do /* copy the overrun to the start of the buffer */
                            {
                                decrunch_buffer[destination_pos++] = decrunch_buffer[temp++];
                            } while (--count > 0);
                        }

                        pos = destination_pos;
                    }

                    destination_end = (int)(destination_pos + huffmanTable.decrunch_length);
                    if (destination_end > 258 + 65536)
                        destination_end = 258 + 65536;
                    temp = destination_pos;

                    huffmanTable.decrunch(read_buffer, ref source_pos, source_end, decrunch_buffer, ref destination_pos,
                        destination_end);

                    huffmanTable.decrunch_length -= (uint)(destination_pos - temp);
                }
                
                /* calculate amount of data we can use before we need to fill the buffer again */
                count = destination_pos - pos;
                if (count > unpack_size) count = unpack_size; /* take only what we need */

                sum = CrcHelper.crc_calc(decrunch_buffer, pos, count, sum);

                entryStream.Write(decrunch_buffer, pos, count);

                unpack_size -= count;
                pos += count;
            }

            // using (var dump = File.OpenWrite(entry.Name))
            // {
            //     entryStream.Position = 0;
            //     entryStream.CopyTo(dump);
            // }
            
            // if (sum != entry.DataCrc)
            // {
            //     throw new IOException("Crc error");
            // }
        }
        
    }
}