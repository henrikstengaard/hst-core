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
            
            int method;
            switch (hdr.Method)
            {
                case Constants.LZHUFF0_METHOD:
                    method = Constants.LZHUFF0_METHOD_NUM;
                    break;
                case Constants.LZHUFF1_METHOD:
                    method = Constants.LZHUFF1_METHOD_NUM;
                    break;
                case Constants.LZHUFF2_METHOD:
                    method = Constants.LZHUFF2_METHOD_NUM;
                    break;
                case Constants.LZHUFF3_METHOD:
                    method = Constants.LZHUFF3_METHOD_NUM;
                    break;
                case Constants.LZHUFF4_METHOD:
                    method = Constants.LZHUFF4_METHOD_NUM;
                    break;
                case Constants.LZHUFF5_METHOD:
                    method = Constants.LZHUFF5_METHOD_NUM;
                    break;
                case Constants.LZHUFF6_METHOD:
                    method = Constants.LZHUFF6_METHOD_NUM;
                    break;
                case Constants.LZHUFF7_METHOD:
                    method = Constants.LZHUFF7_METHOD_NUM;
                    break;
                case Constants.LARC_METHOD:
                    method = Constants.LARC_METHOD_NUM;
                    break;
                case Constants.LARC5_METHOD:
                    method = Constants.LARC5_METHOD_NUM;
                    break;
                case Constants.LARC4_METHOD:
                    method = Constants.LARC4_METHOD_NUM;
                    break;
                case Constants.PMARC0_METHOD:
                    method = Constants.PMARC0_METHOD_NUM;
                    break;
                case Constants.PMARC2_METHOD:
                    method = Constants.PMARC2_METHOD_NUM;
                    break;
                default:
                    method = Constants.LZHUFF5_METHOD_NUM;
                    break;
            }

            int dicBit;
            switch (hdr.Method)
            {
                case Constants.LZHUFF0_METHOD:
                    dicBit = Constants.LZHUFF0_DICBIT;
                    break;
                case Constants.LZHUFF1_METHOD:
                    dicBit = Constants.LZHUFF1_DICBIT;
                    break;
                case Constants.LZHUFF2_METHOD:
                    dicBit = Constants.LZHUFF2_DICBIT;
                    break;
                case Constants.LZHUFF3_METHOD:
                    dicBit = Constants.LZHUFF3_DICBIT;
                    break;
                case Constants.LZHUFF4_METHOD:
                    dicBit = Constants.LZHUFF4_DICBIT;
                    break;
                case Constants.LZHUFF5_METHOD:
                    dicBit = Constants.LZHUFF5_DICBIT;
                    break;
                case Constants.LZHUFF6_METHOD:
                    dicBit = Constants.LZHUFF6_DICBIT;
                    break;
                case Constants.LZHUFF7_METHOD:
                    dicBit = Constants.LZHUFF7_DICBIT;
                    break;
                case Constants.LARC_METHOD:
                    dicBit = Constants.LARC_DICBIT;
                    break;
                case Constants.LARC5_METHOD:
                    dicBit = Constants.LARC5_DICBIT;
                    break;
                case Constants.LARC4_METHOD:
                    dicBit = Constants.LARC4_DICBIT;
                    break;
                case Constants.PMARC0_METHOD:
                    dicBit = Constants.PMARC0_DICBIT;
                    break;
                case Constants.PMARC2_METHOD:
                    dicBit = Constants.PMARC2_DICBIT;
                    break;
                default:
                    dicBit = Constants.LZHUFF5_DICBIT;
                    break;
            }

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
    }
}