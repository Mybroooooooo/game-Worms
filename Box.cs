using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace Worms
{
    public class Box
    {
        public Image Image { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public WeaponType WeaponInside { get; set; }
        public bool IsOpened { get; set; } = false;

        private const double BOX_WIDTH = 40;
        private const double BOX_HEIGHT = 40;
        private BitmapImage boxSprite;
        private Canvas parentCanvas;

        public Box(double x, double y, WeaponType weaponType)
        {
            X = x;
            Y = y;
            WeaponInside = weaponType;

            LoadSprite();

            Image = new Image
            {
                Source = boxSprite,
                Width = BOX_WIDTH,
                Height = BOX_HEIGHT,
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Visible,
                Opacity = 1.0
            };
        }

        private void LoadSprite()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string boxPath = Path.Combine(basePath, "Sprites", "Misc", "box.png");
                boxSprite = new BitmapImage(new Uri(boxPath));
            }
            catch (Exception ex)
            {
            }
        }

        public void AddToCanvas(Canvas canvas)
        {
            parentCanvas = canvas;
            canvas.Children.Add(Image);
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (parentCanvas == null) return;

            Canvas.SetLeft(Image, X);
            Canvas.SetTop(Image, Y);
        }

        public void RemoveFromCanvas()
        {
            if (parentCanvas != null)
            {
                parentCanvas.Children.Remove(Image);
            }
        }

        public void Open()
        {
            IsOpened = true;
            RemoveFromCanvas();
        }

        public double Width => BOX_WIDTH;
        public double Height => BOX_HEIGHT;

        public Rect GetBounds()
        {
            return new Rect(X, Y, Width, Height);
        }
    }
}