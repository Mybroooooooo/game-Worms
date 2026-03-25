using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Worms
{
    public class CollisionMask
    {
        private readonly WriteableBitmap mask;
        private readonly int width;
        private readonly int height;
        private readonly byte[] pixels;

        private const byte GroundThreshold = 200;

        public CollisionMask(WriteableBitmap maskBitmap)
        {
            mask = maskBitmap;
            width = mask.PixelWidth;
            height = mask.PixelHeight;
            pixels = new byte[width * height * 4];
            mask.CopyPixels(pixels, width * 4, 0);
        }

        public bool IsGround(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            int idx = (y * width + x) * 4;
            byte r = pixels[idx + 2];
            byte g = pixels[idx + 1];
            byte b = pixels[idx];

            return r > GroundThreshold && g > GroundThreshold && b > GroundThreshold;
        }

        public bool IsSpawnArea(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            int idx = (y * width + x) * 4;
            byte r = pixels[idx + 2];
            byte g = pixels[idx + 1];
            byte b = pixels[idx];
            byte a = pixels[idx + 3];

            return a > 200 && r > 200 && g < 100 && b < 100;
        }

        public int? FindSurfaceBelow(int x, int y, int maxDepth = 100)
        {
            if (x < 0 || x >= width)
                return null;

            int startY = Math.Min(y, height - 1);
            int endY = Math.Min(y + maxDepth, height - 1);

            for (int checkY = startY; checkY <= endY; checkY++)
            {
                if (IsGround(x, checkY))
                {
                    for (int surfaceY = checkY; surfaceY >= Math.Max(0, checkY - 10); surfaceY--)
                    {
                        if (surfaceY == 0 || !IsGround(x, surfaceY - 1))
                        {
                            return surfaceY;
                        }
                    }
                    return checkY;
                }
            }

            return null;
        }

        public int? FindGroundBelow(int x, int y, int maxDepth = 100)
        {
            int startY = Math.Min(y, height - 1);
            int endY = Math.Min(y + maxDepth, height - 1);

            for (int checkY = startY; checkY <= endY; checkY++)
            {
                if (IsGround(x, checkY))
                {
                    return checkY;
                }
            }

            return null;
        }

        public int? FindCeilingAbove(int x, int y, int maxHeight = 100)
        {
            int startY = Math.Max(y, 0);
            int endY = Math.Max(y - maxHeight, 0);

            for (int checkY = startY; checkY >= endY; checkY--)
            {
                if (IsGround(x, checkY))
                {
                    return checkY;
                }
            }

            return null;
        }

        public void RemovePixel(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int idx = (y * width + x) * 4;

            pixels[idx] = 0;
            pixels[idx + 1] = 0;
            pixels[idx + 2] = 0;
            pixels[idx + 3] = 255;

            Int32Rect rect = new Int32Rect(x, y, 1, 1);
            mask.WritePixels(rect, pixels, width * 4, idx);
        }

        public void DestroyCircularArea(int centerX, int centerY, int radius)
        {
            if (centerX < 0 || centerY < 0 || centerX >= width || centerY >= height)
                return;

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height)
                        continue;

                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        RemovePixel(x, y);
                    }
                }
            }
        }
        public int Width => width;
        public int Height => height;
    }
}