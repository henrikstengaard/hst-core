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
    using System.IO;

    public static class Slide
    {
        private static readonly IDecoder[] Decoders =
        {
            new Lh1Decoder(),
            new Lh2Decoder(),
            new Lh3Decoder(),
            new Lh4Decoder(),
            new Lh5Decoder(),
            new Lh6Decoder(),
            new Lh7Decoder(),
            new LzsDecoder(),
            new Lz5Decoder(),
            new Lz4Decoder(),
            new LhdDecoder(),
            new Pm0Decoder(),
            new Pm2Decoder()
        };
        
        public static int Decode(CrcIo crcIo, Stream input, Stream output, int method, int dicbit, int origsize,
            int packed, bool extractBrokenArchive)
        {
            // infile = interface->infile;
            // outfile = interface->outfile;
            // dicbit = interface->dicbit;
            // origsize = interface->original;
            // compsize = interface->packed;
            // decode_set = decode_define[interface->method - 1];
            var decoder = Decoders[method - 1];
            
            var bitIo = new BitIo(input, origsize, packed);
            var lha = new Lha(dicbit, bitIo, crcIo);

            //uint i, c;
            //uint dicsiz1, adjust;
            
            // https://github.com/jca02266/lha/blob/master/src/slide.c
            //crcIo.InitializeCrc();
            var dicsiz = 1L << dicbit;
            // dtext = (unsigned char *)xmalloc(dicsiz);
            var dtext = new byte[dicsiz];

            for (var i = 0; i < dicsiz; i++)
            {
                dtext[i] = (byte)(extractBrokenArchive ? 0 : ' ');
            }

            decoder.DecodeStart(lha);
            var dicsiz1 = dicsiz - 1;
            var adjust = 256 - Constants.THRESHOLD;
            if (method == Constants.LARC_METHOD_NUM || method == Constants.PMARC2_METHOD_NUM)
            {
                adjust = 256 - 2;
            }
            
            var decode_count = 0;
            var loc = 0;
            ushort c;
            while (decode_count < origsize) {
                c = decoder.DecodeC(lha);
                if (c < 256) {
                    dtext[loc++] = (byte)c;
                    if (loc == dicsiz) {
                        crcIo.fwrite_crc(dtext, (int)dicsiz, output);
                        loc = 0;
                    }
                    decode_count++;
                }
                else {
                    // struct matchdata match;
                    // unsigned int matchpos;
                    int matchdata_len;
                    uint matchdata_off;
                    uint matchpos;

                    matchdata_len = c - adjust;
                    matchdata_off = (uint)decoder.DecodeP(lha) + 1;
                    matchpos = (uint)((loc - matchdata_off) & dicsiz1);

                    decode_count += matchdata_len;
                    for (var i = 0; i < matchdata_len; i++) {
                        c = dtext[(matchpos + i) & dicsiz1];
                        dtext[loc++] = (byte)c;
                        if (loc == dicsiz) {
                            crcIo.fwrite_crc(dtext, (int)dicsiz, output);
                            loc = 0;
                        }
                    }
                }
            }
            if (loc != 0) {
                crcIo.fwrite_crc(dtext, loc, output);
            }

            /* usually read size is interface->packed */
            //interface->read_size = interface->packed - compsize;
            //read_size = interface->packed - compsize;

            return packed - bitIo.compsize;            
        }
    }
}