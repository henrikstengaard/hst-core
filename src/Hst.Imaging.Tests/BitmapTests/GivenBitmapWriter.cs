namespace Hst.Imaging.Tests.BitmapTests
{
    using System.IO;
    using System.Threading.Tasks;
    using Bitmaps;
    using Xunit;

    public class GivenBitmapWriter : ImageTestBase
    {
        [Fact]
        public async Task WhenWriteImageWith8BitsPerPixelThenBytesMatchBitmapFileStructure()
        {
            // arrange - create new image 
            var image = Create8BppImage(false);

            // act - write image as bitmap to stream
            var bitmapStream = new MemoryStream();
            BitmapWriter.Write(bitmapStream, image);

            var bitmapBytes = bitmapStream.ToArray();
            await File.WriteAllBytesAsync("8bpp.bmp", bitmapBytes);

            // assert - read file and info headers
            bitmapStream.Position = 0;
            var binaryReader = new BinaryReader(bitmapStream);
            var fileType = binaryReader.ReadUInt16();
            var fileSize = binaryReader.ReadUInt32();
            binaryReader.ReadUInt16();
            binaryReader.ReadUInt16();
            var pixelDataOffset = binaryReader.ReadUInt32();
            var headerSize = binaryReader.ReadUInt32();
            var imageWidth = binaryReader.ReadInt32();
            var imageHeight = binaryReader.ReadInt32();
            var planes = binaryReader.ReadUInt16();
            var bitsPerPixel = binaryReader.ReadUInt16();
            var compression = binaryReader.ReadUInt32();
            var imageSize = binaryReader.ReadUInt32();
            var pixelsPerMeterHorizontal = binaryReader.ReadInt32();
            var pixelsPerMeterVertical = binaryReader.ReadInt32();
            var totalColors = binaryReader.ReadUInt32();
            var importantColors = binaryReader.ReadUInt32();

            var scanline = ((bitsPerPixel * imageWidth + 31) / 32) * 4;

            Assert.Equal(Constants.BitmapFileType, fileType);
            Assert.Equal(SizeOf.BitmapFileHeader + SizeOf.BitmapInfoHeader + totalColors * 4, pixelDataOffset);
            Assert.Equal(2, imageWidth);
            Assert.Equal(2, imageHeight);
            Assert.Equal(1U, planes);
            Assert.Equal(8U, bitsPerPixel);
            Assert.Equal((uint)SizeOf.BitmapInfoHeader, headerSize);
            Assert.Equal(0U, compression);
            Assert.Equal((uint)(scanline * imageHeight), imageSize);
            Assert.Equal(2835, pixelsPerMeterHorizontal);
            Assert.Equal(2835, pixelsPerMeterVertical);
            Assert.Equal(256U, totalColors);
            Assert.Equal(0U, importantColors);
            Assert.Equal(SizeOf.BitmapFileHeader + SizeOf.BitmapInfoHeader + (totalColors * 4) + (scanline * imageHeight),
                fileSize);

            // assert - palette color 1 is black
            var b = binaryReader.ReadByte();
            var g = binaryReader.ReadByte();
            var r = binaryReader.ReadByte();
            var unused = binaryReader.ReadByte();
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.Equal(0, unused);

            // assert - palette color 2 is red
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            unused = binaryReader.ReadByte();
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.Equal(0, unused);

            // assert - color 3 - 256 is zero
            for (var i = 2; i < 256; i++)
            {
                b = binaryReader.ReadByte();
                g = binaryReader.ReadByte();
                r = binaryReader.ReadByte();
                unused = binaryReader.ReadByte();
                Assert.Equal(0, r);
                Assert.Equal(0, g);
                Assert.Equal(0, b);
                Assert.Equal(0, unused);
            }

            // note: scan lines are stored bottom to top instead of top to bottom, so y goes 1, 0, ...
            
            // assert - pixel 0,1 is black
            var color = binaryReader.ReadByte();
            Assert.Equal(0, color);

            // assert - pixel 1,1 is red
            color = binaryReader.ReadByte();
            Assert.Equal(1, color);

            // assert - padding for 4 byte alignment is zero
            Assert.Equal(0, binaryReader.ReadByte()); 
            Assert.Equal(0, binaryReader.ReadByte());

            // assert - pixel 0,0 is red
            color = binaryReader.ReadByte();
            Assert.Equal(1, color);
            
            // assert - pixel 1,0 is black
            color = binaryReader.ReadByte();
            Assert.Equal(0, color);
            
            // assert - padding for 4 byte alignment is zero
            Assert.Equal(0, binaryReader.ReadByte()); 
            Assert.Equal(0, binaryReader.ReadByte());
        }

        [Fact]
        public async Task WhenWriteImageWith24BitsPerPixelThenBytesMatchBitmapFileStructure()
        {
            // arrange - create new image 
            var image = Create24BppImage(false);
            
            // act - write image as bitmap to stream
            var bitmapStream = new MemoryStream();
            BitmapWriter.Write(bitmapStream, image);

            var bitmapBytes = bitmapStream.ToArray();
            await File.WriteAllBytesAsync("24bpp.bmp", bitmapBytes);

            // assert - read file and info headers
            bitmapStream.Position = 0;
            var binaryReader = new BinaryReader(bitmapStream);
            var fileType = binaryReader.ReadUInt16();
            var fileSize = binaryReader.ReadUInt32();
            binaryReader.ReadUInt16();
            binaryReader.ReadUInt16();
            var pixelDataOffset = binaryReader.ReadUInt32();
            var headerSize = binaryReader.ReadUInt32();
            var imageWidth = binaryReader.ReadInt32();
            var imageHeight = binaryReader.ReadInt32();
            var planes = binaryReader.ReadUInt16();
            var bitsPerPixel = binaryReader.ReadUInt16();
            var compression = binaryReader.ReadUInt32();
            var imageSize = binaryReader.ReadUInt32();
            var pixelsPerMeterHorizontal = binaryReader.ReadInt32();
            var pixelsPerMeterVertical = binaryReader.ReadInt32();
            var totalColors = binaryReader.ReadUInt32();
            var importantColors = binaryReader.ReadUInt32();

            var scanline = ((bitsPerPixel * imageWidth + 31) / 32) * 4;

            Assert.Equal(Constants.BitmapFileType, fileType);
            Assert.Equal(SizeOf.BitmapFileHeader + SizeOf.BitmapInfoHeader + totalColors * 4, pixelDataOffset);
            Assert.Equal(2, imageWidth);
            Assert.Equal(2, imageHeight);
            Assert.Equal(1U, planes);
            Assert.Equal(24U, bitsPerPixel);
            Assert.Equal((uint)SizeOf.BitmapInfoHeader, headerSize);
            Assert.Equal(0U, compression);
            Assert.Equal((uint)(scanline * imageHeight), imageSize);
            Assert.Equal(2835, pixelsPerMeterHorizontal);
            Assert.Equal(2835, pixelsPerMeterVertical);
            Assert.Equal(0U, totalColors);
            Assert.Equal(0U, importantColors);
            Assert.Equal((uint)(SizeOf.BitmapFileHeader + SizeOf.BitmapInfoHeader + (scanline * imageHeight)),
                fileSize);

            // note: scan lines are stored bottom to top instead of top to bottom, so y goes 1, 0, ...
            
            // assert - pixel 0,1 is transparent
            var b = binaryReader.ReadByte();
            var g = binaryReader.ReadByte();
            var r = binaryReader.ReadByte();
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            
            // assert - pixel 1,1 is red
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            
            // assert - padding for 4 byte alignment is zero
            Assert.Equal(0, binaryReader.ReadByte()); 
            Assert.Equal(0, binaryReader.ReadByte());
            
            // assert - pixel 0,0 is red
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);

            // assert - pixel 1,0 is transparent
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            
            // assert - padding for 4 byte alignment is zero
            Assert.Equal(0, binaryReader.ReadByte()); 
            Assert.Equal(0, binaryReader.ReadByte());
        }
        
        [Fact]
        public async Task WhenWriteImageWith32BitsPerPixelThenBytesMatchBitmapFileStructure()
        {
            // arrange - create new image 
            var image = Create32BppImage(true);
            
            // act - write image as bitmap to stream
            var bitmapStream = new MemoryStream();
            BitmapWriter.Write(bitmapStream, image);

            var bitmapBytes = bitmapStream.ToArray();
            await File.WriteAllBytesAsync("32bpp.bmp", bitmapBytes);

            // assert - read file and info headers
            bitmapStream.Position = 0;
            var binaryReader = new BinaryReader(bitmapStream);
            var fileType = binaryReader.ReadUInt16();
            var fileSize = binaryReader.ReadUInt32();
            binaryReader.ReadUInt16();
            binaryReader.ReadUInt16();
            var pixelDataOffset = binaryReader.ReadUInt32();
            var headerSize = binaryReader.ReadUInt32();
            var imageWidth = binaryReader.ReadInt32();
            var imageHeight = binaryReader.ReadInt32();
            var planes = binaryReader.ReadUInt16();
            var bitsPerPixel = binaryReader.ReadUInt16();
            var compression = binaryReader.ReadUInt32();
            var imageSize = binaryReader.ReadUInt32();
            var pixelsPerMeterHorizontal = binaryReader.ReadInt32();
            var pixelsPerMeterVertical = binaryReader.ReadInt32();
            var totalColors = binaryReader.ReadUInt32();
            var importantColors = binaryReader.ReadUInt32();

            var scanline = ((bitsPerPixel * imageWidth + 31) / 32) * 4;

            Assert.Equal(Constants.BitmapFileType, fileType);
            Assert.Equal((uint)(SizeOf.BitmapFileHeader + SizeOf.BitmapV4Header), pixelDataOffset);
            Assert.Equal(2, imageWidth);
            Assert.Equal(2, imageHeight);
            Assert.Equal(1U, planes);
            Assert.Equal(32U, bitsPerPixel);
            Assert.Equal((uint)SizeOf.BitmapV4Header, headerSize);
            Assert.Equal(3U, compression);
            Assert.Equal((uint)(scanline * imageHeight), imageSize);
            Assert.Equal(2835, pixelsPerMeterHorizontal);
            Assert.Equal(2835, pixelsPerMeterVertical);
            Assert.Equal(0U, totalColors);
            Assert.Equal(0U, importantColors);

            Assert.Equal((uint)(SizeOf.BitmapFileHeader + SizeOf.BitmapV4Header + (scanline * imageHeight)),
                fileSize);
            
            // assert - channel bit masks match
            var redChannelBitMask = binaryReader.ReadBytes(4);
            var greenChannelBitMask = binaryReader.ReadBytes(4);
            var blueChannelBitMask = binaryReader.ReadBytes(4);
            var alphaChannelBitMask = binaryReader.ReadBytes(4);
            Assert.Equal(new byte[]{ 0x0, 0x0, 0xff, 0x0 }, redChannelBitMask);
            Assert.Equal(new byte[]{ 0x0, 0xff, 0x0, 0x0 }, greenChannelBitMask);
            Assert.Equal(new byte[]{ 0xff, 0x0, 0x0, 0x0 }, blueChannelBitMask);
            Assert.Equal(new byte[]{ 0x0, 0x0, 0x0, 0xff }, alphaChannelBitMask);

            // assert - lcs windows color space and CIEXYZTRIPLE color space endpoints match
            var lcsWindowsColorSpace = binaryReader.ReadBytes(4);
            var cieXyzTripleColorSpaceEndpoints = binaryReader.ReadBytes(36);
            Assert.Equal(new byte[]{0x20, 0x6E, 0x69, 0x57}, lcsWindowsColorSpace);
            Assert.Equal(new byte[36], cieXyzTripleColorSpaceEndpoints);

            // assert - gamma match
            var redGamma = binaryReader.ReadBytes(4);
            var greenGamma = binaryReader.ReadBytes(4);
            var blueGamma = binaryReader.ReadBytes(4);
            Assert.Equal(new byte[4], redGamma);
            Assert.Equal(new byte[4], greenGamma);
            Assert.Equal(new byte[4], blueGamma);
            
            // note: scan lines are stored bottom to top instead of top to bottom, so y goes 1, 0, ...
            
            // assert - pixel 0,1 is transparent
            var b = binaryReader.ReadByte();
            var g = binaryReader.ReadByte();
            var r = binaryReader.ReadByte();
            var a = binaryReader.ReadByte();
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.Equal(0, a);
            
            // assert - pixel 1,1 is red
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            a = binaryReader.ReadByte();
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.Equal(255, a);
            
            // assert - pixel 0,0 is red
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            a = binaryReader.ReadByte();
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.Equal(255, a);

            // assert - pixel 1,0 is transparent
            b = binaryReader.ReadByte();
            g = binaryReader.ReadByte();
            r = binaryReader.ReadByte();
            a = binaryReader.ReadByte();
            Assert.Equal(0, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
            Assert.Equal(0, a);
        }
    }
}