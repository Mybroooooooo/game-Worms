using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Worms
{
    public class Projectile
    {
        public FrameworkElement Shape { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double Damage { get; set; }
        public double ExplosionRadius { get; set; }
        public double KnockbackPower { get; set; }
        public bool IsActive { get; set; } = true;
        public double Radius { get; set; } = 3;
        public Color Color { get; set; } = Colors.Black;
        public WeaponType WeaponType { get; set; }
        public int FuseTime { get; set; } = 0;
        public bool HasExploded { get; set; } = false;
        public bool HasHitGround { get; set; } = false;
        public int BounceCount { get; set; } = 0;
        public int MaxBounces { get; set; } = 3;
        public int ShooterWormId { get; set; } = -1;

        private const double GRAVITY = 0.5;
        private Canvas parentCanvas;
        private DispatcherTimer fuseTimer;
        private GameWindow gameWindow;

        public Projectile(double x, double y, double velocityX, double velocityY,
            double damage, WeaponType weaponType, double explosionRadius = 40,
            double knockbackPower = 10, int fuseTime = 0,
            GameWindow window = null, Color? color = null, BitmapImage sprite = null,
            int shooterWormId = -1)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Damage = damage;
            WeaponType = weaponType;
            ExplosionRadius = explosionRadius;
            KnockbackPower = knockbackPower;
            FuseTime = fuseTime;
            gameWindow = window;
            ShooterWormId = shooterWormId;

            if (WeaponType == WeaponType.Grenade || WeaponType == WeaponType.Dynamite || WeaponType == WeaponType.Sheep)
            {
                MaxBounces = 2;
            }

            switch (weaponType)
            {
                case WeaponType.Grenade:
                case WeaponType.Dynamite:
                case WeaponType.Sheep:
                    Radius = 12;
                    break;
                default:
                    Radius = 3;
                    break;
            }

            if (color.HasValue)
                Color = color.Value;

            if (sprite != null)
            {
                Shape = new Image
                {
                    Source = sprite,
                    Width = Radius * 4,
                    Height = Radius * 4,
                    Stretch = Stretch.Uniform
                };
            }
            else
            {
                Shape = new Ellipse
                {
                    Width = Radius * 2,
                    Height = Radius * 2,
                    Fill = new SolidColorBrush(Color),
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };
            }

            if (FuseTime > 0 && (WeaponType == WeaponType.Dynamite || WeaponType == WeaponType.Grenade || WeaponType == WeaponType.Sheep))
            {
                fuseTimer = new DispatcherTimer();
                fuseTimer.Interval = TimeSpan.FromMilliseconds(FuseTime);
                fuseTimer.Tick += OnFuseTimerTick;
                fuseTimer.Start();
            }
        }

        private void OnFuseTimerTick(object sender, EventArgs e)
        {
            if (fuseTimer != null)
            {
                fuseTimer.Stop();
                fuseTimer = null;
            }

            if (!HasExploded && IsActive)
            {
                Explode();
            }
        }

        public void AddToCanvas(Canvas canvas)
        {
            parentCanvas = canvas;
            canvas.Children.Add(Shape);
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (parentCanvas == null) return;

            if (Shape is Image)
            {
                Canvas.SetLeft(Shape, X - Radius * 2);
                Canvas.SetTop(Shape, Y - Radius * 2);
            }
            else
            {
                Canvas.SetLeft(Shape, X - Radius);
                Canvas.SetTop(Shape, Y - Radius);
            }
        }

        public void Update()
        {
            if (HasExploded || HasHitGround) return;

            VelocityY += GRAVITY;

            X += VelocityX;
            Y += VelocityY;

            UpdatePosition();
        }

        public void RemoveFromCanvas()
        {
            if (parentCanvas != null && Shape != null && parentCanvas.Children.Contains(Shape))
            {
                parentCanvas.Children.Remove(Shape);
            }
        }

        public Rect GetBounds()
        {
            if (Shape is Image)
            {
                return new Rect(X - Radius * 2, Y - Radius * 2, Radius * 4, Radius * 4);
            }
            return new Rect(X - Radius, Y - Radius, Radius * 2, Radius * 2);
        }

        public void Explode()
        {
            if (HasExploded) return;

            HasExploded = true;
            IsActive = false;
            HasHitGround = true;

            CreateExplosionEffect();

            RemoveFromCanvas();

            if (gameWindow != null)
            {
                gameWindow.HandleExplosion(this);
            }
        }

        private void CreateExplosionEffect()
        {
            if (parentCanvas == null) return;

            var explosion = new Ellipse
            {
                Width = ExplosionRadius * 2,
                Height = ExplosionRadius * 2,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Colors.Orange, 0),
                        new GradientStop(Colors.Red, 0.5),
                        new GradientStop(Colors.Transparent, 1)
                    }
                },
                Opacity = 0.8
            };

            parentCanvas.Children.Add(explosion);
            Canvas.SetLeft(explosion, X - ExplosionRadius);
            Canvas.SetTop(explosion, Y - ExplosionRadius);
            Canvas.SetZIndex(explosion, 999);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            timer.Tick += (s, e) =>
            {
                if (parentCanvas.Children.Contains(explosion))
                {
                    parentCanvas.Children.Remove(explosion);
                }
                timer.Stop();
            };
            timer.Start();
        }

        public void HitGround()
        {
            if (HasHitGround) return;

            if (WeaponType == WeaponType.Bazooka || WeaponType == WeaponType.Airstrike)
            {
                Explode();
            }
            else if (WeaponType == WeaponType.Shotgun)
            {
                IsActive = false;
                RemoveFromCanvas();
            }
            else if (WeaponType == WeaponType.Dynamite || WeaponType == WeaponType.Grenade ||
                     WeaponType == WeaponType.Sheep)
            {
                if (BounceCount < MaxBounces)
                {
                    VelocityY = -VelocityY * 0.7;
                    VelocityX *= 0.8;
                    BounceCount++;

                }
                else
                {
                    HasHitGround = true;
                    VelocityX = 0;
                    VelocityY = 0;
                }
            }
        }

        public Point GetCenter()
        {
            return new Point(X, Y);
        }

        public void Cleanup()
        {
            if (fuseTimer != null)
            {
                fuseTimer.Stop();
                fuseTimer = null;
            }
        }
    }
}