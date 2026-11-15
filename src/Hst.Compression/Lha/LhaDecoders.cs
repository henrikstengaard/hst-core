namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Built-in decoders by method.
    /// </summary>
    internal static class LhaDecoders
    {
        private static readonly IDictionary<string, LhaDecoderFactory> BuiltInDecoders =
            new Dictionary<string, LhaDecoderFactory>
            {
                { Constants.LZHUFF0_METHOD, (input, _) => new NullDecoder(input) },
                { Constants.LARC4_METHOD, (input, _) => new NullDecoder(input) },
                { Constants.PMARC0_METHOD, (input, _) => new NullDecoder(input) },
                { Constants.LZHDIRS_METHOD, (input, _) => new NullDecoder(input) },
                { Constants.LZHUFF1_METHOD, (input, _) => new Lh1Decoder(input) },
                { Constants.LZHUFF4_METHOD, (input, _) => LhNewDecoder.CreateLh5(input) },
                { Constants.LZHUFF5_METHOD, (input, _) => LhNewDecoder.CreateLh5(input) },
                { Constants.LZHUFF6_METHOD, (input, _) => LhNewDecoder.CreateLh6(input) },
                { Constants.LZHUFF7_METHOD, (input, _) => LhNewDecoder.CreateLh7(input) },
                { Constants.LZHUFFX_METHOD, (input, _) => LhNewDecoder.CreateLhx(input) },
                { Constants.LHARK7_METHOD, (input, _) => LhNewDecoder.CreateLk7(input) },
                { Constants.LARC_METHOD, (input, _) => new LzsDecoder(input) },
                { Constants.LARC5_METHOD, (input, _) => new Lz5Decoder(input) },
                { Constants.PMARC1_METHOD, (input, _) => new Pm1Decoder(input) },
                { Constants.PMARC2_METHOD, (input, _) => new Pm2Decoder(input) },
            };

        /// <summary>
        /// Create decoder for header method using decoders from options or built-in decoders.
        /// </summary>
        public static ILhaDecoder Create(Stream packedData, LzHeader header, LhaOptions options)
        {
            if (options != null && options.Decoders.TryGetValue(header.Method, out var factory))
            {
                return factory(packedData, header);
            }

            if (BuiltInDecoders.TryGetValue(header.Method, out factory))
            {
                return factory(packedData, header);
            }

            throw new NotSupportedException($"Method \"{header.Method}\" of \"{header.Name}\" is not supported");
        }
    }
}
