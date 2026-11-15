namespace Hst.Compression.Tests.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Xunit;

    /// <summary>
    /// Tests encoding data with static huffman methods and decoding it with decoders ported from Lhasa.
    /// </summary>
    public class GivenLhEncoder
    {
        private static readonly string[] Methods =
        {
            LhaMethod.Lh4, LhaMethod.Lh5, LhaMethod.Lh6, LhaMethod.Lh7
        };

        public static IEnumerable<object[]> MethodsAndData =>
            from method in Methods
            from data in new[] { "zeros", "random", "text", "pattern", "mixed", "single", "two" }
            select new object[] { method, data };

        [Theory]
        [MemberData(nameof(MethodsAndData))]
        public void WhenEncodeAndDecodeThenDataIsEqual(string method, string dataName)
        {
            // arrange - data to encode
            var data = CreateData(dataName);

            // act - encode data without limit
            using var packed = new MemoryStream();
            var result = LhEncoder.Create(method).Encode(new MemoryStream(data), packed, data.Length, long.MaxValue);

            // assert - result has crc and sizes
            Assert.False(result.LimitExceeded);
            Assert.Equal(data.Length, result.UnpackedSize);
            Assert.Equal(packed.Length, result.PackedSize);

            // act - decode data
            var decoded = Decode(method, packed.ToArray(), data.Length, result.Crc);

            // assert - decoded data is equal
            Assert.Equal(data, decoded);
        }

        [Theory]
        [InlineData(LhaMethod.Lh5)]
        [InlineData(LhaMethod.Lh7)]
        public void WhenEncodeRandomDataWithLimitThenLimitIsExceededAndBytesWrittenAreLimited(string method)
        {
            // arrange - random data, which can't be compressed
            var data = CreateData("random");

            // act - encode data with limit less than original size
            using var packed = new MemoryStream();
            var result = LhEncoder.Create(method)
                .Encode(new MemoryStream(data), packed, data.Length, data.Length - 1);

            // assert - limit is exceeded and no more than limit bytes are written
            Assert.True(result.LimitExceeded);
            Assert.True(packed.Length <= data.Length - 1);
        }

        [Fact]
        public void WhenEncodeDataLargerThanDictionaryThenMatchesBeyondDictionaryAreNotUsed()
        {
            // arrange - random block repeated with distance larger than lh5 dictionary (8 KiB) and smaller than
            // lh6 dictionary (32 KiB)
            var block = new byte[16384];
            new Random(99).NextBytes(block);
            var data = block.Concat(block).ToArray();

            // act - encode data with lh5 and lh6
            using var lh5Packed = new MemoryStream();
            var lh5Result = LhEncoder.Create(LhaMethod.Lh5)
                .Encode(new MemoryStream(data), lh5Packed, data.Length, long.MaxValue);
            using var lh6Packed = new MemoryStream();
            var lh6Result = LhEncoder.Create(LhaMethod.Lh6)
                .Encode(new MemoryStream(data), lh6Packed, data.Length, long.MaxValue);

            // assert - lh6 finds repeated block and lh5 doesn't
            Assert.True(lh5Result.PackedSize > data.Length * 0.9);
            Assert.True(lh6Result.PackedSize < data.Length * 0.6);
            Assert.Equal(data, Decode(LhaMethod.Lh5, lh5Packed.ToArray(), data.Length, lh5Result.Crc));
            Assert.Equal(data, Decode(LhaMethod.Lh6, lh6Packed.ToArray(), data.Length, lh6Result.Crc));
        }

        [Theory]
        [InlineData(LhaMethod.Lh5)]
        [InlineData(LhaMethod.Lh6)]
        [InlineData(LhaMethod.Lh7)]
        public async Task WhenReencodeEntriesFromLhaArchivesThenDataIsEqual(string method)
        {
            // arrange - extract entries from lha archives created by other lha programs
            var extensions = new[] { ".lha", ".lzh", ".lzs", ".pma" };
            var paths = Directory.GetFiles(Path.Combine("TestData", "Lha"), "*.*", SearchOption.AllDirectories)
                .Where(x => extensions.Contains(Path.GetExtension(x).ToLowerInvariant()))
                .ToList();
            Assert.NotEmpty(paths);

            foreach (var path in paths)
            {
                await using var stream = File.OpenRead(path);
                var lhaArchive = new LhaArchive(stream);
                foreach (var entry in await lhaArchive.Entries())
                {
                    if (entry.Method == LhaMethod.Lhd || entry.UnpackedSize == 0)
                    {
                        continue;
                    }

                    using var original = new MemoryStream();
                    lhaArchive.Extract(entry, original);
                    var data = original.ToArray();

                    // act - encode and decode data
                    using var packed = new MemoryStream();
                    var result = LhEncoder.Create(method)
                        .Encode(new MemoryStream(data), packed, data.Length, long.MaxValue);
                    var decoded = Decode(method, packed.ToArray(), data.Length, result.Crc);

                    // assert - decoded data is equal and crc is equal to crc from archive
                    Assert.Equal(data, decoded);
                    Assert.Equal((ushort)entry.Crc, result.Crc);
                }
            }
        }

        private static byte[] Decode(string method, byte[] packed, long size, ushort crc)
        {
            var header = new LhaEntry
            {
                Method = method,
                Name = "test.bin",
                UnpackedSize = size,
                PackedSize = packed.Length,
                HasCrc = true,
                Crc = (short)crc
            };

            using var entryStream = new LhaEntryStream(new MemoryStream(packed), header, 0, LhaOptions.Default);
            using var output = new MemoryStream();
            entryStream.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] CreateData(string name)
        {
            switch (name)
            {
                case "zeros":
                    return new byte[200000];
                case "random":
                {
                    var data = new byte[70000];
                    new Random(1).NextBytes(data);
                    return data;
                }
                case "text":
                {
                    var words = new[] { "lha", "archive", "amiga", "huffman", "sliding", "window", "\n" };
                    var random = new Random(2);
                    var text = new StringBuilder();
                    while (text.Length < 150000)
                    {
                        text.Append(words[random.Next(words.Length)]).Append(' ');
                    }

                    return Encoding.ASCII.GetBytes(text.ToString());
                }
                case "pattern":
                    return Enumerable.Range(0, 300000).Select(i => (byte)(i % 7 * 31 + i / 9973)).ToArray();
                case "mixed":
                {
                    // many short blocks of random and repeated data to produce many commands and varied tables
                    var random = new Random(3);
                    var data = new byte[400000];
                    for (var i = 0; i < data.Length; i++)
                    {
                        data[i] = random.Next(4) == 0 ? (byte)random.Next(256) : (byte)(i / 3 % 17);
                    }

                    return data;
                }
                case "single":
                    return new byte[] { 42 };
                case "two":
                    return new byte[] { 1, 2 };
                default:
                    throw new ArgumentOutOfRangeException(nameof(name));
            }
        }
    }
}
