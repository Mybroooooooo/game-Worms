using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Worms
{
    public class Worm
    {
        public int Id { get; }
        private static int nextId = 1;
        public Image Image { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double VelocityY { get; set; } = 0;
        public bool IsJumping { get; set; } = false;
        public bool FacingRight { get; set; } = true;
        public Color TeamColor { get; set; }
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public bool IsUsingParachute { get; set; } = false;
        public bool ParachuteDeployed { get; set; } = false;
        public double OriginalGravityScale { get; set; } = 1.0;
        public int TeamIndex { get; set; }

        public Weapon CurrentWeapon { get; set; }
        public List<Weapon> Inventory { get; private set; }

        private BitmapImage defaultNormalSprite;
        private BitmapImage defaultJumpSprite;
        private BitmapImage normalSprite;
        private BitmapImage jumpSprite;
        private BitmapImage parachuteSprite;
        private BitmapImage parachuteJumpSprite;

        private TextBlock nameTextBlock;
        private TextBlock healthTextBlock;
        private Canvas parentCanvas;
        private DoubleAnimation pulseAnimation;
        private ScaleTransform directionTransform;
        private ScaleTransform pulseTransform;
        private TransformGroup transformGroup;

        private const double WORM_WIDTH = 40;
        private const double WORM_HEIGHT = 40;

        public event Action<string> OnWeaponAdded;
        public event Action<int> OnDamageTaken;
        public event Action OnDeath;

        public Worm(string name, double startX, double startY, Color teamColor, int teamIndex)
        {
            Id = nextId++;
            Name = name;
            X = startX;
            Y = startY;
            TeamColor = teamColor;
            TeamIndex = teamIndex;
            Inventory = new List<Weapon>();

            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                string normalPath = Path.Combine(basePath, "Sprites", "Worms", "worm_right.png");
                if (File.Exists(normalPath))
                {
                    defaultNormalSprite = new BitmapImage(new Uri(normalPath));

                    string jumpPath = Path.Combine(basePath, "Sprites", "Worms", "worm_jump.png");
                    if (File.Exists(jumpPath))
                    {
                        defaultJumpSprite = new BitmapImage(new Uri(jumpPath));
                    }
                }

                LoadParachuteSprite();

                normalSprite = defaultNormalSprite;
                jumpSprite = defaultJumpSprite;
            }
            catch (Exception ex)
            {
            }

            directionTransform = new ScaleTransform(1, 1);
            pulseTransform = new ScaleTransform(1, 1);
            transformGroup = new TransformGroup();
            transformGroup.Children.Add(directionTransform);
            transformGroup.Children.Add(pulseTransform);

            Image = new Image
            {
                Source = normalSprite,
                Width = WORM_WIDTH,
                Height = WORM_HEIGHT,
                Stretch = Stretch.Uniform,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = transformGroup,
                Visibility = Visibility.Visible,
                Opacity = 1.0
            };

            CreateUIElements();
            UpdatePosition();
        }

        private void LoadParachuteSprite()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string parachutePath = Path.Combine(basePath, "Sprites", "WormsWithWeapons", "worm_with_parachute.png");

                if (File.Exists(parachutePath))
                {
                    parachuteSprite = new BitmapImage(new Uri(parachutePath));
                    parachuteJumpSprite = new BitmapImage();
                    parachuteJumpSprite.BeginInit();
                    parachuteJumpSprite.UriSource = new Uri(parachutePath);
                    parachuteJumpSprite.DecodePixelWidth = (int)WORM_WIDTH;
                    parachuteJumpSprite.DecodePixelHeight = (int)WORM_HEIGHT;
                    parachuteJumpSprite.EndInit();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void InitializeWeapons()
        {
            var shotgun = new Weapon(WeaponType.Shotgun);
            Inventory.Add(shotgun);
            CurrentWeapon = shotgun;
            UpdateWeaponSprite();
        }

        public void UpdateWeaponSprite()
        {
            if (CurrentWeapon != null && CurrentWeapon.WormSprite != null && CurrentWeapon.Type != WeaponType.Parachute)
            {
                normalSprite = CurrentWeapon.WormSprite;
                jumpSprite = defaultJumpSprite;
                UpdateSpriteAndDirection();
            }
            else if (CurrentWeapon != null && CurrentWeapon.Type == WeaponType.Parachute)
            {
                normalSprite = defaultNormalSprite;
                jumpSprite = parachuteJumpSprite;
                UpdateSpriteAndDirection();
            }
            else
            {
                normalSprite = defaultNormalSprite;
                jumpSprite = defaultJumpSprite;
                UpdateSpriteAndDirection();
            }
        }

        public void ChangeWeapon(WeaponType weaponType)
        {
            var weapon = Inventory.FirstOrDefault(w => w.Type == weaponType && w.IsAvailable);
            if (weapon != null)
            {
                CurrentWeapon = weapon;
                UpdateWeaponSprite();
            }
        }

        public void AddWeapon(WeaponType weaponType)
        {
            var existingWeapon = Inventory.FirstOrDefault(w => w.Type == weaponType);
            if (existingWeapon != null)
            {
                existingWeapon.Ammo += 5;
                OnWeaponAdded?.Invoke($"+5 ammo for {weaponType}");
            }
            else
            {
                var weapon = new Weapon(weaponType);
                Inventory.Add(weapon);
                OnWeaponAdded?.Invoke($"New weapon: {weaponType}");
            }
        }

        public void UseCurrentWeapon()
        {
            if (CurrentWeapon != null && CurrentWeapon.IsAvailable)
            {
                CurrentWeapon.Use();
                if (CurrentWeapon.Ammo <= 0 && CurrentWeapon.Type != WeaponType.Shotgun)
                {
                    var shotgun = Inventory.FirstOrDefault(w => w.Type == WeaponType.Shotgun);
                    if (shotgun != null)
                    {
                        CurrentWeapon = shotgun;
                        UpdateWeaponSprite();
                    }
                }
            }
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;
            int oldHealth = Health;
            int newHealth = Health - damage;
            UpdateHealth(newHealth);
            StartDamageAnimation();
            OnDamageTaken?.Invoke(damage);

            if (Health <= 0 && oldHealth > 0)
            {
                Die();
            }
        }

        public void UpdateHealth(int newHealth)
        {
            Health = Math.Max(0, Math.Min(newHealth, MaxHealth));
            healthTextBlock.Text = $"{Health}%";

            if (Health > 70)
                healthTextBlock.Foreground = Brushes.LimeGreen;
            else if (Health > 30)
                healthTextBlock.Foreground = Brushes.Yellow;
            else
                healthTextBlock.Foreground = Brushes.Red;
        }

        private void Die()
        {
            StartDeathAnimation();
            OnDeath?.Invoke();
        }

        private void StartDamageAnimation()
        {
            var colorAnimation = new ColorAnimation
            {
                To = Colors.Red,
                Duration = TimeSpan.FromSeconds(0.1),
                AutoReverse = true
            };
            var brush = new SolidColorBrush(Colors.White);
            healthTextBlock.Foreground = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
        }

        private void StartDeathAnimation()
        {
            var fadeAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            Image.BeginAnimation(Image.OpacityProperty, fadeAnimation);
            nameTextBlock.BeginAnimation(TextBlock.OpacityProperty, fadeAnimation);
            healthTextBlock.BeginAnimation(TextBlock.OpacityProperty, fadeAnimation);

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.6)
            };
            timer.Tick += (s, e) =>
            {
                Image.Visibility = Visibility.Collapsed;
                nameTextBlock.Visibility = Visibility.Collapsed;
                healthTextBlock.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void CreateUIElements()
        {
            nameTextBlock = new TextBlock
            {
                Text = Name,
                Foreground = new SolidColorBrush(TeamColor),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            healthTextBlock = new TextBlock
            {
                Text = $"{Health}%",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(2, 1, 2, 1)
            };
        }

        public void AddToCanvas(Canvas canvas)
        {
            parentCanvas = canvas;
            canvas.Children.Add(Image);
            canvas.Children.Add(nameTextBlock);
            canvas.Children.Add(healthTextBlock);
            UpdateUIPosition();
        }

        private void UpdateUIPosition()
        {
            if (parentCanvas == null) return;

            Canvas.SetLeft(Image, X);
            Canvas.SetTop(Image, Y);

            Canvas.SetLeft(nameTextBlock, X + (Width - nameTextBlock.ActualWidth) / 2);
            Canvas.SetTop(nameTextBlock, Y - 15);

            Canvas.SetLeft(healthTextBlock, X + (Width - healthTextBlock.ActualWidth) / 2);
            Canvas.SetTop(healthTextBlock, Y + Height + 1);
        }

        public void StartPulseAnimation()
        {
            StopPulseAnimation();
            pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.15,
                Duration = TimeSpan.FromSeconds(0.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            pulseTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            pulseTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);
        }

        public void StopPulseAnimation()
        {
            if (pulseAnimation != null)
            {
                pulseTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                pulseTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                pulseTransform.ScaleX = 1;
                pulseTransform.ScaleY = 1;
                pulseAnimation = null;
            }
        }

        public void UpdateSpriteAndDirection()
        {
            if (Image == null) return;

            try
            {
                if ((IsUsingParachute || ParachuteDeployed) && IsJumping)
                {
                    Image.Source = parachuteJumpSprite;
                    Image.Width = WORM_WIDTH;
                    Image.Height = WORM_HEIGHT;
                }
                else if (IsJumping)
                {
                    Image.Source = jumpSprite;
                    if (CurrentWeapon != null && CurrentWeapon.Type == WeaponType.Teleport)
                    {
                        Image.Width = 35;
                        Image.Height = 35;
                    }
                    else
                    {
                        Image.Width = WORM_WIDTH;
                        Image.Height = WORM_HEIGHT;
                    }
                }
                else
                {
                    Image.Source = normalSprite;
                    if (CurrentWeapon != null && CurrentWeapon.Type == WeaponType.Teleport)
                    {
                        Image.Width = 35;
                        Image.Height = 35;
                    }
                    else
                    {
                        Image.Width = WORM_WIDTH;
                        Image.Height = WORM_HEIGHT;
                    }
                }

                directionTransform.ScaleX = FacingRight ? 1 : -1;
            }
            catch (Exception ex)
            {
            }
        }

        public void UpdatePosition()
        {
            UpdateUIPosition();
        }

        public double Width => Image.Width;
        public double Height => Image.Height;

        public void RemoveFromCanvas()
        {
            if (parentCanvas != null)
            {
                parentCanvas.Children.Remove(Image);
                parentCanvas.Children.Remove(nameTextBlock);
                parentCanvas.Children.Remove(healthTextBlock);
            }
        }

        public string GetWeaponInfo()
        {
            if (CurrentWeapon != null)
            {
                return $"{CurrentWeapon.Name} ({(CurrentWeapon.Ammo == -1 ? "∞" : CurrentWeapon.Ammo.ToString())})";
            }
            return "No weapon";
        }

        public bool HasWeapon(WeaponType weaponType)
        {
            return Inventory.Any(w => w.Type == weaponType && w.IsAvailable);
        }

        public Weapon GetWeapon(WeaponType weaponType)
        {
            return Inventory.FirstOrDefault(w => w.Type == weaponType);
        }

        public Rect GetBounds()
        {
            return new Rect(X, Y, Width, Height);
        }

        public bool IntersectsWith(Rect rect)
        {
            return GetBounds().IntersectsWith(rect);
        }

        public bool ContainsPoint(Point point)
        {
            return GetBounds().Contains(point);
        }

        public Point GetCenter()
        {
            return new Point(X + Width / 2, Y + Height / 2);
        }

        public void Reset()
        {
            Health = MaxHealth;
            VelocityY = 0;
            IsJumping = false;
            IsUsingParachute = false;
            ParachuteDeployed = false;
            StopPulseAnimation();
            UpdateHealth(Health);
        }
    }
}