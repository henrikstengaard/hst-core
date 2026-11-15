namespace Hst.Compression.Tests.Lha
{
    using System;
    using System.IO;
    using System.Linq;
    using Compression.Lha;
    using Xunit;

    /// <summary>
    /// Tests for building blocks of the lha decoders.
    /// </summary>
    public class GivenLhaDecoders
    {
        private static BitReader CreateBitReader(string bits, bool zeroPadding = false)
        {
            var bytes = new byte[(bits.Length + 7) / 8];
            for (var i = 0; i < bits.Length; i++)
            {
                if (bits[i] == '1')
                {
                    bytes[i / 8] |= (byte)(0x80 >> (i % 8));
                }
            }

            return new BitReader(new MemoryStream(bytes), zeroPadding);
        }

        [Fact]
        public void WhenReadBitsThenBitsAreReadMostSignificantBitFirst()
        {
            var reader = CreateBitReader("101" + "11110000" + "0110");

            Assert.Equal(5, reader.Read(3));
            Assert.Equal(0xf0, reader.Peek(8));
            Assert.Equal(0xf0, reader.Read(8));
            Assert.Equal(0, reader.ReadBit());
            Assert.Equal(3, reader.Read(2));
        }

        [Fact]
        public void WhenReadPastEndOfDataThenExceptionIsThrown()
        {
            var reader = CreateBitReader("10101010");

            // peeking past end is allowed for decoding codes at end of data
            Assert.Equal(0xaa00, reader.Peek(16));
            Assert.Equal(0xaa, reader.Read(8));
            Assert.Throws<IOException>(() => reader.Read(1));
        }

        [Fact]
        public void WhenReadPastEndOfDataWithZeroPaddingThenZerosAreRead()
        {
            var reader = CreateBitReader("11111111", true);

            Assert.Equal(0xff, reader.Read(8));
            Assert.Equal(0, reader.Read(16));
            Assert.Equal(0, reader.Read(16));
        }

        [Fact]
        public void WhenDecodeCanonicalCodesThenSymbolsAreDecoded()
        {
            // lengths: A=2, B=1, C=3, D=3 gives canonical codes B=0, A=10, C=110, D=111
            var decoder = new PrefixCodeDecoder(4);
            decoder.Build(new byte[] { 2, 1, 3, 3 }, 4);

            var reader = CreateBitReader("0" + "10" + "110" + "111" + "0");

            Assert.Equal(new[] { 1, 0, 2, 3, 1 }, Enumerable.Range(0, 5).Select(_ => decoder.Decode(reader)));
        }

        [Fact]
        public void WhenDecodeCodesLongerThanLookupTableThenSymbolsAreDecoded()
        {
            // lengths 1, 2, ..., 15, 15 is a complete code table with codes up to 15 bits
            var lengths = Enumerable.Range(1, 15).Select(x => (byte)x).Concat(new byte[] { 15 }).ToArray();
            var decoder = new PrefixCodeDecoder(lengths.Length);
            decoder.Build(lengths, lengths.Length);

            // code for symbol n < 15 is n ones followed by a zero, symbols 14 and 15 are 14 ones followed by 0 or 1
            var bits = string.Concat(Enumerable.Range(0, 15).Select(n => new string('1', n) + "0")) +
                       new string('1', 15);
            var reader = CreateBitReader(bits);

            Assert.Equal(Enumerable.Range(0, 16), Enumerable.Range(0, 16).Select(_ => decoder.Decode(reader)));
        }

        [Fact]
        public void WhenDecodeSingleSymbolThenNoBitsAreRead()
        {
            var decoder = new PrefixCodeDecoder(10);
            decoder.SetSingle(7);
            var reader = CreateBitReader("1");

            Assert.Equal(7, decoder.Decode(reader));
            Assert.Equal(1, reader.ReadBit());
        }

        [Fact]
        public void WhenDecodeUnassignedCodeOfIncompleteCodeTableThenSymbolZeroIsDecoded()
        {
            // only symbol 2 is used with code 0, code 1 is unassigned
            var decoder = new PrefixCodeDecoder(3);
            decoder.Build(new byte[] { 0, 0, 1 }, 3);
            var reader = CreateBitReader("01");

            Assert.Equal(2, decoder.Decode(reader));
            Assert.Equal(0, decoder.Decode(reader));
        }

        [Fact]
        public void WhenBuildCodeTableWithTooManyCodesThenExceptionIsThrown()
        {
            var decoder = new PrefixCodeDecoder(3);

            Assert.Throws<IOException>(() => decoder.Build(new byte[] { 1, 1, 1 }, 3));
        }

        [Fact]
        public void WhenMoveByteValueToFrontThenOtherValuesAreShifted()
        {
            var list = new MoveToFrontList();

            // initial order starts with printable ascii followed by control characters
            Assert.Equal(0x20, list[0]);
            Assert.Equal(0x7f, list[0x5f]);
            Assert.Equal(0x00, list[0x60]);

            list.MoveToFront(0x41);

            Assert.Equal(0x41, list[0]);
            Assert.Equal(0x20, list[1]);
            Assert.Equal(0x40, list[0x21]);
            Assert.Equal(0x42, list[0x22]);
        }

        [Theory]
        [InlineData(LhaMethod.Lh5)]
        [InlineData(LhaMethod.Lh7)]
        public void WhenExtractTruncatedCompressedDataThenExceptionIsThrown(string method)
        {
            // arrange - compress data and truncate compressed data
            var data = Enumerable.Range(0, 20000).Select(x => (byte)(x * 7 % 251)).ToArray();
            var packed = new MemoryStream();
            var result = LhEncoder.Create(method).Encode(new MemoryStream(data), packed, data.Length, long.MaxValue);
            var truncated = packed.ToArray().Take((int)result.PackedSize / 2).ToArray();
            var entry = new LhaEntry
            {
                Method = method,
                Name = "test.bin",
                UnpackedSize = data.Length,
                PackedSize = truncated.Length,
                HasCrc = true,
                Crc = (short)result.Crc
            };

            // act & assert - reading entry throws exception
            using var entryStream = new LhaEntryStream(new MemoryStream(truncated), entry, 0, LhaOptions.Default);
            Assert.ThrowsAny<IOException>(() => entryStream.CopyTo(new MemoryStream()));
        }
    }
}
