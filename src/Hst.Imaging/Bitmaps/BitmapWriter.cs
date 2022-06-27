namespace Hst.Imaging.Bitmaps
{
    using System;
    using System.IO;

    public static class BitmapWriter
    {
        public static void Write(Stream stream, Image image)
        {
            var binaryWriter = new BinaryWriter(stream);

            var totalColors = Convert.ToInt32(image.BitsPerPixel <= 8 ? Math.Pow(2, image.BitsPerPixel) : 0);
            var pixelDataOffset = SizeOf.BitmapFileHeader + (image.BitsPerPixel == 32 ? SizeOf.BitmapV4Header : SizeOf.BitmapInfoHeader) +
                                  (image.BitsPerPixel <= 8 ? totalColors * 4 : 0);
            var scanline = ((image.BitsPerPixel * image.Width + 31) / 32) * 4;
            var imageSize = scanline * image.Height;
            var fileSize = (uint)(pixelDataOffset + imageSize);

            // write bitmap file header
            binaryWriter.Write(Constants.BitmapFileType);
            binaryWriter.Write(fileSize);
            binaryWriter.Write((ushort)0); // reserved 1
            binaryWriter.Write((ushort)0); // reserved 2
            binaryWriter.Write(pixelDataOffset);

            // write bitmap info header
            binaryWriter.Write((uint)(image.BitsPerPixel == 32 ? SizeOf.BitmapV4Header : SizeOf.BitmapInfoHeader));
            binaryWriter.Write((int)image.Width); // width
            binaryWriter.Write((int)image.Height); // height
            binaryWriter.Write((ushort)1); // planes
            binaryWriter.Write((ushort)image.BitsPerPixel); // bits per pixel
            binaryWriter.Write((uint)(image.BitsPerPixel == 32 ? 3 : 0)); // compression (3 = BI_BITFIELDS, no pixel array compression used)
            binaryWriter.Write((uint)imageSize);
            binaryWriter.Write((int)2835); // pixels per meter horizontal (print resolution of the image, 72 DPI × 39.3701 inches per metre yields 2834.6472)
            binaryWriter.Write((int)2835); // pixels per meter vertical (print resolution of the image, 72 DPI × 39.3701 inches per metre yields 2834.6472)
            binaryWriter.Write((uint)(image.BitsPerPixel <= 8 ? totalColors : 0)); // total colors
            binaryWriter.Write((uint)0); // important colors  
            
            if (image.BitsPerPixel == 32)
            {
                // write bitmap v4 header
                binaryWriter.Write(new byte[]{0x0, 0x0, 0xff, 0x0}); // 00FF0000 in big-endian, Red channel bit mask (valid because BI_BITFIELDS is specified)
                binaryWriter.Write(new byte[]{0x0, 0xff, 0x0, 0x0}); // 0000FF00 in big-endian, Green channel bit mask (valid because BI_BITFIELDS is specified)
                binaryWriter.Write(new byte[]{0xff, 0x0, 0x0, 0x0}); // 000000FF in big-endian, Blue channel bit mask (valid because BI_BITFIELDS is specified)
                binaryWriter.Write(new byte[]{0x0, 0x0, 0x0, 0xff}); // FF000000  in big-endian, Alpha channel bit mask (valid because BI_BITFIELDS is specified)
                
                binaryWriter.Write(new byte[]{0x20, 0x6E, 0x69, 0x57}); // little-endian "Win ", LCS_WINDOWS_COLOR_SPACE
                binaryWriter.Write(new byte[36]); // CIEXYZTRIPLE Color Space endpoints, Unused for LCS "Win " or "sRGB"

                binaryWriter.Write(new byte[4]); // 0 Red Gamma, Unused for LCS "Win " or "sRGB"
                binaryWriter.Write(new byte[4]); // 0 Green Gamma, Unused for LCS "Win " or "sRGB"
                binaryWriter.Write(new byte[4]); // 0 Blue Gamma, Unused for LCS "Win " or "sRGB"
            }

            if (image.BitsPerPixel <= 8)
            {
                // write palette RGB color backwards BGR
                foreach (var color in image.Palette)
                {
                    binaryWriter.Write((byte)color.B); // blue
                    binaryWriter.Write((byte)color.G); // green
                    binaryWriter.Write((byte)color.R); // red
                    binaryWriter.Write((byte)0); // reserved
                }

                // write unused palette colors as zeros
                for (var i = image.Palette.Count; i < totalColors; i++)
                {
                    binaryWriter.Write(new byte[4]);
                }
            }
            
            var bytesPerPixel = image.BitsPerPixel <= 8 ? 1 : image.BitsPerPixel / 8;
            
            for (var y = 0; y < image.Height; y++)
            {
                var bitmapScanLineData = new byte[scanline];
                var imagePixelOffset = (image.Height - y - 1) * image.Scanline;
                var bitmapPixelOffset = 0;
                
                for (var x = 0; x < image.Width; x++)
                {
                    if (image.BitsPerPixel < 8)
                    {
                        throw new NotImplementedException();
                    }

                    if (image.BitsPerPixel == 8)
                    {
                        bitmapScanLineData[bitmapPixelOffset] = image.Data[imagePixelOffset];
                    }

                    if (image.BitsPerPixel >= 24)
                    {
                        bitmapScanLineData[bitmapPixelOffset + 2] = image.Data[imagePixelOffset + 0]; // red
                        bitmapScanLineData[bitmapPixelOffset + 1] = image.Data[imagePixelOffset + 1]; // green
                        bitmapScanLineData[bitmapPixelOffset] = image.Data[imagePixelOffset + 2]; // blue
                    }
                    
                    if (image.BitsPerPixel == 32)
                    {
                        bitmapScanLineData[bitmapPixelOffset + 3] = image.Data[imagePixelOffset + 3]; // alpha
                    }

                    imagePixelOffset += bytesPerPixel;
                    bitmapPixelOffset += bytesPerPixel;
                }
                
                binaryWriter.Write(bitmapScanLineData);
            }
        }
    }
}