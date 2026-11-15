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
                { LhaMethod.Lh0, (input, _) => new StoredDecoder(input) },
                { LhaMethod.Lz4, (input, _) => new StoredDecoder(input) },
                { LhaMethod.Pm0, (input, _) => new StoredDecoder(input) },
                { LhaMethod.Lhd, (input, _) => new StoredDecoder(input) },
                { LhaMethod.Lh1, (input, _) => new Lh1Decoder(input) },
                { LhaMethod.Lh4, (input, _) => LhDecoder.Create(input, LhaMethod.Lh4) },
                { LhaMethod.Lh5, (input, _) => LhDecoder.Create(input, LhaMethod.Lh5) },
                { LhaMethod.Lh6, (input, _) => LhDecoder.Create(input, LhaMethod.Lh6) },
                { LhaMethod.Lh7, (input, _) => LhDecoder.Create(input, LhaMethod.Lh7) },
                { LhaMethod.Lhx, (input, _) => LhDecoder.Create(input, LhaMethod.Lhx) },
                { LhaMethod.Lk7, (input, _) => LhDecoder.Create(input, LhaMethod.Lk7) },
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
