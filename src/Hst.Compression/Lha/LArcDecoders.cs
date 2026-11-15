namespace Hst.Compression.Lha
{
    using System.IO;

    /// <summary>
    /// Decoder for the -lzs- method used by LArc. Each command starts with a flag bit: 1 is followed by an 8 bit
    /// literal, 0 is followed by an 11 bit absolute window position and a 4 bit copy length from 2 to 17.
    /// Window is 2 KiB filled with spaces and decoding starts 17 bytes before end of window.
    /// </summary>
    internal sealed class LzsDecoder : WindowDecoder
    {
        private readonly BitReader reader;

        public LzsDecoder(Stream input) : base(11, (byte)' ')
        {
            reader = new BitReader(input);
            WindowPosition = Window.Length - 17;
        }

        protected override bool DecodeCommand()
        {
            if (reader.ReadBit() == 1)
            {
                Literal((byte)reader.Read(8));
                return true;
            }

            var position = reader.Read(11);
            var length = reader.Read(4) + 2;
            CopyFromWindow(position, length);
            return true;
        }
    }

    /// <summary>
    /// Decoder for the -lz5- method used by LArc. Commands are byte aligned in groups of 8 with a preceding flag byte,
    /// where each bit from least significant bit is the type of a command: 1 is a literal byte, 0 is a 2 byte copy with
    /// a 12 bit absolute window position and a 4 bit copy length from 3 to 18.
    /// Window is 4 KiB with initial content useful for runs and decoding starts 18 bytes before end of window.
    /// </summary>
    internal sealed class Lz5Decoder : WindowDecoder
    {
        private readonly BitReader reader;
        private int flags;
        private int remainingFlags;

        public Lz5Decoder(Stream input) : base(12, 0)
        {
            reader = new BitReader(input);
            InitializeWindow(Window);
            WindowPosition = Window.Length - 18;
        }

        private static void InitializeWindow(byte[] window)
        {
            var position = 0;

            // runs of 13 bytes with each value
            for (var value = 0; value < 256; value++)
            {
                for (var i = 0; i < 13; i++)
                {
                    window[position++] = (byte)value;
                }
            }

            // all values increasing and then decreasing
            for (var value = 0; value < 256; value++)
            {
                window[position++] = (byte)value;
            }

            for (var value = 255; value >= 0; value--)
            {
                window[position++] = (byte)value;
            }

            // 128 zeros followed by 110 spaces, remaining 18 bytes are zeros
            position += 128;
            for (var i = 0; i < 110; i++)
            {
                window[position++] = (byte)' ';
            }
        }

        protected override bool DecodeCommand()
        {
            if (remainingFlags == 0)
            {
                flags = reader.Read(8);
                remainingFlags = 8;
            }

            var isLiteral = (flags & 1) != 0;
            flags >>= 1;
            remainingFlags--;

            if (isLiteral)
            {
                Literal((byte)reader.Read(8));
                return true;
            }

            var low = reader.Read(8);
            var high = reader.Read(8);
            CopyFromWindow(((high & 0xf0) << 4) | low, (high & 0x0f) + 3);
            return true;
        }
    }
}
