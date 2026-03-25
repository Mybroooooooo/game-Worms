using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Worms
{
    public class TerrainMap
    {
        private WriteableBitmap terrainBitmap;
        private int width;
        private int height;
        private byte[] terrainPixels;

        private const byte AlphaThreshold = 10;

        public TerrainMap(WriteableBitmap texture)
        {
            terrainBitmap = texture;
            width = texture.PixelWidth;
            height = texture.PixelHeight;
            terrainPixels = new byte[width * height * 4];
            texture.CopyPixels(terrainPixels, width * 4, 0);
        }

        public bool IsGround(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            int idx = (y * width + x) * 4;
            byte alpha = terrainPixels[idx + 3];

            return alpha > AlphaThreshold;
        }

        public void RemovePixel(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int idx = (y * width + x) * 4;

            terrainPixels[idx] = 235;
            terrainPixels[idx + 1] = 206;
            terrainPixels[idx + 2] = 135;
            terrainPixels[idx + 3] = 255;

            Int32Rect rect = new Int32Rect(x, y, 1, 1);
            terrainBitmap.WritePixels(rect, terrainPixels, width * 4, idx);
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

        public int Width => width;
        public int Height => height;
        public WriteableBitmap Bitmap => terrainBitmap;
    }
}