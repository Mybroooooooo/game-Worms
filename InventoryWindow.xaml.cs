using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Worms
{
    public partial class InventoryWindow : Window
    {
        private Worm currentWorm;

        public InventoryWindow(Worm worm)
        {
            InitializeComponent();
            currentWorm = worm;
            LoadWeapons();

            PreviewKeyDown += OnKeyDown;

            Loaded += (s, e) => this.Focus();
        }

        private void LoadWeapons()
        {
            WeaponsList.Items.Clear();

            foreach (var weapon in currentWorm.Inventory.Where(w => w.IsAvailable))
            {
                var weaponItem = CreateWeaponItem(weapon);
                WeaponsList.Items.Add(weaponItem);
            }

            StatusText.Text = $"Инвентарь: {currentWorm.Inventory.Count(w => w.IsAvailable)} доступно";
        }

        private Border CreateWeaponItem(Weapon weapon)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromArgb(255, 62, 62, 66)),
                CornerRadius = new CornerRadius(5),
                Tag = weapon.Type
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0)
            };

            var image = new Image
            {
                Source = weapon.Icon,
                Width = 40,
                Height = 40,
                Margin = new Thickness(10, 5, 10, 5),
                Stretch = Stretch.Uniform
            };

            if (weapon.Type == WeaponType.Teleport)
            {
                image.Width = 50;
                image.Height = 50;
                image.Margin = new Thickness(5, 5, 10, 5);
            }

            var infoPanel = new StackPanel
            {
                Margin = new Thickness(0, 5, 0, 5),
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = weapon.Name,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };

            var ammoText = new TextBlock
            {
                Text = $"Боеприпасы: {(weapon.Ammo == -1 ? "∞" : weapon.Ammo.ToString())}",
                Foreground = weapon.Ammo > 0 || weapon.Ammo == -1 ? Brushes.LightGreen : Brushes.Red,
                FontSize = 12
            };

            var damageText = new TextBlock
            {
                Text = $"Урон: {weapon.Damage}",
                Foreground = Brushes.LightGray,
                FontSize = 12
            };

            TextBlock currentIndicator = null;
            if (weapon == currentWorm.CurrentWeapon)
            {
                currentIndicator = new TextBlock
                {
                    Text = "✓",
                    Foreground = Brushes.LimeGreen,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            infoPanel.Children.Add(nameText);
            infoPanel.Children.Add(ammoText);
            infoPanel.Children.Add(damageText);

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(infoPanel);

            if (currentIndicator != null)
            {
                stackPanel.Children.Add(currentIndicator);
            }

            border.Child = stackPanel;

            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 84));
            border.MouseLeave += (s, e) => border.Background = new SolidColorBrush(Color.FromArgb(255, 62, 62, 66));

            border.MouseLeftButtonDown += (s, e) =>
            {
                if (border.Tag is WeaponType weaponType)
                {
                    SelectWeapon(weaponType);
                }
            };

            return border;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.I)
            {
                Close();
            }
        }

        private void SelectWeapon(WeaponType weaponType)
        {
            var weapon = currentWorm.Inventory.FirstOrDefault(w => w.Type == weaponType && w.IsAvailable);
            if (weapon != null)
            {
                currentWorm.ChangeWeapon(weaponType);
                LoadWeapons();
                StatusText.Text = $"Выбрано: {weapon.Name}";
            }
            else
            {
                StatusText.Text = "Оружие недоступно!";
            }
        }
    }
}