using UnityEngine;

namespace AICore;

public static class LanczosResize
{
    private const int A = 3;
    private const double EPSILON = .0000125f;
    private static readonly Func<double, double> Clean = t => Math.Abs(t) < EPSILON ? (double)0.0f : (double)t;
    private static readonly Func<double, double> Sinc = x =>
    {
        x *= Math.PI;
        return x is < (double)0.01f and > (double)-0.01f ? 1.0f + (x * x * ((-1.0f / 6.0f) + (x * x * 1.0f / 120.0f))) : Math.Sin(x) / x;
    };
    private static readonly Func<double, int, double> LanczosFilter = (t, a) => Math.Abs(t) < a ? Clean(Sinc(Math.Abs(t)) * Sinc(Math.Abs(t) / a)) : 0.0;

    public static Texture2D? DownsampleImage(Texture2D image, double divisor)
    {
#if DEBUG
        LogTool.Debug($"Downsampling image with original dimensions: {image.width}x{image.height}, divisor: {divisor}");
#endif

        if (image == null) return null;
        if (image.width <= 0) return null;
        if (image.height <= 0) return null;

        Color[] oldPixels = image.GetPixels();
        uint oldColCnt = (uint)image.width;
        uint oldRowCnt = (uint)image.height;
        double divColCnt = oldColCnt / divisor;
        double divRowCnt = oldRowCnt / divisor;

        if (divColCnt > uint.MaxValue) return null;
        if (divRowCnt > uint.MaxValue) return null;

        uint newColCnt = (uint)Math.Floor(divColCnt);
        uint newRowCnt = (uint)Math.Floor(divRowCnt);

        if (newRowCnt <= A + 1) return null;
        if (newColCnt <= A + 1) return null;

#if DEBUG
        LogTool.Debug($"Calculated new dimensions: {newColCnt}x{newRowCnt}");
#endif

        Texture2D newImage = new((int)newColCnt, (int)newRowCnt);
        Color[] newPixels = new Color[(int)newColCnt * (int)newRowCnt];

        _ = Parallel.For(A + 1, newRowCnt - A - 1, row =>
        {
            for (uint col = A + 1; col < newColCnt - A - 1; col++)
            {
                //Find row and col in terms of image coord
                double y = row * divisor;
                double x = col * divisor;
                double x1 = Math.Floor(x);
                double y1 = Math.Floor(y);

                //Note that x2-x1 = 1 & y2 - y1 = 1
                Color newPixel = new(0, 0, 0, 1);

                for (uint i = (uint)x1 - A + 1; i <= (uint)x1 + A; i++)
                    for (uint j = (uint)y1 - A + 1; j < (uint)y1 + A; j++)
                    {
                        if (i < 0 || i >= oldColCnt) continue;
                        if (j < 0 || j >= oldRowCnt) continue;

                        double lanczosFactorX = LanczosFilter(x - i, A);
                        double lanczosFactorY = LanczosFilter(y - j, A);
                        double lanczosFactor = lanczosFactorX * lanczosFactorY;

                        uint oldPixelIndex = (j * oldColCnt) + i;
                        newPixel.r += oldPixels[oldPixelIndex].r * (float)lanczosFactor;
                        newPixel.g += oldPixels[oldPixelIndex].g * (float)lanczosFactor;
                        newPixel.b += oldPixels[oldPixelIndex].b * (float)lanczosFactor;
                    }

                newPixels[(row * newColCnt) + col] = newPixel;
            }
        });

        newImage.SetPixels(newPixels);
        newImage.Apply();
        return newImage;
    }

    public static Texture2D? CropImage(Texture2D image, int cropWidth, int cropHeight, string alignment, int offset = 0)
    {
        if (image == null) return null;
        if (alignment == null) return null;
        if (image.width <= 0 || image.height <= 0) return null;
        if (cropWidth <= 0 || cropHeight <= 0) return null;
        if (cropWidth > image.width || cropHeight > image.height) return null;

        int startX;
        int startY;

        switch (alignment.ToUpperInvariant())
        {
            case "TOP":
                startX = (image.width - cropWidth) / 2; // Center horizontally
                startY = image.height - cropHeight - offset; // Start from the top
                break;
            case "LEFT":
                startX = offset; // Start from the left edge
                startY = (image.height - cropHeight) / 2; // Center vertically
                break;
            case "RIGHT":
                startX = image.width - cropWidth - offset; // Start from the right edge
                startY = (image.height - cropHeight) / 2; // Center vertically
                break;
            case "BOTTOM":
                startX = (image.width - cropWidth) / 2; // Center horizontally
                startY = offset; // Start from the bottom
                break;
            default:
                return null; // Invalid alignment
        }

        // Ensure the coordinates are within bounds
        startX = Mathf.Clamp(startX, 0, image.width - cropWidth);
        startY = Mathf.Clamp(startY, 0, image.height - cropHeight);

        // Get the cropped pixels
        Color[] croppedPixels = image.GetPixels(startX, startY, cropWidth, cropHeight);

        // Create the new cropped texture
        Texture2D croppedImage = new(cropWidth, cropHeight);
        croppedImage.SetPixels(croppedPixels);
        croppedImage.Apply();

        return croppedImage;
    }
}