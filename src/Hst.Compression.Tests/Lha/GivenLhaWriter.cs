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

    public class GivenLhaWriter
    {
        private static readonly DateTime LastModified = new DateTime(2024, 5, 17, 13, 37, 42, DateTimeKind.Utc);

        public static IEnumerable<object[]> MethodsAndHeaderLevels =>
            from method in new[]
            {
                LhaMethod.Lh0, LhaMethod.Lh4,
                LhaMethod.Lh5, LhaMethod.Lh6, LhaMethod.Lh7
            }
            from headerLevel in new[] { 0, 1, 2 }
            select new object[] { method, headerLevel };

        [Theory]
        [MemberData(nameof(MethodsAndHeaderLevels))]
        public async Task WhenWriteEntriesAndReadThenEntriesAndDataAreEqual(string method, int headerLevel)
        {
            // arrange - files to add
            var files = new Dictionary<string, byte[]>
            {
                { "text.txt", CreateText(20000) },
                { @"dir\sub\large.bin", CreateLargeData(300000) },
                { "dir/random.bin", CreateRandomData(10000) },
                { "empty.txt", Array.Empty<byte>() },
                { "one.txt", new byte[] { 0x41 } },
                { "three.txt", new byte[] { 0x41, 0x42, 0x41 } }
            };

            // act - write lha archive
            using var archiveStream = new MemoryStream();
            using (var lhaWriter = new LhaWriter(archiveStream,
                       new LhaWriterOptions { Method = method, HeaderLevel = headerLevel }, true))
            {
                lhaWriter.AddDirectory("dir", LastModified);
                lhaWriter.AddDirectory(@"dir\sub\", LastModified);
                foreach (var file in files)
                {
                    lhaWriter.AddFile(file.Key, new MemoryStream(file.Value), LastModified);
                }
            }

            // act - read lha archive
            archiveStream.Position = 0;
            var lhaArchive = new LhaArchive(archiveStream);
            var entries = (await lhaArchive.EntriesAsync()).ToList();

            // assert - directories are read
            Assert.Equal(files.Count + 2, entries.Count);
            var dirEntry = Assert.Single(entries, x => x.Name == @"dir\");
            Assert.Equal(LhaMethod.Lhd, dirEntry.Method);
            Assert.Equal(headerLevel, dirEntry.HeaderLevel);
            Assert.Equal(0x10, dirEntry.Attributes);
            var subDirEntry = Assert.Single(entries, x => x.Name == @"dir\sub\");
            Assert.Equal(LhaMethod.Lhd, subDirEntry.Method);

            // assert - files are read with equal data
            foreach (var file in files)
            {
                var name = file.Key.Replace('/', '\\');
                var entry = Assert.Single(entries, x => x.Name == name);
                Assert.Equal(headerLevel, entry.HeaderLevel);
                Assert.Equal(file.Value.Length, entry.UnpackedSize);
                Assert.Equal(LastModified, entry.Date);

                using var output = new MemoryStream();
                await lhaArchive.ExtractAsync(entry, output);
                Assert.Equal(file.Value, output.ToArray());
            }

            // assert - compressible data is compressed with method
            var textEntry = entries.First(x => x.Name == "text.txt");
            var largeEntry = entries.First(x => x.Name == @"dir\sub\large.bin");
            Assert.Equal(method, textEntry.Method);
            Assert.Equal(method, largeEntry.Method);
            if (method != LhaMethod.Lh0)
            {
                Assert.True(textEntry.PackedSize < textEntry.UnpackedSize / 2);
                Assert.True(largeEntry.PackedSize < largeEntry.UnpackedSize);
            }

            // assert - incompressible and empty data is stored
            Assert.Equal(LhaMethod.Lh0, entries.First(x => x.Name == @"dir\random.bin").Method);
            Assert.Equal(LhaMethod.Lh0, entries.First(x => x.Name == "empty.txt").Method);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task WhenWriteGenericFormatEntriesAndReadThenDataIsEqual(int headerLevel)
        {
            // arrange - data to add
            var data = CreateText(5000);

            // act - write lha archive with generic headers
            using var archiveStream = new MemoryStream();
            using (var lhaWriter = new LhaWriter(archiveStream,
                       new LhaWriterOptions { HeaderLevel = headerLevel, GenericFormat = true }, true))
            {
                lhaWriter.AddFile(@"dir\file.txt", new MemoryStream(data), LastModified);
            }

            // act - read lha archive
            archiveStream.Position = 0;
            var lhaArchive = new LhaArchive(archiveStream);
            var entry = Assert.Single(await lhaArchive.EntriesAsync());

            // assert - entry is read with equal data
            Assert.Equal(@"dir\file.txt", entry.Name);
            Assert.Equal(LhaOsType.Generic, entry.OsType);
            Assert.Equal(LhaMethod.Lh5, entry.Method);
            using var output = new MemoryStream();
            await lhaArchive.ExtractAsync(entry, output);
            Assert.Equal(data, output.ToArray());
        }

        [Fact]
        public async Task WhenWriteToNonSeekableStreamFromNonSeekableStreamThenDataIsEqual()
        {
            // arrange - data to add
            var text = CreateText(30000);
            var random = CreateRandomData(5000);

            // act - write lha archive to non-seekable stream reading data from non-seekable streams
            using var archiveStream = new MemoryStream();
            using (var lhaWriter = new LhaWriter(new NonSeekableStream(archiveStream), LhaWriterOptions.Default))
            {
                lhaWriter.AddFile("text.txt", new NonSeekableStream(new MemoryStream(text)), LastModified);
                lhaWriter.AddFile("random.bin", new NonSeekableStream(new MemoryStream(random)), LastModified);
            }

            // act - read lha archive
            var lhaArchive = new LhaArchive(new MemoryStream(archiveStream.ToArray()));
            var entries = (await lhaArchive.EntriesAsync()).ToList();

            // assert - entries are read with equal data
            Assert.Equal(2, entries.Count);
            Assert.Equal(LhaMethod.Lh5, entries[0].Method);
            Assert.Equal(LhaMethod.Lh0, entries[1].Method);
            using var textOutput = new MemoryStream();
            await lhaArchive.ExtractAsync(entries[0], textOutput);
            Assert.Equal(text, textOutput.ToArray());
            using var randomOutput = new MemoryStream();
            await lhaArchive.ExtractAsync(entries[1], randomOutput);
            Assert.Equal(random, randomOutput.ToArray());
        }

        [Fact]
        public async Task WhenReadEntryStreamFromNonSeekableStreamThenDataIsEqual()
        {
            // arrange - write lha archive
            var text = CreateText(30000);
            using var archiveStream = new MemoryStream();
            LhaEntry header;
            using (var lhaWriter = new LhaWriter(archiveStream, LhaWriterOptions.Default, true))
            {
                header = lhaWriter.AddFile("text.txt", new MemoryStream(text), LastModified);
            }

            // act - extract entry from non-seekable stream positioned at entry data
            var input = new NonSeekableStream(new MemoryStream(archiveStream.ToArray(), (int)header.HeaderSize,
                (int)header.PackedSize));
            using var output = new MemoryStream();
            using (var entryStream = new LhaEntryStream(input, header))
            {
                entryStream.CopyTo(output);
            }

            // assert - extracted data is equal
            Assert.Equal(text, output.ToArray());
            await Task.CompletedTask;
        }

        [Theory]
        [InlineData(0, 200)]
        [InlineData(1, 250)]
        [InlineData(2, 250)]
        public async Task WhenWriteEntriesWithNameLengthsThenEntriesAreRead(int headerLevel, int maxNameLength)
        {
            // arrange - names with different lengths to cover level 1 long name and level 2 header padding
            var names = Enumerable.Range(1, maxNameLength)
                .Select(length => string.Concat("d", "\\", new string((char)('a' + length % 26), length)))
                .ToList();
            var data = Encoding.ASCII.GetBytes("hello hello hello hello");

            // act - write lha archive
            using var archiveStream = new MemoryStream();
            using (var lhaWriter = new LhaWriter(archiveStream,
                       new LhaWriterOptions { HeaderLevel = headerLevel }, true))
            {
                foreach (var name in names)
                {
                    lhaWriter.AddFile(name, new MemoryStream(data), LastModified);
                }
            }

            // act - read lha archive
            archiveStream.Position = 0;
            var lhaArchive = new LhaArchive(archiveStream);
            var entries = (await lhaArchive.EntriesAsync()).ToList();

            // assert - entries are read with equal names and data
            Assert.Equal(names, entries.Select(x => x.Name));
            foreach (var entry in entries)
            {
                using var output = new MemoryStream();
                await lhaArchive.ExtractAsync(entry, output);
                Assert.Equal(data, output.ToArray());
            }
        }

        [Fact]
        public void WhenWriteLevel0EntryWithTooLongNameThenExceptionIsThrown()
        {
            using var archiveStream = new MemoryStream();
            using var lhaWriter = new LhaWriter(archiveStream, new LhaWriterOptions { HeaderLevel = 0 }, true);

            Assert.Throws<ArgumentException>(() =>
                lhaWriter.AddFile(new string('a', 222), new MemoryStream(new byte[] { 1 }), LastModified));
        }

        [Theory]
        [InlineData(LhaMethod.Lh1)]
        [InlineData(LhaMethod.Lh2)]
        [InlineData(LhaMethod.Lh3)]
        [InlineData(LhaMethod.Lz5)]
        [InlineData(LhaMethod.Pm2)]
        public void WhenWriteWithUnsupportedCompressionMethodThenExceptionIsThrown(string method)
        {
            using var archiveStream = new MemoryStream();
            Assert.Throws<NotSupportedException>(() =>
                new LhaWriter(archiveStream, new LhaWriterOptions { Method = method }));
        }

        private static byte[] CreateText(int size)
        {
            var words = new[]
            {
                "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog", "lha", "archive", "compression",
                "amiga", "sliding", "dictionary", "huffman", "\n"
            };
            var random = new Random(42);
            var text = new StringBuilder();
            while (text.Length < size)
            {
                text.Append(words[random.Next(words.Length)]).Append(' ');
            }

            return Encoding.ASCII.GetBytes(text.ToString(0, size));
        }

        private static byte[] CreateRandomData(int size)
        {
            var data = new byte[size];
            new Random(1234).NextBytes(data);
            return data;
        }

        private static byte[] CreateLargeData(int size)
        {
            // mix of repeating patterns, text and random blocks to exceed dictionary and block sizes
            var random = new Random(4321);
            var text = CreateText(size);
            var data = new byte[size];
            for (var i = 0; i < size; i++)
            {
                var block = i / 4096 % 3;
                data[i] = block == 0 ? (byte)(i % 251) : block == 1 ? text[i] : (byte)random.Next(256);
            }

            return data;
        }

        private class NonSeekableStream : Stream
        {
            private readonly Stream stream;

            public NonSeekableStream(Stream stream) => this.stream = stream;

            public override bool CanRead => stream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => stream.CanWrite;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => stream.Flush();
            public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);
        }
    }
}
