namespace Hst.Imaging.Bitmaps
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public static class BitmapReader
    {
        public static Image Read(Stream stream)
        {
            var binaryReader = new BinaryReader(stream);
            
            var fileType = binaryReader.ReadUInt16();
            var fileSize = binaryReader.ReadUInt32();
            binaryReader.ReadUInt16(); // reserved 1
            binaryReader.ReadUInt16(); // reserved 2
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

            uint redChannelBitMask = 0;
            uint greenChannelBitMask = 0;
            uint blueChannelBitMask = 0;
            uint alphaChannelBitMask = 0;
            
            if (bitsPerPixel == 32)
            {
                // write bitmap v4 header
                redChannelBitMask = binaryReader.ReadUInt32(); // 00FF0000 in big-endian, Red channel bit mask (valid because BI_BITFIELDS is specified)
                greenChannelBitMask = binaryReader.ReadUInt32(); // 0000FF00 in big-endian, Green channel bit mask (valid because BI_BITFIELDS is specified)
                blueChannelBitMask = binaryReader.ReadUInt32(); // 000000FF in big-endian, Blue channel bit mask (valid because BI_BITFIELDS is specified)
                alphaChannelBitMask = binaryReader.ReadUInt32(); // FF000000  in big-endian, Alpha channel bit mask (valid because BI_BITFIELDS is specified)
                
                var lcsWindowsColorSpace = binaryReader.ReadBytes(4); // little-endian "Win ", LCS_WINDOWS_COLOR_SPACE
                binaryReader.ReadBytes(36); // CIEXYZTRIPLE Color Space endpoints, Unused for LCS "Win " or "sRGB"

                binaryReader.ReadBytes(4); // 0 Red Gamma, Unused for LCS "Win " or "sRGB"
                binaryReader.ReadBytes(4); // 0 Green Gamma, Unused for LCS "Win " or "sRGB"
                binaryReader.ReadBytes(4); // 0 Blue Gamma, Unused for LCS "Win " or "sRGB"
            }

            // read palette
            var colors = new List<Color>();
            if (bitsPerPixel <= 8)
            {
                for (var i = 0; i < totalColors; i++)
                {
                    var b = binaryReader.ReadByte();
                    var g = binaryReader.ReadByte();
                    var r = binaryReader.ReadByte();
                    binaryReader.ReadByte(); // reserved

                    colors.Add(new Color(r, g, b, 255));
                }
            }

            var bytesPerPixel = bitsPerPixel <= 8 ? 1 : bitsPerPixel / 8;
            var data = new byte[imageWidth * imageHeight * bytesPerPixel];

            var imageScanline = imageWidth * bytesPerPixel;
            var bitmapScanline = BitmapHelper.CalculateScanline(bitsPerPixel, imageWidth);
            var scanlineData = new byte[bitmapScanline];

            for (var y = 0; y < imageHeight; y++)
            {
                if (binaryReader.Read(scanlineData, 0, bitmapScanline) != bitmapScanline)
                {
                    throw new IOException($"Unable to scanline of {bitmapScanline} bytes");
                }

                var imagePixelOffset = imageScanline * (imageHeight - y - 1);
                var bitmapPixelOffset = 0;
                for (var x = 0; x < imageWidth; x++)
                {
                    if (bitsPerPixel < 8)
                    {
                        throw new NotImplementedException();
                    }
                    
                    if (bitsPerPixel == 8)
                    {
                        data[imagePixelOffset] = scanlineData[bitmapPixelOffset];
                    }
                    
                    if (bitsPerPixel >= 24)
                    {
                        data[imagePixelOffset] = scanlineData[bitmapPixelOffset + 2]; // red
                        data[imagePixelOffset + 1] = scanlineData[bitmapPixelOffset + 1]; // green
                        data[imagePixelOffset + 2] = scanlineData[bitmapPixelOffset]; // blue
                    }
                    
                    if (bitsPerPixel == 32)
                    {
                        data[imagePixelOffset + 3] = scanlineData[bitmapPixelOffset + 3]; // alpha
                    }

                    imagePixelOffset += bytesPerPixel;
                    bitmapPixelOffset += bytesPerPixel;
                }
            }
            
            return new Image(imageWidth, imageHeight, bitsPerPixel, false, Color.Transparent, new Palette(colors), data);
        }
    }
}