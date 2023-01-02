/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              lhext.c -- LHarc extract                                    */
/*                                                                          */
/*      Copyright (C) MCMLXXXIX Yooichi.Tagawa                              */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 0.00  Original                             1988.05.23  Y.Tagawa    */
/*  Ver. 1.00  Fixed                                1989.09.22  Y.Tagawa    */
/*  Ver. 0.03  LHa for UNIX                         1991.12.17  M.Oki       */
/*  Ver. 1.12  LHa for UNIX                         1993.10.01  N.Watazaki  */
/*  Ver. 1.13b Symbolic Link Update Bug Fix         1994.06.21  N.Watazaki  */
/*  Ver. 1.14  Source All chagned                   1995.01.14  N.Watazaki  */
/*  Ver. 1.14e bugfix                               1999.04.30  T.Okamoto   */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System.IO;
    using System.Linq;

    public static class LhExt
    {
        private static readonly string[] Methods =
        {
            Constants.LZHUFF0_METHOD, Constants.LZHUFF1_METHOD, Constants.LZHUFF2_METHOD, Constants.LZHUFF3_METHOD,
            Constants.LZHUFF4_METHOD, Constants.LZHUFF5_METHOD, Constants.LZHUFF6_METHOD, Constants.LZHUFF7_METHOD,
            Constants.LARC_METHOD, Constants.LARC5_METHOD, Constants.LARC4_METHOD, Constants.LZHDIRS_METHOD,
            Constants.PMARC0_METHOD, Constants.PMARC2_METHOD
        };

        public static void ExtractOne(Stream input, Stream output, LzHeader hdr, bool verifyMode = false)
        {
            var method = Methods.FirstOrDefault(x => x == hdr.Method);

            if (string.IsNullOrWhiteSpace(method))
            {
                throw new IOException($"Unknown method \"{method}\"; \"{hdr.Name}\" will be skipped ...");
            }

            var crc = (short)Extract.DecodeLzHuf(input, output, hdr, out var readSize);

            if (hdr.PackedSize != readSize)
            {
                throw new IOException($"Read size {readSize} doesnt match packed size {hdr.PackedSize}");
            }
            
            if (hdr.HasCrc && crc != hdr.Crc)
            {
                throw new IOException($"CRC error: \"{hdr.Name}\"");
            }
        }
    }
}