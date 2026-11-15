/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              extract.c -- extrcat from archive                           */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    internal static class Extract
    {
        /// <summary>
        /// Decode (extract) entry data from input stream to output stream.
        /// </summary>
        /// <param name="input">Input stream positioned at start of entry data.</param>
        /// <param name="output">Output stream to write extracted data to. Can be null to only calculate crc.</param>
        /// <param name="hdr">Header of entry.</param>
        /// <param name="readSize">Number of bytes read from input stream.</param>
        /// <param name="verifyMode">Verify mode only calculates crc and doesn't write to output stream.</param>
        /// <param name="textMode">Text mode is not supported.</param>
        /// <param name="extractBrokenArchive">Extract archives created by old LHa for UNIX with a bug.</param>
        /// <returns>CRC-16 of extracted data.</returns>
        public static uint DecodeLzHuf(Stream input, Stream output, LzHeader hdr, out int readSize,
            bool verifyMode = false, bool textMode = false, bool extractBrokenArchive = false)
        {
            if (textMode)
            {
                throw new NotSupportedException("Text mode is not supported");
            }

            var dataOffset = input.CanSeek ? input.Position : 0;
            using (var entryStream = new LhaEntryStream(input, hdr, dataOffset, extractBrokenArchive, false))
            {
                var buffer = new byte[Constants.BUFFERSIZE * 16];
                int bytesRead;
                while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (!verifyMode)
                    {
                        output?.Write(buffer, 0, bytesRead);
                    }
                }

                readSize = (int)entryStream.ReadSize;
                return entryStream.Crc;
            }
        }

        internal static int GetMethodNum(string method)
        {
            switch (method)
            {
                case Constants.LZHUFF0_METHOD:
                    return Constants.LZHUFF0_METHOD_NUM;
                case Constants.LZHUFF1_METHOD:
                    return Constants.LZHUFF1_METHOD_NUM;
                case Constants.LZHUFF2_METHOD:
                    return Constants.LZHUFF2_METHOD_NUM;
                case Constants.LZHUFF3_METHOD:
                    return Constants.LZHUFF3_METHOD_NUM;
                case Constants.LZHUFF4_METHOD:
                    return Constants.LZHUFF4_METHOD_NUM;
                case Constants.LZHUFF5_METHOD:
                    return Constants.LZHUFF5_METHOD_NUM;
                case Constants.LZHUFF6_METHOD:
                    return Constants.LZHUFF6_METHOD_NUM;
                case Constants.LZHUFF7_METHOD:
                    return Constants.LZHUFF7_METHOD_NUM;
                case Constants.LARC_METHOD:
                    return Constants.LARC_METHOD_NUM;
                case Constants.LARC5_METHOD:
                    return Constants.LARC5_METHOD_NUM;
                case Constants.LARC4_METHOD:
                    return Constants.LARC4_METHOD_NUM;
                case Constants.LZHDIRS_METHOD:
                    return Constants.LZHDIRS_METHOD_NUM;
                case Constants.PMARC0_METHOD:
                    return Constants.PMARC0_METHOD_NUM;
                case Constants.PMARC2_METHOD:
                    return Constants.PMARC2_METHOD_NUM;
                default:
                    throw new NotSupportedException($"Unknown method \"{method}\"");
            }
        }

        internal static int GetDicBit(string method)
        {
            switch (method)
            {
                case Constants.LZHUFF0_METHOD:
                    return Constants.LZHUFF0_DICBIT;
                case Constants.LZHUFF1_METHOD:
                    return Constants.LZHUFF1_DICBIT;
                case Constants.LZHUFF2_METHOD:
                    return Constants.LZHUFF2_DICBIT;
                case Constants.LZHUFF3_METHOD:
                    return Constants.LZHUFF3_DICBIT;
                case Constants.LZHUFF4_METHOD:
                    return Constants.LZHUFF4_DICBIT;
                case Constants.LZHUFF5_METHOD:
                    return Constants.LZHUFF5_DICBIT;
                case Constants.LZHUFF6_METHOD:
                    return Constants.LZHUFF6_DICBIT;
                case Constants.LZHUFF7_METHOD:
                    return Constants.LZHUFF7_DICBIT;
                case Constants.LARC_METHOD:
                    return Constants.LARC_DICBIT;
                case Constants.LARC5_METHOD:
                    return Constants.LARC5_DICBIT;
                case Constants.LARC4_METHOD:
                    return Constants.LARC4_DICBIT;
                case Constants.LZHDIRS_METHOD:
                    return 0; /* directory or symbolic link has no data */
                case Constants.PMARC0_METHOD:
                    return Constants.PMARC0_DICBIT;
                case Constants.PMARC2_METHOD:
                    return Constants.PMARC2_DICBIT;
                default:
                    throw new NotSupportedException($"Unknown method \"{method}\"");
            }
        }
    }
}
