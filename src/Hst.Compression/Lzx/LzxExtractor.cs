namespace Hst.Compression.Lzx
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class LzxExtractor
    {
        private readonly Stream stream;
        private readonly byte[] read_buffer;
        private readonly byte[] decrunch_buffer;
        private readonly HuffmanTable huffmanTable;
        private int source_pos { get; set; }
        private int source_end { get; set; }
        private int destination_pos { get; set; }
        private int destination_end { get; set; }
        private int pos { get; set; }
        private int pack_size { get; set; }

        public LzxExtractor(Stream stream)
        {
            this.stream = stream;
            this.read_buffer = new byte[16384];
            this.decrunch_buffer = new byte[258+65536+258];
            this.huffmanTable = new HuffmanTable(0U, -16, 1U);
            this.source_pos = 0;
            this.source_end = 0;
            this.destination_pos = 0;
            this.destination_end = 0;
            this.pos = 0;
            this.pack_size = 0;
        }

        public void Reset(LzxMergedChunk mergedChunk)
        {
            for (var i = 0; i < this.read_buffer.Length;i++)
            {
                this.read_buffer[i] = 0;
            }
            for (var i = 0; i < this.decrunch_buffer.Length;i++)
            {
                this.decrunch_buffer[i] = 0;
            }

            this.huffmanTable.Reset(0U, -16, 1U);
            this.source_pos = 16384;
            this.source_end = source_pos - 1024;
            this.destination_end = destination_pos = 258 + 65536;
            this.pos = destination_end;
            this.pack_size = mergedChunk?.PackSize ?? 0;
        }

        public async Task ExtractStore(LzxEntry entry, Stream entryStream)
        {
            var dataCrc = 0;
            var unpackSize = entry.UnpackedSize;
            var buffer = new byte[16384];

            while (unpackSize > 0)
            {
                var count = unpackSize > buffer.Length ? buffer.Length : unpackSize;
                var bytesRead = await stream.ReadAsync(buffer, 0, count);
                if (bytesRead != count)
                {
                    throw new IOException("Failed to read entry data");
                }

                dataCrc = CrcHelper.crc_calc(buffer, 0, count, dataCrc);
                
                await entryStream.WriteAsync(buffer, 0, bytesRead);
                
                unpackSize -= count;
            }
            
            if (entry.DataCrc != dataCrc)
            {
                throw new IOException("Failed to read entry data: Invalid crc");
            }
        }

        public async Task ExtractNormal(LzxEntry entry, Stream entryStream)
        {
            var sum = 0; /* reset CRC */

            var unpackSize = entry.UnpackedSize;

            while (unpackSize > 0)
            {
                int count;
                if (pos == destination_pos) /* time to fill the buffer? */
                {
                    /* check if we have enough data and read some if not */
                    if (source_pos >= source_end) /* have we exhausted the current read buffer? */
                    {
                        var temp = PrepareReadBuffer(out count);

                        var bytesRead = await stream.ReadAsync(read_buffer, temp, count);
                        if (bytesRead != count)
                        {
                            throw new IOException("Failed to read data");
                        }

                        pack_size -= count;
                        temp += count;
                        if (source_pos >= temp) break; /* argh! no more data! */
                    } /* if(source >= source_end) */

                    DecrunchBlock();
                }
                
                /* calculate amount of data we can use before we need to fill the buffer again */
                count = destination_pos - pos;
                if (count > unpackSize) count = unpackSize; /* take only what we need */

                sum = CrcHelper.crc_calc(decrunch_buffer, pos, count, sum);

                if (entryStream != null)
                {
                    await entryStream.WriteAsync(decrunch_buffer, pos, count);
                }

                unpackSize -= count;
                pos += count;
            }

            if (sum != entry.DataCrc)
            {
                throw new IOException("Crc error");
            }
        }

        /// <summary>
        /// Read up to count bytes of stored data into buffer
        /// </summary>
        /// <returns>Bytes read</returns>
        public int ReadStore(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;
            while (bytesRead < count)
            {
                var read = stream.Read(buffer, offset + bytesRead, count - bytesRead);
                if (read == 0)
                {
                    throw new IOException("Failed to read entry data");
                }

                bytesRead += read;
            }

            return bytesRead;
        }

        /// <summary>
        /// Decrunch up to count bytes of normal packed data into buffer
        /// </summary>
        /// <returns>Bytes read, 0 if no more data</returns>
        public int ReadNormal(byte[] buffer, int offset, int count)
        {
            if (pos == destination_pos) /* time to fill the buffer? */
            {
                /* check if we have enough data and read some if not */
                if (source_pos >= source_end) /* have we exhausted the current read buffer? */
                {
                    var temp = PrepareReadBuffer(out var readCount);

                    var bytesRead = 0;
                    while (bytesRead < readCount)
                    {
                        var read = stream.Read(read_buffer, temp + bytesRead, readCount - bytesRead);
                        if (read == 0)
                        {
                            throw new IOException("Failed to read data");
                        }

                        bytesRead += read;
                    }

                    pack_size -= readCount;
                    temp += readCount;
                    if (source_pos >= temp) return 0; /* argh! no more data! */
                } /* if(source >= source_end) */

                DecrunchBlock();
            }

            /* calculate amount of data we can use before we need to fill the buffer again */
            var length = destination_pos - pos;
            if (length > count) length = count; /* take only what we need */

            Array.Copy(decrunch_buffer, pos, buffer, offset, length);
            pos += length;

            return length;
        }

        /// <summary>
        /// Copy remaining overrun to the start of read buffer and calculate count of bytes to read into it
        /// </summary>
        /// <param name="count">Count of bytes to read into read buffer</param>
        /// <returns>Read buffer offset to read bytes into</returns>
        private int PrepareReadBuffer(out int count)
        {
            var temp = 0;
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

            return temp;
        }

        /// <summary>
        /// Read huffman tables, if needed, and unpack data from read buffer into decrunch buffer
        /// </summary>
        private void DecrunchBlock()
        {
            int count;
            int temp;

            /* check if we need to read the tables */
            int sourcePosRef;
            if (huffmanTable.decrunch_length <= 0)
            {
                sourcePosRef = source_pos;
                huffmanTable.read_literal_table(read_buffer, ref sourcePosRef);// break; /* argh! can't make huffman tables! */
                source_pos = sourcePosRef;
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
            {
                destination_end = 258 + 65536;
            }
            temp = destination_pos;

            sourcePosRef = source_pos;
            var destinationPosRef = destination_pos;
            huffmanTable.decrunch(read_buffer, ref sourcePosRef, source_end, decrunch_buffer, 
                ref destinationPosRef, destination_end);
            source_pos = sourcePosRef;
            destination_pos = destinationPosRef;

            huffmanTable.decrunch_length -= (uint)(destination_pos - temp);
        }
    }
}