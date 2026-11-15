/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              lhadd.c -- LHarc Add Command                                */
/*              append.c -- append to archive                               */
/*                                                                          */
/*      Copyright (C) MCMLXXXIX Yooichi.Tagawa                              */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Writes (creates) lha archives by adding files and directories, which are compressed as they are added.
    /// </summary>
    public class LhaWriter : IDisposable
    {
        private const int GENERIC_ATTRIBUTE = 0x20;
        private const int GENERIC_DIRECTORY_ATTRIBUTE = 0x10;
        private const ushort DEFAULT_FILE_MODE = Constants.UNIX_FILE_REGULAR | 0x1a4; /* 0644 */
        private const ushort DEFAULT_DIRECTORY_MODE = Constants.UNIX_FILE_DIRECTORY | 0x1ed; /* 0755 */

        private readonly Stream stream;
        private readonly LhaWriterOptions options;
        private readonly bool leaveOpen;
        private readonly HeaderWriter headerWriter;
        private bool finished;
        private bool disposed;

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

            GetCompressMethodNum(this.options.Method);

            headerWriter = new HeaderWriter(this.options.Encoding, this.options.GenericFormat);
        }

        private static int GetCompressMethodNum(string method)
        {
            switch (method)
            {
                case Constants.LZHUFF0_METHOD:
                    return Constants.LZHUFF0_METHOD_NUM;
                case Constants.LZHUFF1_METHOD:
                    return Constants.LZHUFF1_METHOD_NUM;
                case Constants.LZHUFF4_METHOD:
                    return Constants.LZHUFF4_METHOD_NUM;
                case Constants.LZHUFF5_METHOD:
                    return Constants.LZHUFF5_METHOD_NUM;
                case Constants.LZHUFF6_METHOD:
                    return Constants.LZHUFF6_METHOD_NUM;
                case Constants.LZHUFF7_METHOD:
                    return Constants.LZHUFF7_METHOD_NUM;
                default:
                    throw new NotSupportedException($"Compression method \"{method}\" is not supported");
            }
        }

        private static string NormalizeName(string name) => name.Replace('/', '\\');

        private LzHeader CreateHeader(string name, DateTime lastModified)
        {
            return new LzHeader
            {
                Method = Constants.LZHUFF0_METHOD,
                PackedSize = 0,
                OriginalSize = 0,
                Attribute = GENERIC_ATTRIBUTE,
                HeaderLevel = options.HeaderLevel,
                Name = NormalizeName(name),
                Crc = 0,
                HasCrc = true,
                ExtendType = options.GenericFormat ? Constants.EXTEND_GENERIC : Constants.EXTEND_UNIX,
                UnixLastModifiedStamp = lastModified,
                UnixMode = DEFAULT_FILE_MODE,
                UnixUid = 0,
                UnixGid = 0
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

        private void WriteHeader(LzHeader hdr)
        {
            var headerBytes = headerWriter.Write(hdr);
            hdr.HeaderSize = headerBytes.Length;
            stream.Write(headerBytes, 0, headerBytes.Length);
        }

        /// <summary>
        /// Add directory entry to lha archive.
        /// </summary>
        /// <param name="name">Name of directory. Path separators '/' and '\' are supported.</param>
        /// <param name="lastModified">Last modified date of directory. Default is now.</param>
        /// <returns>Header of entry added.</returns>
        public LzHeader AddDirectory(string name, DateTime? lastModified = null)
        {
            EnsureWritable();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var hdr = CreateHeader(name, lastModified ?? DateTime.Now);
            hdr.Method = Constants.LZHDIRS_METHOD;
            hdr.Attribute = GENERIC_DIRECTORY_ATTRIBUTE;
            hdr.UnixMode = DEFAULT_DIRECTORY_MODE;
            if (!hdr.Name.EndsWith("\\", StringComparison.Ordinal))
            {
                hdr.Name += "\\";
            }

            hdr.HeaderOffset = stream.CanSeek ? stream.Position : 0;
            WriteHeader(hdr);

            return hdr;
        }

        /// <summary>
        /// Add file entry to lha archive. Data is read from current position to end of data stream and compressed
        /// using method from options. If compressed data is not smaller than original data, data is stored using
        /// method lh0 instead.
        /// </summary>
        /// <param name="name">Name of file. Path separators '/' and '\' are supported.</param>
        /// <param name="data">Stream with data of file.</param>
        /// <param name="lastModified">Last modified date of file. Default is now.</param>
        /// <param name="method">Compression method to use, overrides method from options.</param>
        /// <returns>Header of entry added.</returns>
        public LzHeader AddFile(string name, Stream data, DateTime? lastModified = null, string method = null)
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

            var methodNum = GetCompressMethodNum(method ?? options.Method);

            // data stream must be seekable to retry with stored method, if data can't be compressed
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
                if (size > int.MaxValue)
                {
                    throw new ArgumentException("Files larger than 2GB are not supported", nameof(data));
                }

                var hdr = CreateHeader(name, lastModified ?? DateTime.Now);
                hdr.OriginalSize = size;
                hdr.HeaderOffset = stream.CanSeek ? stream.Position : 0;

                if (size == 0)
                {
                    /* empty file */
                    WriteHeader(hdr);
                    return hdr;
                }

                if (stream.CanSeek)
                {
                    var headerPos = stream.Position;
                    WriteHeader(hdr); /* DUMMY */
                    EncodeData(hdr, data, stream, methodNum, size);
                    var nextPos = stream.Position;
                    stream.Seek(headerPos, SeekOrigin.Begin);
                    WriteHeader(hdr);
                    stream.Seek(nextPos, SeekOrigin.Begin);
                }
                else
                {
                    // non-seekable stream, compress data into buffer before writing header and data
                    using (var packedBuffer = new MemoryStream())
                    {
                        EncodeData(hdr, data, packedBuffer, methodNum, size);
                        WriteHeader(hdr);
                        packedBuffer.Position = 0;
                        packedBuffer.CopyTo(stream);
                    }
                }

                return hdr;
            }
            finally
            {
                dataBuffer?.Dispose();
            }
        }

        /// <summary>
        /// Encode data and update header with method, crc and sizes. Equivalent of add_one in lhadd.c.
        /// </summary>
        private static void EncodeData(LzHeader hdr, Stream data, Stream output, int methodNum, long size)
        {
            var orgPos = data.Position;
            var dataPos = output.Position;

            if (methodNum > 0)
            {
                var result = Slide.Encode(data, output, methodNum, size);
                if (!result.Unpackable && result.PackedSize < result.OriginalSize)
                {
                    hdr.Method = $"-lh{methodNum}-";
                    hdr.Crc = (short)result.Crc;
                    hdr.OriginalSize = result.OriginalSize;
                    hdr.PackedSize = result.PackedSize;
                    return;
                }

                /* retry by stored method */
                data.Seek(orgPos, SeekOrigin.Begin);
                output.Seek(dataPos, SeekOrigin.Begin);
            }

            var crcIo = new CrcIo();
            crcIo.InitializeCrc();
            /* stored data overwrites compressed data written, which is never larger than original size */
            var storedSize = Util.CopyFile(data, output, (int)size, false, crcIo);

            hdr.Method = Constants.LZHUFF0_METHOD;
            hdr.Crc = (short)crcIo.crc;
            hdr.OriginalSize = storedSize;
            hdr.PackedSize = storedSize;
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
