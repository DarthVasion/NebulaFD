﻿using Nebula.Core.FileReaders;
using Nebula.Core.Memory;
using Nebula.Core.Utilities;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Nebula.Core.Data.Chunks.BankChunks.Images
{
    public class Image : Chunk
    {
        /*
         * ===GRAPHIC MODES===
         *
         * 0 - android, transparency, 4 bytes per pixel, 8 bits per channel
         * 1 - android, transparency, 2 bytes per pixel, 4 bits per channel
         * 2 - android, transparency, 2 bytes per pixel, 5 bits per channel
         * 3 - android, no transparency, 3 bytes per pixel, 8 bits per channel
         * 4 - android, no transparency, 2 bytes per pixel, 5 bits per channel
         * 4 - normal, 24 bits per color, 8 bit-deep alpha mask at the end
         * 4 - mmf1.5, i don't like that
         * 5 - android, no transparency, JPEG
         * 6 - normal, 15 bits per pixel, but it's actually 16 but retarded
         * 7 - normal, 16 bits per pixel
         * 8 - 2.5+, 32 bits per pixel, 8 bits per color
         * 9 - Flash, basically 8 but backwards
         */

        public uint Handle;
        public int Checksum;
        public int References;
        public short Width;
        public short Height;
        public byte GraphicMode;
        public short HotspotX;
        public short HotspotY;
        public short ActionPointX;
        public short ActionPointY;
        public Color TransparentColor = Color.Black;

        public byte[] ImageData = new byte[0];
        private Bitmap? BitmapCache = null;
        public bool IsMasked;

        public BitDict Flags = new BitDict(new string[]
        {
            "RLE",
            "RLEW",
            "RLET",
            "LZX",
            "Alpha",
            "ACE",
            "Mac",
            "RGBA"
        });

        public Image()
        {
            ChunkName = "Image";
        }

        public static Image NewImage()
        {
            if (NebulaCore.MFA)
                return new ImageMFA();
            else if (NebulaCore.CurrentReader is OpenFileReader)
                return new ImageOpen();
            else if (NebulaCore.Android || NebulaCore.iOS)
                return new ImageMobile();
            else if (NebulaCore.Flash)
                return new ImageFlash();
            else if (NebulaCore.Fusion == 1.5f)
                return new Image15();
            return new Image25();
        }

        public byte[] GetData()
        {
            if (GraphicMode == 3 && NebulaCore.Fusion == 0)
                Width = (short)(Math.Ceiling(Width / 2.0f) * 2);

            byte[] colorArray = null;
            switch (GraphicMode)
            {
                case 0:
                    colorArray = ImageTranslatorCPU.AndroidMode0ToRGBA(this);
                    break;
                case 1:
                    colorArray = ImageTranslatorCPU.AndroidMode1ToRGBA(this);
                    break;
                case 2:
                    colorArray = ImageTranslatorCPU.AndroidMode2ToRGBA(this);
                    break;
                case 3:
                    if (NebulaCore.Android || NebulaCore.iOS)
                        colorArray = ImageTranslatorCPU.AndroidMode3ToRGBA(this);
                    else
                        colorArray = ImageTranslatorCPU.ColorPaletteToRGBA(this, NebulaCore.PackageData.Frames.First().FramePalette.Palette);
                    break;
                case 4:
                    if ((NebulaCore.Android || NebulaCore.iOS) && !IsMasked)
                        colorArray = ImageTranslatorCPU.AndroidMode4ToRGBA(this);
                    else// if (NebulaCore.CurrentReader is not ChowdrenFileReader)
                    {
                        if (!Parameters.GPUAcceleration)
                            colorArray = ImageTranslatorCPU.Normal24BitMaskedToRGBA(this);
                        else
                            colorArray = ImageTranslatorGPU.Normal24BitMaskedToRGBA(this);
                    }
                    //else
                    //    colorArray = ImageData;
                    break;
                case 5:
                    colorArray = ImageTranslatorCPU.AndroidMode5ToRGBA(this);
                    break;
                case 6:
                    colorArray = ImageTranslatorCPU.Normal15BitToRGBA(this);
                    break;
                case 7:
                    if (!Parameters.GPUAcceleration)
                        colorArray = ImageTranslatorCPU.Normal16BitToRGBA(this);
                    else
                        colorArray = ImageTranslatorGPU.Normal16BitToRGBA(this);
                    break;
                case 8:
                    if (!Parameters.GPUAcceleration)
                        colorArray = ImageTranslatorCPU.TwoFivePlusToRGBA(this);
                    else
                        colorArray = ImageTranslatorGPU.TwoFivePlusToRGBA(this);
                    break;
                case 9:
                    colorArray = ImageTranslatorCPU.FlashToRGBA(this);
                    break;
            }
            return colorArray;
        }

        public Bitmap GetBitmap()
        {
            lock (ImageData)
            {
                if (BitmapCache == null)
                {
                    if (ImageData.Length == 0)
                        return new Bitmap(1, 1);

                    BitmapCache = new Bitmap(Width, Height);
                    var bmpData = BitmapCache.LockBits(new Rectangle(0, 0, Width, Height),
                                                        ImageLockMode.WriteOnly,
                                                        PixelFormat.Format32bppArgb);

                    byte[] colorArray = GetData();

                    if (!IsMasked && colorArray != null)
                    {
                        ImageData = colorArray;
                        if (!Parameters.GPUAcceleration)
                            ImageData = ImageTranslatorCPU.RGBAToRGBMasked(this);
                        else
                            ImageData = ImageTranslatorGPU.RGBAToRGBMasked(this);

                        GraphicMode = 4;
                        IsMasked = true;
                    }

                    Marshal.Copy(colorArray, 0, bmpData.Scan0, colorArray.Length);
                    BitmapCache.UnlockBits(bmpData);
                }
            }

            return BitmapCache;
        }

        public void DisposeBmp()
        {
            if (BitmapCache != null)
            {
                BitmapCache.Dispose();
                BitmapCache = null;
            }
        }

        public void FromBitmap(Bitmap bmp)
        {
            Width = (short)bmp.Width;
            Height = (short)bmp.Height;
            GraphicMode = 4;
            IsMasked = true;

            var bitmapData = bmp.LockBits(new Rectangle(0, 0, Width, Height),
                                          ImageLockMode.ReadOnly,
                                          PixelFormat.Format24bppRgb);
            var copyPad = ImageHelper.GetPadding(Width, 4);
            var length = bitmapData.Height * bitmapData.Stride + copyPad * 4;

            var bytes = new byte[length];
            var stride = bitmapData.Stride;
            Marshal.Copy(bitmapData.Scan0, bytes, 0, length);
            bmp.UnlockBits(bitmapData);

            ImageData = new byte[Width * Height * 6];
            var position = 0;
            var pad = ImageHelper.GetPadding(Width, 3);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var newPos = y * stride + x * 3;
                    ImageData[position] = bytes[newPos];
                    ImageData[position + 1] = bytes[newPos + 1];
                    ImageData[position + 2] = bytes[newPos + 2];
                    position += 3;
                }

                position += 3 * pad;
            }

            var bitmapDataAlpha = bmp.LockBits(new Rectangle(0, 0, Width, Height),
                                                ImageLockMode.ReadOnly,
                                                PixelFormat.Format32bppArgb);
            var copyPadAlpha = ImageHelper.GetPadding(Width, 1);
            var lengthAlpha = bitmapDataAlpha.Height * bitmapDataAlpha.Stride + copyPadAlpha * 4;

            var bytesAlpha = new byte[lengthAlpha];
            var strideAlpha = bitmapDataAlpha.Stride;
            Marshal.Copy(bitmapDataAlpha.Scan0, bytesAlpha, 0, lengthAlpha);
            bmp.UnlockBits(bitmapDataAlpha);

            var aPad = ImageHelper.GetPadding(Width, 1, 4);
            var alphaPos = position;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    ImageData[alphaPos] = bytesAlpha[y * strideAlpha + x * 4 + 3];
                    alphaPos += 1;
                }

                alphaPos += aPad;
            }
        }

        public override void ReadCCN(ByteReader reader, params object[] extraInfo)
        {

        }

        public override void ReadMFA(ByteReader reader, params object[] extraInfo)
        {

        }

        public override void WriteCCN(ByteWriter writer, params object[] extraInfo)
        {

        }

        public override void WriteMFA(ByteWriter writer, params object[] extraInfo)
        {
            PrepareForMfa();

            Flags["LZX"] = true;
            byte[] compressedImg = Decompressor.CompressBlock(ImageData);

            // For multi-threading, works single-threaded too ofc
            ByteWriter tempWriter = new ByteWriter(new MemoryStream());
            tempWriter.WriteUInt(Handle);
            tempWriter.WriteInt(Checksum);
            tempWriter.WriteInt(References);
            tempWriter.WriteInt(compressedImg.Length + 4);
            tempWriter.WriteShort(Width);
            tempWriter.WriteShort(Height);
            tempWriter.WriteByte(GraphicMode);
            tempWriter.WriteByte((byte)Flags.Value);
            tempWriter.WriteShort(0);
            tempWriter.WriteShort(HotspotX);
            tempWriter.WriteShort(HotspotY);
            tempWriter.WriteShort(ActionPointX);
            tempWriter.WriteShort(ActionPointY);
            tempWriter.WriteColor(TransparentColor);
            tempWriter.WriteInt(ImageData.Length);
            tempWriter.WriteBytes(compressedImg);
            lock (writer)
                writer.WriteWriter(tempWriter);
        }

        public void PrepareForMfa()
        {
            if (IsMasked)
            {
                GraphicMode = 4;
                Flags["RLE"] = Flags["RLEW"] = Flags["RLET"] = false;
                return;
            }

            switch (GraphicMode)
            {
                case 0:
                    ImageData = ImageTranslatorCPU.AndroidMode0ToRGBA(this);
                    break;
                case 1:
                    ImageData = ImageTranslatorCPU.AndroidMode1ToRGBA(this);
                    break;
                case 2:
                    ImageData = ImageTranslatorCPU.AndroidMode2ToRGBA(this);
                    break;
                case 3:
                    if (NebulaCore.Android || NebulaCore.iOS)
                        ImageData = ImageTranslatorCPU.AndroidMode3ToRGBA(this);
                    else
                        ImageData = ImageTranslatorCPU.ColorPaletteToRGBA(this, NebulaCore.PackageData.Frames.First().FramePalette.Palette);
                    break;
                case 4:
                    if (NebulaCore.Android || NebulaCore.iOS)
                        ImageData = ImageTranslatorCPU.AndroidMode4ToRGBA(this);
                    else if (NebulaCore.Fusion > 2.5f)
                    {
                        if (!Parameters.GPUAcceleration)
                            ImageData = ImageTranslatorCPU.Normal24BitMaskedToRGBA(this);
                        else
                            ImageData = ImageTranslatorGPU.Normal24BitMaskedToRGBA(this);
                    }
                    else
                        IsMasked = true;
                    break;
                case 5:
                    ImageData = ImageTranslatorCPU.AndroidMode5ToRGBA(this);
                    break;
                case 6:
                    ImageData = ImageTranslatorCPU.Normal15BitToRGBA(this);
                    break;
                case 7:
                    if (!Parameters.GPUAcceleration)
                        ImageData = ImageTranslatorCPU.Normal16BitToRGBA(this);
                    else
                        ImageData = ImageTranslatorGPU.Normal16BitToRGBA(this);
                    break;
                case 8:
                    if (!Parameters.GPUAcceleration)
                        ImageData = ImageTranslatorCPU.TwoFivePlusToRGBA(this);
                    else
                        ImageData = ImageTranslatorGPU.TwoFivePlusToRGBA(this);
                    break;
                case 9:
                    ImageData = ImageTranslatorCPU.FlashToRGBA(this);
                    break;
            }

            GraphicMode = 4;
            Flags["RLE"] = Flags["RLEW"] = Flags["RLET"] = false;

            if (IsMasked)
                return;

            if (!Parameters.GPUAcceleration)
                ImageData = ImageTranslatorCPU.RGBAToRGBMasked(this);
            else
                ImageData = ImageTranslatorGPU.RGBAToRGBMasked(this);
            IsMasked = true;
        }

        public Image Clone()
        {
            Image newImg = new Image();
            newImg.Handle = Handle;
            newImg.Checksum = Checksum;
            newImg.References = References;
            newImg.Width = Width;
            newImg.Height = Height;
            newImg.GraphicMode = GraphicMode;
            newImg.HotspotX = HotspotX;
            newImg.HotspotY = HotspotY;
            newImg.ActionPointX = ActionPointX;
            newImg.ActionPointY = ActionPointY;
            newImg.TransparentColor = TransparentColor;

            newImg.ImageData = ImageData;
            newImg.BitmapCache = BitmapCache;

            newImg.Flags.Value = Flags.Value;
            return newImg;
        }
    }
}
