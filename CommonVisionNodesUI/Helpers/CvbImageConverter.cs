using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Stemmer.Cvb;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Converts CVB <see cref="Stemmer.Cvb.Image"/> instances to WinUI <see cref="WriteableBitmap"/> for display.
/// </summary>
public static class CvbImageConverter
{
    /// <summary>
    /// Converts a CVB image to a <see cref="WriteableBitmap"/>.
    /// Supports mono (1-plane) and RGB (3+ plane) images.
    /// </summary>
    /// <param name="cvbImage">The CVB image to convert.</param>
    /// <returns>A <see cref="WriteableBitmap"/> containing the pixel data.</returns>
    public static WriteableBitmap ConvertToWriteableBitmap(Stemmer.Cvb.Image cvbImage)
    {
        int width = cvbImage.Width;
        int height = cvbImage.Height;
        int planeCount = cvbImage.Planes.Count;

        var pixels = new byte[width * height * 4];

        if (planeCount == 1)
            ConvertMono(cvbImage, pixels, width, height);
        else if (planeCount >= 3)
            ConvertRgb(cvbImage, pixels, width, height);

        var bitmap = new WriteableBitmap(width, height);
        using (var stream = bitmap.PixelBuffer.AsStream())
            stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();

        return bitmap;
    }

    /// <summary>
    /// Converts a single-plane (mono) image to BGRA pixel data using linear access.
    /// </summary>
    private static void ConvertMono(Stemmer.Cvb.Image image, byte[] pixels, int width, int height)
    {
        var plane = image.Planes[0];

        if (plane.TryGetLinearAccess(out var access))
        {
            var dataType = plane.DataType;
            for (int y = 0; y < height; y++)
            {
                nint rowBase = access.BasePtr + y * access.YInc;
                for (int x = 0; x < width; x++)
                {
                    byte value = ReadScaled(rowBase + x * access.XInc, dataType);
                    int idx = (y * width + x) * 4;
                    pixels[idx] = value;
                    pixels[idx + 1] = value;
                    pixels[idx + 2] = value;
                    pixels[idx + 3] = 255;
                }
            }
        }
        else
        {
            ConvertMonoFallback(plane, pixels, width, height);
        }
    }

    /// <summary>
    /// Fallback mono conversion using per-pixel access when linear access is unavailable.
    /// </summary>
    private static void ConvertMonoFallback(ImagePlane plane, byte[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = (byte)Math.Clamp(plane.GetPixel(x, y), 0, 255);
                int idx = (y * width + x) * 4;
                pixels[idx] = value;
                pixels[idx + 1] = value;
                pixels[idx + 2] = value;
                pixels[idx + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Converts a 3+ plane (RGB) image to BGRA pixel data using linear access.
    /// </summary>
    private static void ConvertRgb(Stemmer.Cvb.Image image, byte[] pixels, int width, int height)
    {
        var plane0 = image.Planes[0];
        var plane1 = image.Planes[1];
        var plane2 = image.Planes[2];

        if (plane0.TryGetLinearAccess(out var access0) &&
            plane1.TryGetLinearAccess(out var access1) &&
            plane2.TryGetLinearAccess(out var access2))
        {
            var dt0 = plane0.DataType;
            var dt1 = plane1.DataType;
            var dt2 = plane2.DataType;

            for (int y = 0; y < height; y++)
            {
                nint row0 = access0.BasePtr + y * access0.YInc;
                nint row1 = access1.BasePtr + y * access1.YInc;
                nint row2 = access2.BasePtr + y * access2.YInc;

                for (int x = 0; x < width; x++)
                {
                    byte r = ReadScaled(row0 + x * access0.XInc, dt0);
                    byte g = ReadScaled(row1 + x * access1.XInc, dt1);
                    byte b = ReadScaled(row2 + x * access2.XInc, dt2);

                    int idx = (y * width + x) * 4;
                    pixels[idx] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = 255;
                }
            }
        }
        else
        {
            ConvertRgbFallback(image, pixels, width, height);
        }
    }

    /// <summary>
    /// Fallback RGB conversion using per-pixel access when linear access is unavailable.
    /// </summary>
    private static void ConvertRgbFallback(Stemmer.Cvb.Image image, byte[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var px = image.GetPixel(x, y);
                byte r = (byte)Math.Clamp(px[0], 0, 255);
                byte g = (byte)Math.Clamp(px[1], 0, 255);
                byte b = (byte)Math.Clamp(px[2], 0, 255);

                int idx = (y * width + x) * 4;
                pixels[idx] = b;
                pixels[idx + 1] = g;
                pixels[idx + 2] = r;
                pixels[idx + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Reads a pixel value from memory and scales it to a byte (0–255),
    /// handling 8-bit, 16-bit, and float data types.
    /// </summary>
    /// <param name="addr">Pointer to the pixel data.</param>
    /// <param name="dataType">The pixel data type.</param>
    /// <returns>Scaled byte value.</returns>
    private static byte ReadScaled(nint addr, DataType dataType)
    {
        if (dataType.BytesPerPixel == 1 && dataType.IsUnsignedInteger)
            return Marshal.ReadByte(addr);

        if (dataType.BytesPerPixel == 2 && dataType.IsUnsignedInteger)
        {
            ushort raw = (ushort)Marshal.ReadInt16(addr);
            return (byte)(raw >> 8);
        }

        if (dataType.BytesPerPixel == 2 && dataType.IsSignedInteger)
        {
            short raw = Marshal.ReadInt16(addr);
            return (byte)Math.Clamp((raw + 32768) >> 8, 0, 255);
        }

        if (dataType.IsFloat && dataType.BytesPerPixel == 4)
        {
            int bits = Marshal.ReadInt32(addr);
            float value = BitConverter.Int32BitsToSingle(bits);
            return (byte)Math.Clamp(value * 255f, 0f, 255f);
        }

        return Marshal.ReadByte(addr);
    }
}
