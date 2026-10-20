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
    using System.IO;

    public static class Extract
    {
        public static uint DecodeLzHuf(Stream input, Stream output, LzHeader hdr, out int readSize,
            bool verifyMode = false, bool textMode = false, bool extractBrokenArchive = false)
        {
            var crcIo = new CrcIo(verifyMode);
            readSize = 0;
            
            var method = GetMethodNum(hdr.Method);
            var dicBit = GetDicBit(hdr.Method);

            // reset crc
            crcIo.InitializeCrc();

            if (dicBit == 0)
            {
                /* LZHUFF0_DICBIT or LARC4_DICBIT or PMARC0_DICBIT*/
                // *read_sizep = copyfile(infp, (verify_mode ? NULL : outfp), original_size, 2, &crc);
                readSize = Util.CopyFile(input, verifyMode ? null : output, (int)hdr.OriginalSize, textMode, crcIo);
            }
            else
            {
                readSize = Slide.Decode(crcIo, input, output, method, dicBit, (int)hdr.OriginalSize, (int)hdr.PackedSize, extractBrokenArchive);
                //read_size =  interface.read_size;
            }

            return crcIo.crc;
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
                case Constants.PMARC0_METHOD:
                    return Constants.PMARC0_METHOD_NUM;
                case Constants.PMARC2_METHOD:
                    return Constants.PMARC2_METHOD_NUM;
                default:
                    return Constants.LZHUFF5_METHOD_NUM;
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
                case Constants.PMARC0_METHOD:
                    return Constants.PMARC0_DICBIT;
                case Constants.PMARC2_METHOD:
                    return Constants.PMARC2_DICBIT;
                default:
                    return Constants.LZHUFF5_DICBIT;
            }
        }
    }
}