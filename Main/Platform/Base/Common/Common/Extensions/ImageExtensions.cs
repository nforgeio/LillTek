//-----------------------------------------------------------------------------
// FILE:        ImageExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Image extension methods.

#if !MOBILE_DEVICE

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LillTek.Common
{
    /// <summary>
    /// Image extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Crops a rectangular area from an image and returns it as a new image.
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="cropRect">The cropping rectangle.</param>
        /// <returns>The cropped image.</returns>
        public static Image Crop(this Image inputImage, Rectangle cropRect)
        {
            var inputBitmap = new Bitmap(inputImage);
            var cropBitmap  = inputBitmap.Clone(cropRect, inputBitmap.PixelFormat);

            return (Image)cropBitmap;
        }

        /// <summary>
        /// Resizes an image while maintaining its aspect ratio.
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="size">The requested image dimensions.</param>
        /// <returns>The resized image.</returns>
        public static Image Resize(this Image inputImage, Size size)
        {
            return inputImage.Resize(size, inputImage.PixelFormat);
        }

        /// <summary>
        /// Resizes an image while maintaining its aspect ratio and
        /// also changing its pixel format.
        /// </summary>
        /// <param name="inputImage">The input image.</param>
        /// <param name="size">The requested image dimensions.</param>
        /// <param name="pixelFormat">The new pixel format.</param>
        /// <returns>The resized image.</returns>
        public static Image Resize(this Image inputImage, Size size, PixelFormat pixelFormat)
        {
            int     inputWidth  = inputImage.Width;
            int     inputHeight = inputImage.Height;
            float   nPercent    = 0;
            float   nPercentW   = 0;
            float   nPercentH   = 0;
            int     outputWidth;
            int     outputHeight;
            Bitmap  outputBitmap;

            nPercentW = ((float)size.Width / (float)inputWidth);
            nPercentH = ((float)size.Height / (float)inputHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            outputWidth = (int)(inputWidth * nPercent);
            outputHeight = (int)(inputHeight * nPercent);
            outputBitmap = new Bitmap(outputWidth, outputHeight, pixelFormat);

            using (var g = Graphics.FromImage((Image)outputBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(inputImage, 0, 0, outputWidth, outputHeight);
            }

            return (Image)outputBitmap;
        }
    }
}

#endif // !MOBILE_DEVICE