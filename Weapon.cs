using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Worms
{
    public enum WeaponType
    {
        Shotgun,
        Sheep,
        Grenade,
        Dynamite,
        BaseballBat,
        Teleport,
        Parachute,
        Airstrike,
        Bazooka
    }

    public class Weapon
    {
        public WeaponType Type { get; }
        public string Name { get; }
        public BitmapImage Icon { get; private set; }
        public BitmapImage WormSprite { get; private set; }
        public BitmapImage ProjectileSprite { get; private set; }
        public int Ammo { get; set; }
        public int Damage { get; set; }
        public double Range { get; set; }
        public double ExplosionRadius { get; set; }
        public double KnockbackPower { get; set; }
        public double ProjectileSpeed { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int FuseTime { get; set; }
        public bool RequiresAiming { get; set; } = true;
        public bool IsProjectile { get; set; } = true;

        public Weapon(WeaponType type)
        {
            Type = type;

            switch (type)
            {
                case WeaponType.Shotgun:
                    Name = "Shotgun";
                    Ammo = -1;
                    Damage = 25;
                    Range = 300;
                    ExplosionRadius = 20;
                    KnockbackPower = 15;
                    ProjectileSpeed = 20;
                    FuseTime = 0;
                    RequiresAiming = true;
                    IsProjectile = true;
                    break;

                case WeaponType.Sheep:
                    Name = "Sheep";
                    Ammo = 3;
                    Damage = 50;
                    Range = 200;
                    ExplosionRadius = 80;
                    KnockbackPower = 20;
                    ProjectileSpeed = 15;
                    FuseTime = 3000;
                    RequiresAiming = true;
                    IsProjectile = true;
                    break;

                case WeaponType.Grenade:
                    Name = "Grenade";
                    Ammo = 5;
                    Damage = 30;
                    Range = 400;
                    ExplosionRadius = 60;
                    KnockbackPower = 12;
                    ProjectileSpeed = 18;
                    FuseTime = 2000;
                    RequiresAiming = true;
                    IsProjectile = true;
                    break;

                case WeaponType.Dynamite:
                    Name = "Dynamite";
                    Ammo = 2;
                    Damage = 75;
                    Range = 150;
                    ExplosionRadius = 100;
                    KnockbackPower = 25;
                    ProjectileSpeed = 12;
                    FuseTime = 5000;
                    RequiresAiming = true;
                    IsProjectile = true;
                    break;

                case WeaponType.BaseballBat:
                    Name = "Baseball Bat";
                    Ammo = -1;
                    Damage = 15;
                    Range = 50;
                    ExplosionRadius = 0;
                    KnockbackPower = 30;
                    ProjectileSpeed = 0;
                    FuseTime = 0;
                    RequiresAiming = false;
                    IsProjectile = false;
                    break;

                case WeaponType.Teleport:
                    Name = "Teleport";
                    Ammo = 3;
                    Damage = 0;
                    Range = 500;
                    ExplosionRadius = 0;
                    KnockbackPower = 0;
                    ProjectileSpeed = 0;
                    FuseTime = 0;
                    RequiresAiming = false;
                    IsProjectile = false;
                    break;

                case WeaponType.Parachute:
                    Name = "Parachute";
                    Ammo = 1;
                    Damage = 0;
                    Range = 0;
                    ExplosionRadius = 0;
                    KnockbackPower = 0;
                    ProjectileSpeed = 0;
                    FuseTime = 0;
                    RequiresAiming = false;
                    IsProjectile = false;
                    break;

                case WeaponType.Airstrike:
                    Name = "Airstrike";
                    Ammo = 1;
                    Damage = 100;
                    Range = 1000;
                    ExplosionRadius = 150;
                    KnockbackPower = 40;
                    ProjectileSpeed = 30;
                    FuseTime = 0;
                    RequiresAiming = false;
                    IsProjectile = true;
                    break;

                case WeaponType.Bazooka:
                    Name = "Bazooka";
                    Ammo = 5;
                    Damage = 80;
                    Range = 600;
                    ExplosionRadius = 120;
                    KnockbackPower = 35;
                    ProjectileSpeed = 25;
                    FuseTime = 0;
                    RequiresAiming = true;
                    IsProjectile = true;
                    break;
            }

            LoadSprites();
        }

        private void LoadSprites()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                string iconPath = Path.Combine(basePath, "Sprites", "Weapons", $"icon_{Type.ToString().ToLower()}.png");
                Icon = new BitmapImage(new Uri(iconPath));
                if (Type == WeaponType.Teleport || Type == WeaponType.Parachute)
                {
                    Icon.DecodePixelWidth = 40;
                    Icon.DecodePixelHeight = 40;
                }

                string wormSpritePath = Path.Combine(basePath, "Sprites", "WormsWithWeapons", $"worm_with_{Type.ToString().ToLower()}.png");
                WormSprite = new BitmapImage(new Uri(wormSpritePath));
                if (Type == WeaponType.Teleport)
                {
                    WormSprite.DecodePixelWidth = 5;
                    WormSprite.DecodePixelHeight = 5;
                }
                else if (Type == WeaponType.Parachute)
                {
                    WormSprite.DecodePixelWidth = 40;
                    WormSprite.DecodePixelHeight = 40;
                }

                if (IsProjectile)
                {
                    string projectilePath = Path.Combine(basePath, "Sprites", "Misc", $"{Type.ToString().ToLower()}_projectile.png");
                    if (File.Exists(projectilePath))
                    {
                        ProjectileSprite = new BitmapImage(new Uri(projectilePath));
                    }
                    else if (Type == WeaponType.Airstrike)
                    {
                        projectilePath = Path.Combine(basePath, "Sprites", "Misc", "bomb.png");
                        if (File.Exists(projectilePath))
                        {
                            ProjectileSprite = new BitmapImage(new Uri(projectilePath));
                        }
                        else
                        {
                            ProjectileSprite = Icon;
                        }
                    }
                    else
                    {
                        ProjectileSprite = Icon;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        public void Use()
        {
            if (Ammo > 0)
            {
                Ammo--;
                if (Ammo <= 0)
                {
                    IsAvailable = false;
                }
            }
        }
    }
}