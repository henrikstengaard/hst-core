namespace Hst.Imaging.Pngcs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Hjg.Pngcs;

    public static class PngReader
    {
        private const int RGBA_BYTES_PER_PIXEL = 4;
        private const int RGBA_BITS_PER_PIXEL = 32;

        public static Image Read(Stream stream)
        {
            var pngReader = new Hjg.Pngcs.PngReader(stream);
            return pngReader.ImgInfo.Indexed ? ReadIndexedImage(pngReader) : ReadImage(pngReader);
        }

        private static Image ReadIndexedImage(Hjg.Pngcs.PngReader pngReader)
        {
            var palette = ReadPalette(pngReader);

            var imageData = new byte[pngReader.ImgInfo.Cols * pngReader.ImgInfo.Rows * pngReader.ImgInfo.BytesPixel];

            var imageDataOffset = 0;
            for (var y = 0; y < pngReader.ImgInfo.Rows; y++)
            {
                var imageLine = pngReader.ReadRowInt(y);

                ReadImageLine(imageData, ref imageDataOffset, imageLine);
            }

            return new Image(pngReader.ImgInfo.Cols, pngReader.ImgInfo.Rows, pngReader.ImgInfo.BitspPixel, palette,
                imageData);
        }

        private static Palette ReadPalette(Hjg.Pngcs.PngReader pngReader)
        {
            var paletteChunk = pngReader.GetMetadata().GetPLTE();
            var transparentChunk = pngReader.GetMetadata().GetTRNS();

            var isTransparent = transparentChunk != null;
            var transparentAlpha = transparentChunk != null ? transparentChunk.GetPalletteAlpha() : Array.Empty<int>();
            var entriesWithAlpha = transparentAlpha.Length;

            var transparentColor = -1;
            for (var i = 0; i < transparentAlpha.Length; i++)
            {
                if (transparentAlpha[i] == 0)
                {
                    transparentColor = i;
                    break;
                }
            }

            var colors = new List<Color>();

            for (var i = 0; i < paletteChunk.GetNentries(); i++)
            {
                var rgb = new int[4];
                paletteChunk.GetEntryRgb(i, rgb);
                colors.Add(new Color(rgb[0], rgb[1], rgb[2],
                    isTransparent && i < entriesWithAlpha ? transparentAlpha[i] : 255));
            }

            var palette = new Palette(colors)
            {
                TransparentColor = isTransparent && transparentColor >= 0 ? transparentColor : -1
            };
            return palette;
        }

        private static Image ReadImage(Hjg.Pngcs.PngReader pngReader)
        {
            var imageData = new byte[pngReader.ImgInfo.Cols * pngReader.ImgInfo.Rows * RGBA_BYTES_PER_PIXEL];

            var transparentChunk = pngReader.GetMetadata().GetTRNS();
            var isTransparent = transparentChunk != null;
            var transparentRgb = isTransparent ? transparentChunk.GetRGB() : null;

            var imageDataOffset = 0;
            for (var y = 0; y < pngReader.ImgInfo.Rows; y++)
            {
                var imageLine = pngReader.ReadRowInt(y);

                ReadImageLine(imageData, ref imageDataOffset, imageLine, transparentRgb);
            }

            var image = new Image(pngReader.ImgInfo.Cols, pngReader.ImgInfo.Rows, RGBA_BITS_PER_PIXEL, new Palette(),
                imageData);

            if (transparentRgb != null)
            {
                image.TransparentColor = new Color(transparentRgb[0], transparentRgb[1], transparentRgb[2], 0);
            }

            return image;
        }

        private static void ReadImageLine(byte[] imageData, ref int imageDataOffset, ImageLine imageLine,
            int[] transparentRgb = null)
        {
            if (!imageLine.SamplesUnpacked)
            {
                imageLine = imageLine.unpackToNewImageLine();
            }

            var scanlineOffset = 0;
            for (var col = 0; col < imageLine.ImgInfo.Cols; col++)
            {
                ReadPixel(imageData, imageDataOffset, imageLine, scanlineOffset, transparentRgb);

                scanlineOffset += imageLine.ImgInfo.Channels;
                imageDataOffset += imageLine.ImgInfo.Channels == 1 ? 1 : RGBA_BYTES_PER_PIXEL;
            }
        }

        private static void ReadPixel(byte[] imageData, int imageDataOffset, ImageLine imageLine, int imageLineOffset,
            int[] transparentRgb = null)
        {
            for (var c = 0; c < imageLine.ImgInfo.Channels; c++)
            {
                imageData[imageDataOffset + c] = (byte)imageLine.Scanline[imageLineOffset + c];
            }

            if (imageLine.ImgInfo.Channels == 1 || imageLine.ImgInfo.Alpha)
            {
                return;
            }

            imageData[imageDataOffset + 3] = (byte)(IsPixelTransparent(imageData, imageDataOffset, transparentRgb)
                ? 0
                : 255);
        }

        private static bool IsPixelTransparent(byte[] imageData, int offset, int[] transparentRgb)
        {
            return transparentRgb != null && transparentRgb.Length >= 3 && imageData[offset] == transparentRgb[0] &&
                   imageData[offset + 1] == transparentRgb[1] && imageData[offset + 2] == transparentRgb[2];
        }
    }
}