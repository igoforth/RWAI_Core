using UnityEngine;

namespace AICore;

#pragma warning disable CA1814

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

        if ((newRowCnt - A - 1) > newRowCnt) return null;
        if ((newColCnt - A - 1) > newRowCnt) return null;

        Texture2D newImage = new((int)newColCnt, (int)newRowCnt);
        Color[] newPixels = new Color[(int)newColCnt * (int)newRowCnt];

        for (int row = A + 1; row < newRowCnt - A - 1; row++)
            for (int col = A + 1; col < newColCnt - A - 1; col++)
            {
                //Find row and col in terms of image coord
                double y = row * divisor;
                double x = col * divisor;
                double x1 = Math.Floor(x);
                double y1 = Math.Floor(y);

                //Note that x2-x1 = 1 & y2 - y1 = 1
                Color newPixel = new(0, 0, 0, 1);

                for (int i = (int)x1 - A + 1; i <= (int)x1 + A; i++)
                    for (int j = (int)y1 - A + 1; j < (int)y1 + A; j++)
                    {
                        if (i < 0 || i >= oldColCnt) continue;
                        if (j < 0 || j >= oldRowCnt) continue;

                        double lanczosFactorX = LanczosFilter(x - i, A);
                        double lanczosFactorY = LanczosFilter(y - j, A);
                        double lanczosFactor = lanczosFactorX * lanczosFactorY;

                        newPixel.r += oldPixels[(j * oldColCnt) + i].r * (float)lanczosFactor;
                        newPixel.g += oldPixels[(j * oldColCnt) + i].g * (float)lanczosFactor;
                        newPixel.b += oldPixels[(j * oldColCnt) + i].b * (float)lanczosFactor;
                    }

                newPixels[(row * newColCnt) + col] = newPixel;
            }

        newImage.SetPixels(newPixels);
        newImage.Apply();
        return newImage;
    }

    private static ushort[,,] DownsampleLanczos3(ushort[,,] image, double divisor)
    {
        uint newRowCnt = (uint)Math.Floor(image.GetLength(0) / divisor);
        uint newColCnt = (uint)Math.Floor(image.GetLength(1) / divisor);
        ushort[,,] retVal = new ushort[newRowCnt, newColCnt, 3];
        for (int row = A + 1; row < newRowCnt - A - 1; row++)
            for (int col = A + 1; col < newColCnt - A - 1; col++)
            {
                //Find row and col in terms of image coord
                double y = row * divisor;
                double x = col * divisor;
                double x1 = Math.Floor(x);
                double y1 = Math.Floor(y);

                //Note that x2-x1 = 1 & y2 - y1 = 1
                for (int color = 0; color < 3; color++)
                {
                    retVal[row, col, color] = 0;
                    for (int i = (int)x1 - A + 1; i <= (int)x1 + A; i++)
                        for (int j = (int)y1 - A + 1; j < (int)y1 + A; j++)
                        {
                            double lanczosFactorX = LanczosFilter(x - i, A);
                            double lanczosFactorY = LanczosFilter(y - j, A);
                            retVal[row, col, color] += (ushort)(image[j, i, color] * lanczosFactorX * lanczosFactorY);
                        }
                }
            }
        return retVal;
    }

    // Converts a Texture2D to a Color array
    private static Color[,] TextureTo2DArray(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        Color[] pixels = texture.GetPixels();
        Color[,] result = new Color[height, width];

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
                result[i, j] = pixels[(i * width) + j];

        return result;
    }

    // Converts a Color array to a ushort array
    // INSECURE, PRIVATE ONLY
    // QUALITY LOSS
    private static ushort[,,] ConvertColorArrayToUShortArray(Color[,] colorArray, int width, int height)
    {
        ushort[,,] ushortArray = new ushort[height, width, 3];  // Only RGB channels, no alpha

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                ushortArray[i, j, 0] = (ushort)(colorArray[i, j].r * 65535); // Convert red
                ushortArray[i, j, 1] = (ushort)(colorArray[i, j].g * 65535); // Convert green
                ushortArray[i, j, 2] = (ushort)(colorArray[i, j].b * 65535); // Convert blue
            }
        }

        return ushortArray;
    }

    // Converts a ushort array to a Color array
    // INSECURE, PRIVATE ONLY
    private static Color[,] ConvertUShortArrayToColorArray(ushort[,,] ushortArray, int width, int height)
    {
        Color[,] colorArray = new Color[height, width];

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                float r = ushortArray[i, j, 0] / 65535f; // Scale the ushort to a float between 0.0 and 1.0
                float g = ushortArray[i, j, 1] / 65535f; // Scale the ushort to a float between 0.0 and 1.0
                float b = ushortArray[i, j, 2] / 65535f; // Scale the ushort to a float between 0.0 and 1.0
                colorArray[i, j] = new Color(r, g, b);
            }
        }

        return colorArray;
    }

    // Converts a Color array to a Texture2D
    // INSECURE, PRIVATE ONLY
    private static Texture2D ArrayToTexture2D(Color[,] array, int width, int height)
    {
        Texture2D texture = new(width, height);
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
                pixels[(i * width) + j] = array[i, j];

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}

#pragma warning restore CA1814