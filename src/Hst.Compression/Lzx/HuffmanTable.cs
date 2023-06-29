namespace Hst.Compression.Lzx
{
    using System.IO;

    public class HuffmanTable
    {
        private static readonly uint[] table_one =
        {
            0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14
        };

        private static readonly uint[] table_two =
        {
            0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512, 768, 1024,
            1536, 2048, 3072, 4096, 6144, 8192, 12288, 16384, 24576, 32768, 49152
        };

        private static readonly uint[] table_three =
        {
            0, 1, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767
        };

        private static readonly byte[] table_four =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16
        };

        private uint decrunch_method;
        public uint decrunch_length { get; set; }
        private uint last_offset;
        private uint global_control;
        private int global_shift;

        private byte[] offset_len;
        private ushort[] offset_table;
        private byte[] huffman20_len;
        private ushort[] huffman20_table;
        private byte[] literal_len;
        private ushort[] literal_table;

        public HuffmanTable(uint global_control, int global_shift, uint last_offset)
        {
            this.global_control = global_control;
            this.global_shift = global_shift;
            this.last_offset = last_offset;
            offset_len = new byte[8];
            offset_table = new ushort[128];
            huffman20_len = new byte[20];
            huffman20_table = new ushort[96];
            literal_len = new byte[768];
            literal_table = new ushort[5120];
        }

/* Read and build the decrunch tables. There better be enough data in the */
/* source buffer or it's stuffed. */

        public void read_literal_table(byte[] source, ref int sourcePos)
        {
            // register unsigned int control;
            // register int shift;
            uint temp; /* could be a register */
            uint symbol, pos, count, fix, max_symbol;
            //int abort = 0;

            var control = global_control;
            var shift = global_shift;

            if (shift < 0) /* fix the control word if necessary */
            {
                shift += 16;
                control += (uint)(source[sourcePos++] << (8 + shift));
                control += (uint)(source[sourcePos++] << shift);
            }

/* read the decrunch method */

            decrunch_method = control & 7;
            control >>= 3;
            if ((shift -= 3) < 0)
            {
                shift += 16;
                control += (uint)(source[sourcePos++] << (8 + shift));
                control += (uint)(source[sourcePos++] << shift);
            }

/* Read and build the offset huffman table */

            if (decrunch_method == 3)
            {
                for (temp = 0; temp < 8; temp++)
                {
                    offset_len[temp] = (byte)(control & 7);
                    control >>= 3;
                    if ((shift -= 3) < 0)
                    {
                        shift += 16;
                        control += (uint)(source[sourcePos++] << (8 + shift));
                        control += (uint)(source[sourcePos++] << shift);
                    }
                }

                make_decode_table(8, 7, offset_len, offset_table);
            }

            /* read decrunch length */

            decrunch_length = (control & 255) << 16;
            control >>= 8;
            if ((shift -= 8) < 0)
            {
                shift += 16;
                control += (uint)(source[sourcePos++] << (8 + shift));
                control += (uint)(source[sourcePos++] << shift);
            }

            decrunch_length += (control & 255) << 8;
            control >>= 8;
            if ((shift -= 8) < 0)
            {
                shift += 16;
                control += (uint)(source[sourcePos++] << (8 + shift));
                control += (uint)(source[sourcePos++] << shift);
            }

            decrunch_length += (control & 255);
            control >>= 8;
            if ((shift -= 8) < 0)
            {
                shift += 16;
                control += (uint)(source[sourcePos++] << (8 + shift));
                control += (uint)(source[sourcePos++] << shift);
            }

/* read and build the huffman literal table */

            if (decrunch_method != 1)
            {
                pos = 0;
                fix = 1;
                max_symbol = 256;

                do
                {
                    for (temp = 0; temp < 20; temp++)
                    {
                        huffman20_len[temp] = (byte)(control & 15);
                        control >>= 4;
                        if ((shift -= 4) < 0)
                        {
                            shift += 16;
                            control += (uint)(source[sourcePos++] << (8 + shift));
                            control += (uint)(source[sourcePos++] << shift);
                        }
                    }

                    make_decode_table(20, 6, huffman20_len, huffman20_table);

                    do
                    {
                        if ((symbol = huffman20_table[control & 63]) >= 20)
                        {
                            do /* symbol is longer than 6 bits */
                            {
                                symbol = huffman20_table[((control >> 6) & 1) + (symbol << 1)];
                                // if (!shift--) // if (!value) = if zero; if (value) = if not zero
                                if (shift-- == 0)
                                {
                                    shift += 16;
                                    control += (uint)(source[sourcePos++] << 24);
                                    control += (uint)(source[sourcePos++] << 16);
                                }

                                control >>= 1;
                            } while (symbol >= 20);

                            temp = 6;
                        }
                        else
                        {
                            temp = huffman20_len[symbol];
                        }

                        control >>= (int)temp;
                        if ((shift -= (int)temp) < 0)
                        {
                            shift += 16;
                            control += (uint)(source[sourcePos++] << (8 + shift));
                            control += (uint)(source[sourcePos++] << shift);
                        }

                        switch (symbol)
                        {
                            case 17:
                            case 18:
                            {
                                if (symbol == 17)
                                {
                                    temp = 4;
                                    count = 3;
                                }
                                else /* symbol == 18 */
                                {
                                    temp = 6 - fix;
                                    count = 19;
                                }

                                count += (control & table_three[temp]) + fix;
                                control >>= (int)temp;
                                if ((shift -= (int)temp) < 0)
                                {
                                    shift += 16;
                                    control += (uint)(source[sourcePos++] << (8 + shift));
                                    control += (uint)(source[sourcePos++] << shift);
                                }

                                while (pos < max_symbol && count-- > 0)
                                    literal_len[pos++] = 0;
                                break;
                            }
                            case 19:
                            {
                                count = (control & 1) + 3 + fix;
                                // if (!shift--) // if (!value) = if zero; if (value) = if not zero
                                if (shift-- == 0)
                                {
                                    shift += 16;
                                    control += (uint)(source[sourcePos++] << 24);
                                    control += (uint)(source[sourcePos++] << 16);
                                }

                                control >>= 1;
                                if ((symbol = huffman20_table[control & 63]) >= 20)
                                {
                                    do /* symbol is longer than 6 bits */
                                    {
                                        symbol = huffman20_table[((control >> 6) & 1) + (symbol << 1)];
                                        // if (!shift--) // if (!value) = if zero; if (value) = if not zero
                                        if (shift-- == 0)
                                        {
                                            shift += 16;
                                            control += (uint)(source[sourcePos++] << 24);
                                            control += (uint)(source[sourcePos++] << 16);
                                        }

                                        control >>= 1;
                                    } while (symbol >= 20);

                                    temp = 6;
                                }
                                else
                                {
                                    temp = huffman20_len[symbol];
                                }

                                control >>= (int)temp;
                                if ((shift -= (int)temp) < 0)
                                {
                                    shift += 16;
                                    control += (uint)(source[sourcePos++] << (8 + shift));
                                    control += (uint)(source[sourcePos++] << shift);
                                }

                                symbol = table_four[literal_len[pos] + 17 - symbol];
                                while (pos < max_symbol && count-- > 0)
                                    literal_len[pos++] = (byte)symbol;
                                break;
                            }
                            default:
                            {
                                symbol = table_four[literal_len[pos] + 17 - symbol];
                                literal_len[pos++] = (byte)symbol;
                                break;
                            }
                        }
                    } while (pos < max_symbol);

                    fix--;
                    max_symbol += 512;
                } while (max_symbol == 768);

                make_decode_table(768, 12, literal_len, literal_table);
            }

            global_control = control;
            global_shift = shift;
        }
        /* ---------------------------------------------------------------------- */

/* Build a fast huffman decode table from the symbol bit lengths.         */
/* There is an alternate algorithm which is faster but also more complex. */

        public void make_decode_table(int number_symbols, int table_size, byte[] length, ushort[] table)
        {
            byte bit_num = 0;
            int symbol;
            uint leaf; /* could be a register */
            uint table_mask, bit_mask, pos, fill, next_symbol, reverse;
            //int abort = 0;

            pos = 0; /* consistantly used as the current position in the decode table */

            bit_mask = table_mask = (uint)(1 << table_size);

            bit_mask >>= 1; /* don't do the first number */
            bit_num++;

            while (bit_num <= table_size)
            {
                for (symbol = 0; symbol < number_symbols; symbol++)
                {
                    if (length[symbol] == bit_num)
                    {
                        reverse = pos; /* reverse the order of the position's bits */
                        leaf = 0;
                        fill = (uint)table_size;
                        do /* reverse the position */
                        {
                            leaf = (leaf << 1) + (reverse & 1);
                            reverse >>= 1;
                        } while (--fill > 0);

                        if ((pos += bit_mask) > table_mask)
                        {
                            throw new IOException("will overrun the table! abort!");
                        }

                        fill = bit_mask;
                        next_symbol = (uint)(1 << bit_num);
                        do
                        {
                            table[leaf] = (ushort)symbol;
                            leaf += next_symbol;
                        } while (--fill > 0);
                    }
                }

                bit_mask >>= 1;
                bit_num++;
            }

            if (pos != table_mask)
            {
                for (symbol = (int)pos; symbol < table_mask; symbol++) /* clear the rest of the table */
                {
                    reverse = (uint)symbol; /* reverse the order of the position's bits */
                    leaf = 0;
                    fill = (uint)table_size;
                    do /* reverse the position */
                    {
                        leaf = (leaf << 1) + (reverse & 1);
                        reverse >>= 1;
                    } while (--fill > 0);

                    table[leaf] = 0;
                }

                next_symbol = table_mask >> 1;
                pos <<= 16;
                table_mask <<= 16;
                bit_mask = 32768;

                while (bit_num <= 16)
                {
                    for (symbol = 0; symbol < number_symbols; symbol++)
                    {
                        if (length[symbol] == bit_num)
                        {
                            reverse = pos >> 16; /* reverse the order of the position's bits */
                            leaf = 0;
                            fill = (uint)table_size;
                            do /* reverse the position */
                            {
                                leaf = (leaf << 1) + (reverse & 1);
                                reverse >>= 1;
                            } while (--fill > 0);

                            for (fill = 0; fill < bit_num - table_size; fill++)
                            {
                                if (table[leaf] == 0)
                                {
                                    table[(next_symbol << 1)] = 0;
                                    table[(next_symbol << 1) + 1] = 0;
                                    table[leaf] = (ushort)(next_symbol++);
                                }

                                leaf = (uint)(table[leaf] << 1);
                                leaf += (pos >> (15 - (int)fill)) & 1;
                            }

                            table[leaf] = (ushort)symbol;
                            if ((pos += bit_mask) > table_mask)
                            {
                                throw new IOException("will overrun the table! abort!");
                            }
                        }
                    }

                    bit_mask >>= 1;
                    bit_num++;
                }
            }

            if (pos != table_mask)
            {
                throw new IOException("the table is incomplete!");
            }
        }

        /* Fill up the decrunch buffer. Needs lots of overrun for both destination */
/* and source buffers. Most of the time is spent in this routine so it's  */
/* pretty damn optimized. */

        public void decrunch(byte[] source, ref int sourcePos, int sourceEnd, byte[] destination, ref int destinationPos,
            int destinationEnd)
        {
            // register unsigned int control;
            // register int shift;
            uint temp; /* could be a register */
            uint symbol, count;
            //unsigned char * string;

            var control = global_control;
            var shift = global_shift;

            do
            {
                if ((symbol = literal_table[control & 4095]) >= 768)
                {
                    control >>= 12;
                    if ((shift -= 12) < 0)
                    {
                        shift += 16;
                        control += (uint)(source[sourcePos++] << (8 + shift));
                        control += (uint)(source[sourcePos++] << shift);
                    }

                    do /* literal is longer than 12 bits */
                    {
                        symbol = literal_table[(control & 1) + (symbol << 1)];
                        // if (!shift--)
                        if (shift-- == 0)
                        {
                            shift += 16;
                            control += (uint)(source[sourcePos++] << 24);
                            control += (uint)(source[sourcePos++] << 16);
                        }

                        control >>= 1;
                    } while (symbol >= 768);
                }
                else
                {
                    temp = literal_len[symbol];
                    control >>= (int)temp;
                    if ((shift -= (int)temp) < 0)
                    {
                        shift += 16;
                        control += (uint)(source[sourcePos++] << (8 + shift));
                        control += (uint)(source[sourcePos++] << shift);
                    }
                }

                if (symbol < 256)
                {
                    destination[destinationPos++] = (byte)symbol;
                }
                else
                {
                    symbol -= 256;
                    count = table_two[temp = symbol & 31];
                    temp = table_one[temp];
                    if ((temp >= 3) && (decrunch_method == 3))
                    {
                        temp -= 3;
                        count += ((control & table_three[temp]) << 3);
                        control >>= (int)temp;
                        if ((shift -= (int)temp) < 0)
                        {
                            shift += 16;
                            control += (uint)(source[sourcePos++] << (8 + shift));
                            control += (uint)(source[sourcePos++] << shift);
                        }

                        count += (temp = offset_table[control & 127]);
                        temp = offset_len[temp];
                    }
                    else
                    {
                        count += control & table_three[temp];
                        if (count == 0) count = last_offset;
                    }

                    control >>= (int)temp;
                    if ((shift -= (int)temp) < 0)
                    {
                        shift += 16;
                        control += (uint)(source[sourcePos++] << (8 + shift));
                        control += (uint)(source[sourcePos++] << shift);
                    }

                    last_offset = count;

                    count = table_two[temp = (symbol >> 5) & 15] + 3;
                    temp = table_one[temp];
                    count += (control & table_three[temp]);
                    control >>= (int)temp;
                    if ((shift -= (int)temp) < 0)
                    {
                        shift += 16;
                        control += (uint)(source[sourcePos++] << (8 + shift));
                        control += (uint)(source[sourcePos++] << shift);
                    }

                    var decrunchBufferPos = last_offset < destinationPos
                        ? destinationPos - last_offset
                        : destinationPos + 65536 - last_offset;
                    do
                    {
                        destination[destinationPos++] = destination[decrunchBufferPos++];
                    } while (--count > 0);
                }
            } while ((destinationPos < destinationEnd) && (sourcePos < sourceEnd));

            global_control = control;
            global_shift = shift;
        }
    }
}