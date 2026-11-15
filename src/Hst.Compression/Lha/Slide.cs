/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              slide.c -- sliding dictionary with percolating update       */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14d  Exchanging a search algorithm  1997.01.11    T.Okamoto      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    public static class Slide
    {
        private const int TXTSIZ = Constants.MAX_DICSIZ * 2 + Constants.MAXMATCH;
        private const int HSHSIZ = 1 << 15;
        private const uint NIL = 0;
        private const int LIMIT = 0x100; /* limit of hash chain */

        /// <summary>
        /// Create decoder for method. Decoders are stateful, so a new decoder is created for each entry decoded.
        /// </summary>
        /// <param name="method">Method number</param>
        /// <returns>Decoder for method</returns>
        public static IDecoder GetDecoder(int method)
        {
            switch (method)
            {
                case Constants.LZHUFF1_METHOD_NUM:
                    return new Lh1Decoder();
                case Constants.LZHUFF2_METHOD_NUM:
                    return new Lh2Decoder();
                case Constants.LZHUFF3_METHOD_NUM:
                    return new Lh3Decoder();
                case Constants.LZHUFF4_METHOD_NUM:
                    return new Lh4Decoder();
                case Constants.LZHUFF5_METHOD_NUM:
                    return new Lh5Decoder();
                case Constants.LZHUFF6_METHOD_NUM:
                    return new Lh6Decoder();
                case Constants.LZHUFF7_METHOD_NUM:
                    return new Lh7Decoder();
                case Constants.LARC_METHOD_NUM:
                    return new LzsDecoder();
                case Constants.LARC5_METHOD_NUM:
                    return new Lz5Decoder();
                case Constants.PMARC2_METHOD_NUM:
                    return new Pm2Decoder();
                default:
                    /* lh0, lz4, lhd and pm0 are stored without compression and doesn't use a decoder */
                    throw new NotSupportedException($"Method number {method} doesn't use a decoder");
            }
        }

        internal static IEncoder GetEncoder(int method, out int dicbit, out ushort maxmatch)
        {
            switch (method)
            {
                case Constants.LZHUFF1_METHOD_NUM:
                    maxmatch = 60;
                    dicbit = Constants.LZHUFF1_DICBIT; /* 12 bits  Changed N.Watazaki */
                    return new Lh1Encoder();
                case Constants.LZHUFF4_METHOD_NUM:
                    maxmatch = (ushort)Constants.MAXMATCH;
                    dicbit = Constants.LZHUFF4_DICBIT; /* 12 bits */
                    return new Lh5Encoder();
                case Constants.LZHUFF5_METHOD_NUM:
                    maxmatch = (ushort)Constants.MAXMATCH;
                    dicbit = Constants.LZHUFF5_DICBIT; /* 13 bits */
                    return new Lh5Encoder();
                case Constants.LZHUFF6_METHOD_NUM:
                    maxmatch = (ushort)Constants.MAXMATCH;
                    dicbit = Constants.LZHUFF6_DICBIT; /* 15 bits */
                    return new Lh5Encoder();
                case Constants.LZHUFF7_METHOD_NUM:
                    maxmatch = (ushort)Constants.MAXMATCH;
                    dicbit = Constants.LZHUFF7_DICBIT; /* 16 bits */
                    return new Lh5Encoder();
                default:
                    throw new NotSupportedException($"Compression with method number {method} is not supported");
            }
        }

        /// <summary>
        /// Encode (compress) input stream to output stream using sliding dictionary.
        /// </summary>
        /// <param name="input">Input stream to read data from.</param>
        /// <param name="output">Output stream to write compressed data to.</param>
        /// <param name="method">Method number, supports lh1, lh4, lh5, lh6 and lh7.</param>
        /// <param name="origsize">Size of data to read from input stream and compress.</param>
        /// <returns>Result of encoding.</returns>
        public static EncodeResult Encode(Stream input, Stream output, int method, long origsize)
        {
            return new Encoder(input, output, method, origsize).Encode();
        }

        /// <summary>
        /// Encoder holding the state of slide.c encode.
        /// </summary>
        private class Encoder
        {
            private struct MatchData
            {
                public int len;
                public uint off;
            }

            private readonly Stream infile;
            private readonly IEncoder encodeSet;
            private readonly Lha lha;
            private readonly CrcIo crcIo;
            private readonly int maxmatch;
            private readonly uint dicsiz;
            private readonly uint txtsiz;
            private readonly byte[] text;
            private readonly uint[] hashPos;
            private readonly bool[] hashTooFlag;
            private readonly uint[] prev;
            private long inputRemaining;
            private uint remainder;

            public Encoder(Stream input, Stream output, int method, long origsize)
            {
                if (origsize > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(origsize),
                        "Compression of data larger than 2GB is not supported");
                }

                infile = input;
                inputRemaining = origsize;
                encodeSet = GetEncoder(method, out var dicbit, out var maxmatchValue);
                maxmatch = maxmatchValue;
                dicsiz = 1U << dicbit;
                txtsiz = dicsiz * 2 + (uint)maxmatch;

                crcIo = new CrcIo();
                crcIo.InitializeCrc();
                var bitIo = new BitIo(output, (int)origsize, 0);
                lha = new Lha(dicbit, bitIo, crcIo)
                {
                    maxmatch = maxmatchValue
                };

                text = new byte[TXTSIZ];
                hashPos = new uint[HSHSIZ];
                hashTooFlag = new bool[HSHSIZ];
                prev = new uint[Constants.MAX_DICSIZ];
            }

            /// <summary>
            /// read from input stream and update crc, equivalent of fread_crc
            /// </summary>
            private uint ReadCrc(int offset, uint n)
            {
                var size = (int)Math.Min(n, inputRemaining);
                var total = 0;
                while (total < size)
                {
                    var read = infile.Read(text, offset + total, size - total);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                }

                inputRemaining -= total;
                crcIo.calccrc(text, offset, total);
                return (uint)total;
            }

            /* hash function: it represents 3 letters from `pos' on `text' */
            private uint INIT_HASH(uint pos) =>
                (uint)((((text[pos] << 5) ^ text[pos + 1]) << 5) ^ text[pos + 2]) & (HSHSIZ - 1);

            private uint NEXT_HASH(uint hash, uint pos) =>
                ((hash << 5) ^ text[pos + 2]) & (HSHSIZ - 1);

            private void InitSlide()
            {
                for (var i = 0; i < HSHSIZ; i++)
                {
                    hashPos[i] = NIL;
                    hashTooFlag[i] = false;
                }
            }

            /* update dictionary */
            private void UpdateDict(ref uint pos)
            {
                Array.Copy(text, dicsiz, text, 0, txtsiz - dicsiz);

                var n = ReadCrc((int)(txtsiz - dicsiz), dicsiz);

                remainder += n;

                pos -= dicsiz;
                for (var i = 0; i < HSHSIZ; i++)
                {
                    var j = hashPos[i];
                    hashPos[i] = j > dicsiz ? j - dicsiz : NIL;
                    hashTooFlag[i] = false;
                }

                for (var i = 0; i < dicsiz; i++)
                {
                    var j = prev[i];
                    prev[i] = j > dicsiz ? j - dicsiz : NIL;
                }
            }

            /* associate position with token */
            private void InsertHash(uint token, uint pos)
            {
                prev[pos & (dicsiz - 1)] = hashPos[token]; /* chain the previous pos. */
                hashPos[token] = pos;
            }

            private void SearchDict1(uint token, uint pos, uint off,
                uint max, /* max. length of matching string */
                ref MatchData m)
            {
                uint chain = 0;
                var scanPos = hashPos[token];
                var scanBeg = (int)scanPos - (int)off;
                var scanEnd = (int)pos - (int)dicsiz;

                while (scanBeg > scanEnd)
                {
                    chain++;

                    if (text[scanBeg + m.len] == text[pos + m.len])
                    {
                        /* collate token */
                        var a = scanBeg;
                        var b = (int)pos;
                        uint len;
                        for (len = 0; len < max && text[a++] == text[b++]; len++)
                        {
                        }

                        if (len > m.len)
                        {
                            m.off = (uint)(pos - scanBeg);
                            m.len = (int)len;
                            if (m.len == max)
                            {
                                break;
                            }
                        }
                    }

                    scanPos = prev[scanPos & (dicsiz - 1)];
                    scanBeg = (int)scanPos - (int)off;
                }

                if (chain >= LIMIT)
                {
                    hashTooFlag[token] = true;
                }
            }

            /* search the longest token matching to current token */
            private void SearchDict(uint token, /* search token */
                uint pos, /* position of token */
                int min, /* min. length of matching string */
                ref MatchData m)
            {
                uint off, tok;

                if (min < Constants.THRESHOLD - 1)
                {
                    min = Constants.THRESHOLD - 1;
                }

                var max = (uint)maxmatch;
                m.off = 0;
                m.len = min;

                off = 0;
                for (tok = token; hashTooFlag[tok] && off < maxmatch - Constants.THRESHOLD;)
                {
                    /* If matching position is too many, The search key is
                       changed into following token from `off' (for speed). */
                    ++off;
                    tok = NEXT_HASH(tok, pos + off);
                }

                if (off == maxmatch - Constants.THRESHOLD)
                {
                    off = 0;
                    tok = token;
                }

                SearchDict1(tok, pos, off, max, ref m);

                if (off > 0 && m.len < off + 3)
                {
                    /* re-search */
                    SearchDict1(token, pos, 0, off + 2, ref m);
                }

                if (m.len > remainder)
                {
                    m.len = (int)remainder;
                }
            }

            /* slide dictionary */
            private void NextToken(ref uint token, ref uint pos)
            {
                remainder--;
                if (++pos >= txtsiz - maxmatch)
                {
                    UpdateDict(ref pos);
                }

                token = NEXT_HASH(token, pos);
            }

            public EncodeResult Encode()
            {
                uint token, pos;
                long count = 0;
                MatchData match, last;

                lha.bitIo.unpackable = false;

                InitSlide();

                encodeSet.EncodeStart(lha);
                for (var i = 0; i < TXTSIZ; i++)
                {
                    text[i] = (byte)' ';
                }

                remainder = ReadCrc((int)dicsiz, txtsiz - dicsiz);

                match.len = Constants.THRESHOLD - 1;
                match.off = 0;
                if (match.len > remainder)
                {
                    match.len = (int)remainder;
                }

                pos = dicsiz;
                token = INIT_HASH(pos);
                InsertHash(token, pos); /* associate token and pos */

                while (remainder > 0 && !lha.bitIo.unpackable)
                {
                    last = match;

                    NextToken(ref token, ref pos);
                    SearchDict(token, pos, last.len - 1, ref match);
                    InsertHash(token, pos);

                    if (match.len > last.len || last.len < Constants.THRESHOLD)
                    {
                        /* output a letter */
                        encodeSet.Output(lha, text[pos - 1], 0);
                        count++;
                    }
                    else
                    {
                        /* output length and offset */
                        encodeSet.Output(lha, (uint)(last.len + (256 - Constants.THRESHOLD)),
                            (last.off - 1) & (dicsiz - 1));

                        count += last.len;

                        --last.len;
                        while (--last.len > 0)
                        {
                            NextToken(ref token, ref pos);
                            InsertHash(token, pos);
                        }

                        NextToken(ref token, ref pos);
                        SearchDict(token, pos, Constants.THRESHOLD - 1, ref match);
                        InsertHash(token, pos);
                    }
                }

                encodeSet.EncodeEnd(lha);

                return new EncodeResult
                {
                    Crc = (ushort)crcIo.crc,
                    PackedSize = lha.bitIo.compsize,
                    OriginalSize = count,
                    Unpackable = lha.bitIo.unpackable
                };
            }
        }
    }
}
