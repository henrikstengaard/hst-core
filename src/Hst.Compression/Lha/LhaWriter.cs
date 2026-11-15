namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Writes (creates) lha archives by adding files and directories, which are compressed as they are added.
    /// </summary>
    public class LhaWriter : IDisposable
    {
        private const int FILE_ATTRIBUTE = 0x20;
        private const int DIRECTORY_ATTRIBUTE = 0x10;
        private const ushort FILE_MODE = LhaUnixMode.Regular | 0x1a4; /* 0644 */
        private const ushort DIRECTORY_MODE = LhaUnixMode.Directory | 0x1ed; /* 0755 */

        private readonly Stream stream;
        private readonly LhaWriterOptions options;
        private readonly bool leaveOpen;
        private readonly HeaderWriter headerWriter;
        private bool finished;
        private bool disposed;

        /// <summary>
        /// Create lha writer with default options.
        /// </summary>
        /// <param name="stream">Stream to write lha archive to.</param>
        public LhaWriter(Stream stream) : this(stream, LhaWriterOptions.Default)
        {
        }

        /// <summary>
        /// Create lha writer.
        /// </summary>
        /// <param name="stream">Stream to write lha archive to.</param>
        /// <param name="options">Options for writing lha archive.</param>
        /// <param name="leaveOpen">Leave stream open, when lha writer is disposed.</param>
        public LhaWriter(Stream stream, LhaWriterOptions options, bool leaveOpen = false)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanWrite)
            {
                throw new ArgumentException("Stream must be writable", nameof(stream));
            }

            this.stream = stream;
            this.options = options ?? LhaWriterOptions.Default;
            this.leaveOpen = leaveOpen;

            if (this.options.HeaderLevel < 0 || this.options.HeaderLevel > 2)
            {
                throw new ArgumentException($"Header level {this.options.HeaderLevel} is not supported",
                    nameof(options));
            }

            ValidateMethod(this.options.Method);

            headerWriter = new HeaderWriter(this.options.Encoding, !this.options.GenericFormat);
        }

        private static void ValidateMethod(string method)
        {
            switch (method)
            {
                case LhaMethod.Lh0:
                case LhaMethod.Lh4:
                case LhaMethod.Lh5:
                case LhaMethod.Lh6:
                case LhaMethod.Lh7:
                    return;
                default:
                    throw new NotSupportedException($"Compression method \"{method}\" is not supported");
            }
        }

        private LhaEntry CreateHeader(string name, DateTime lastModified)
        {
            return new LhaEntry
            {
                Method = LhaMethod.Lh0,
                Attributes = FILE_ATTRIBUTE,
                HeaderLevel = options.HeaderLevel,
                Name = name.Replace('/', '\\'),
                HasCrc = true,
                OsType = options.GenericFormat ? LhaOsType.Generic : LhaOsType.Unix,
                Date = lastModified,
                UnixMode = FILE_MODE
            };
        }

        private void EnsureWritable()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LhaWriter));
            }

            if (finished)
            {
                throw new InvalidOperationException("Lha archive is finished");
            }
        }

        private void WriteHeader(LhaEntry header, Stream output)
        {
            header.HeaderSize = headerWriter.Write(header, output);
        }

        /// <summary>
        /// Add directory entry to lha archive.
        /// </summary>
        /// <param name="name">Name of directory. Path separators '/' and '\' are supported.</param>
        /// <param name="lastModified">Last modified date of directory. Default is now.</param>
        /// <returns>Header of entry added.</returns>
        public LhaEntry AddDirectory(string name, DateTime? lastModified = null)
        {
            EnsureWritable();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var header = CreateHeader(name, lastModified ?? DateTime.Now);
            header.Method = LhaMethod.Lhd;
            header.Attributes = DIRECTORY_ATTRIBUTE;
            header.UnixMode = DIRECTORY_MODE;
            if (!header.Name.EndsWith("\\", StringComparison.Ordinal))
            {
                header.Name += "\\";
            }

            header.HeaderOffset = stream.CanSeek ? stream.Position : 0;
            WriteHeader(header, stream);

            return header;
        }

        /// <summary>
        /// Add file entry to lha archive. Data is read from current position to end of data stream and compressed
        /// using method from options. If compressed data is not smaller than original data, data is stored using
        /// method -lh0- instead.
        /// </summary>
        /// <param name="name">Name of file. Path separators '/' and '\' are supported.</param>
        /// <param name="data">Stream with data of file.</param>
        /// <param name="lastModified">Last modified date of file. Default is now.</param>
        /// <param name="method">Compression method to use, overrides method from options.</param>
        /// <returns>Header of entry added.</returns>
        public LhaEntry AddFile(string name, Stream data, DateTime? lastModified = null, string method = null)
        {
            EnsureWritable();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            method = method ?? options.Method;
            ValidateMethod(method);

            // data stream must be seekable to store data, if it can't be compressed
            MemoryStream dataBuffer = null;
            if (!data.CanSeek)
            {
                dataBuffer = new MemoryStream();
                data.CopyTo(dataBuffer);
                dataBuffer.Position = 0;
                data = dataBuffer;
            }

            try
            {
                var size = data.Length - data.Position;
                if (size > uint.MaxValue)
                {
                    throw new ArgumentException("Files larger than 4GB are not supported", nameof(data));
                }

                var header = CreateHeader(name, lastModified ?? DateTime.Now);
                header.UnpackedSize = size;
                header.HeaderOffset = stream.CanSeek ? stream.Position : 0;

                // validate header before data is written
                headerWriter.Write(header);

                if (size == 0)
                {
                    WriteHeader(header, stream);
                    return header;
                }

                if (stream.CanSeek)
                {
                    // write header to reserve space, header is written again when packed size and crc is known
                    var headerOffset = stream.Position;
                    WriteHeader(header, stream);
                    WriteData(header, data, stream, method, size);
                    var nextOffset = stream.Position;
                    stream.Seek(headerOffset, SeekOrigin.Begin);
                    WriteHeader(header, stream);
                    stream.Seek(nextOffset, SeekOrigin.Begin);
                }
                else
                {
                    // compress data into buffer before writing header and data to non-seekable stream
                    using (var packedBuffer = new MemoryStream())
                    {
                        WriteData(header, data, packedBuffer, method, size);
                        WriteHeader(header, stream);
                        packedBuffer.Position = 0;
                        packedBuffer.CopyTo(stream);
                    }
                }

                return header;
            }
            finally
            {
                dataBuffer?.Dispose();
            }
        }

        /// <summary>
        /// Write data compressed with method or stored, if compressed data isn't smaller than original data.
        /// Header is updated with method, crc and sizes.
        /// </summary>
        private static void WriteData(LhaEntry header, Stream data, Stream output, string method, long size)
        {
            var dataOffset = data.Position;
            var outputOffset = output.Position;

            if (method != LhaMethod.Lh0)
            {
                // compressed data is limited to less than original size, so stored data overwrites it completely
                var result = LhEncoder.Create(method).Encode(data, output, size, size - 1);
                if (!result.LimitExceeded)
                {
                    header.Method = method;
                    header.Crc = (short)result.Crc;
                    header.PackedSize = result.PackedSize;
                    return;
                }

                data.Seek(dataOffset, SeekOrigin.Begin);
                output.Seek(outputOffset, SeekOrigin.Begin);
            }

            ushort crc = 0;
            var buffer = new byte[32768];
            var remaining = size;
            while (remaining > 0)
            {
                var bytesRead = data.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of data");
                }

                crc = Crc16.Update(crc, buffer, 0, bytesRead);
                output.Write(buffer, 0, bytesRead);
                remaining -= bytesRead;
            }

            header.Method = LhaMethod.Lh0;
            header.Crc = (short)crc;
            header.PackedSize = size;
        }

        /// <summary>
        /// Finish lha archive by writing end of archive marker. No entries can be added after lha archive is finished.
        /// </summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LhaWriter));
            }

            stream.WriteByte(0);
            stream.Flush();
            finished = true;
        }

        /// <summary>
        /// Finish lha archive and dispose stream, unless it's left open.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Finish();
            disposed = true;

            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }
    }
}
