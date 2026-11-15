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
                { LhaMethod.Lh0, (input, _) => new NullDecoder(input) },
                { LhaMethod.Lz4, (input, _) => new NullDecoder(input) },
                { LhaMethod.Pm0, (input, _) => new NullDecoder(input) },
                { LhaMethod.Lhd, (input, _) => new NullDecoder(input) },
                { LhaMethod.Lh1, (input, _) => new Lh1Decoder(input) },
                { LhaMethod.Lh4, (input, _) => LhNewDecoder.CreateLh5(input) },
                { LhaMethod.Lh5, (input, _) => LhNewDecoder.CreateLh5(input) },
                { LhaMethod.Lh6, (input, _) => LhNewDecoder.CreateLh6(input) },
                { LhaMethod.Lh7, (input, _) => LhNewDecoder.CreateLh7(input) },
                { LhaMethod.Lhx, (input, _) => LhNewDecoder.CreateLhx(input) },
                { LhaMethod.Lk7, (input, _) => LhNewDecoder.CreateLk7(input) },
                { LhaMethod.Lzs, (input, _) => new LzsDecoder(input) },
                { LhaMethod.Lz5, (input, _) => new Lz5Decoder(input) },
                { LhaMethod.Pm1, (input, _) => new Pm1Decoder(input) },
                { LhaMethod.Pm2, (input, _) => new Pm2Decoder(input) },
            };

        /// <summary>
        /// Create decoder for header method using decoders from options or built-in decoders.
        /// </summary>
        public static ILhaDecoder Create(Stream packedData, LhaEntry header, LhaOptions options)
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
