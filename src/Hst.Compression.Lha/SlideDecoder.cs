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

    /// <summary>
    /// Sliding dictionary decoder for -lh2- and -lh3- methods, equivalent of decode in slide.c, decoding data
    /// on demand as it's read.
    /// </summary>
    internal class SlideDecoder : ILhaDecoder
    {
        private readonly IDecoder decoder;
        private readonly Lha lha;
        private readonly byte[] dtext;
        private readonly int dicsiz1;
        private readonly int adjust;
        private int loc;
        private int matchRemaining;
        private int matchPos;
        private long decodeCount;

        /// <summary>
        /// Create decoder for -lh2- method.
        /// </summary>
        public static SlideDecoder CreateLh2(Stream packedData, LzHeader header) =>
            new SlideDecoder(packedData, header, new Lh2Decoder(), LhConstants.LZHUFF2_DICBIT);

        /// <summary>
        /// Create decoder for -lh3- method.
        /// </summary>
        public static SlideDecoder CreateLh3(Stream packedData, LzHeader header) =>
            new SlideDecoder(packedData, header, new Lh3Decoder(), LhConstants.LZHUFF3_DICBIT);

        private SlideDecoder(Stream packedData, LzHeader header, IDecoder decoder, int dicbit)
        {
            if (header.OriginalSize > int.MaxValue || header.PackedSize > int.MaxValue)
            {
                throw new NotSupportedException("Entries larger than 2GB are not supported");
            }

            this.decoder = decoder;

            var dicsiz = 1 << dicbit;
            dtext = new byte[dicsiz];
            for (var i = 0; i < dicsiz; i++)
            {
                dtext[i] = (byte)' ';
            }

            var crcIo = new CrcIo();
            var bitIo = new BitIo(packedData, (int)header.OriginalSize, (int)header.PackedSize);
            lha = new Lha(dicbit, bitIo, crcIo)
            {
                dtext = dtext,
                loc = 0,
                decode_count = 0
            };

            decoder.DecodeStart(lha);

            dicsiz1 = dicsiz - 1;
            adjust = 256 - LhConstants.THRESHOLD;
            loc = 0;
            matchRemaining = 0;
            matchPos = 0;
            decodeCount = 0;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;
            while (bytesRead < count)
            {
                byte c;
                if (matchRemaining > 0)
                {
                    c = dtext[matchPos++ & dicsiz1];
                    matchRemaining--;
                }
                else
                {
                    // lh2 decoder uses decoded bytes count
                    lha.loc = loc;
                    lha.decode_count = decodeCount;

                    var code = decoder.DecodeC(lha);
                    if (code < 256)
                    {
                        c = (byte)code;
                    }
                    else
                    {
                        var matchLength = code - adjust;
                        var matchOffset = decoder.DecodeP(lha) + 1;
                        matchPos = (loc - matchOffset) & dicsiz1;
                        c = dtext[matchPos++ & dicsiz1];
                        matchRemaining = matchLength - 1;
                    }
                }

                dtext[loc++] = c;
                if (loc > dicsiz1)
                {
                    loc = 0;
                }

                buffer[offset + bytesRead++] = c;
                decodeCount++;
            }

            return bytesRead;
        }
    }
}
