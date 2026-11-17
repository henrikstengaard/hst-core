namespace Hst.Compression.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Compression.Lzx;
    using Xunit;

    /// <summary>
    /// Tests for behaviour shared by lha and lzx archives: leave open, repeated entry listing, cancellation and
    /// mixing iteration with random access extraction.
    /// </summary>
    public class GivenArchiveApi
    {
        private static readonly string LhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
        private static readonly string LzxPath = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");

        private static MemoryStream ReadFile(string path) => new MemoryStream(File.ReadAllBytes(path));

        [Fact]
        public void WhenDisposingLhaArchiveWithLeaveOpenThenStreamIsNotDisposed()
        {
            var stream = ReadFile(LhaPath);

            new LhaArchive(stream, LhaOptions.AmigaLhaOptions, leaveOpen: true).Dispose();

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void WhenDisposingLhaArchiveThenStreamIsDisposed()
        {
            var stream = ReadFile(LhaPath);

            new LhaArchive(stream).Dispose();

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void WhenDisposingLzxArchiveWithLeaveOpenThenStreamIsNotDisposed()
        {
            var stream = ReadFile(LzxPath);

            new LzxArchive(stream, LzxOptions.AmigaLzxOptions, leaveOpen: true).Dispose();

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void WhenDisposingLzxArchiveThenStreamIsDisposed()
        {
            var stream = ReadFile(LzxPath);

            new LzxArchive(stream).Dispose();

            Assert.False(stream.CanRead);
        }

        [Fact]
        public async Task WhenUsingDisposedArchiveThenObjectDisposedExceptionIsThrown()
        {
            var lhaArchive = new LhaArchive(ReadFile(LhaPath));
            lhaArchive.Dispose();
            var lzxArchive = new LzxArchive(ReadFile(LzxPath));
            lzxArchive.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => lhaArchive.EntriesAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => lzxArchive.EntriesAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => lzxArchive.NextAsync());
        }

        [Fact]
        public async Task WhenListingLhaEntriesTwiceThenSameEntriesAreReturned()
        {
            using var lhaArchive = new LhaArchive(ReadFile(LhaPath), LhaOptions.AmigaLhaOptions);

            var entries1 = (await lhaArchive.EntriesAsync()).Select(x => x.Name).ToList();
            var entries2 = (await lhaArchive.EntriesAsync()).Select(x => x.Name).ToList();

            Assert.NotEmpty(entries1);
            Assert.Equal(entries1, entries2);
        }

        [Fact]
        public async Task WhenListingLzxEntriesTwiceThenSameEntriesAreReturned()
        {
            using var lzxArchive = new LzxArchive(ReadFile(LzxPath), LzxOptions.AmigaLzxOptions);

            var entries1 = (await lzxArchive.EntriesAsync()).Select(x => x.Name).ToList();
            var entries2 = (await lzxArchive.EntriesAsync()).Select(x => x.Name).ToList();

            Assert.NotEmpty(entries1);
            Assert.Equal(entries1, entries2);
        }

        [Fact]
        public async Task WhenExtractingCurrentLzxEntryBeforeNextThenInvalidOperationExceptionIsThrown()
        {
            using var lzxArchive = new LzxArchive(ReadFile(LzxPath), LzxOptions.AmigaLzxOptions);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lzxArchive.ExtractCurrentAsync(new MemoryStream()));
        }

        [Fact]
        public async Task WhenExtractingCurrentLzxEntryAfterEndOfArchiveThenInvalidOperationExceptionIsThrown()
        {
            using var lzxArchive = new LzxArchive(ReadFile(LzxPath), LzxOptions.AmigaLzxOptions);
            while (await lzxArchive.NextAsync() != null)
            {
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lzxArchive.ExtractCurrentAsync(new MemoryStream()));
        }

        [Fact]
        public async Task WhenExtractingLzxEntriesByIterationAndByEntryThenDataIsEqual()
        {
            using var lzxArchive = new LzxArchive(ReadFile(LzxPath), LzxOptions.AmigaLzxOptions);

            var count = 0;
            while (await lzxArchive.NextAsync() is { } entry)
            {
                // extracting by entry in between iterating must not affect iteration
                using var byEntry = new MemoryStream();
                await lzxArchive.ExtractAsync(entry, byEntry);

                using var byIteration = new MemoryStream();
                await lzxArchive.ExtractCurrentAsync(byIteration);

                Assert.Equal(entry.UnpackedSize, byIteration.Length);
                Assert.Equal(byIteration.ToArray(), byEntry.ToArray());
                count++;
            }

            Assert.True(count > 1);
        }

        [Fact]
        public async Task WhenCancelledThenArchiveOperationsAreCancelled()
        {
            using var lhaArchive = new LhaArchive(ReadFile(LhaPath), LhaOptions.AmigaLhaOptions);
            using var lzxArchive = new LzxArchive(ReadFile(LzxPath), LzxOptions.AmigaLzxOptions);
            var lhaEntry = (await lhaArchive.EntriesAsync()).First();
            var lzxEntry = (await lzxArchive.EntriesAsync()).First();
            var token = new CancellationToken(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lhaArchive.EntriesAsync(token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                lhaArchive.ExtractAsync(lhaEntry, new MemoryStream(), token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lzxArchive.EntriesAsync(token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lzxArchive.NextAsync(token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                lzxArchive.ExtractAsync(lzxEntry, new MemoryStream(), token));
        }
    }
}
