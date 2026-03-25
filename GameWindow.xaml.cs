using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace Worms
{
    public partial class GameWindow : Window
    {
        private List<Worm> worms = new List<Worm>();
        private Worm currentWorm;
        private bool leftPressed, rightPressed;
        private const double MOVE_SPEED = 5.0;
        private const double GRAVITY = 0.8;
        private const double JUMP_POWER = -15.0;
        private Canvas wormCanvas;
        private WriteableBitmap terrainBitmap;
        private int terrainWidth, terrainHeight;
        private double scaleX = 1.0, scaleY = 1.0;
        private GameData gameData;
        private CollisionMask collisionMaskObj;
        private Random random = new Random();
        private Dictionary<int, int> teamWormCursors = new Dictionary<int, int>();
        private DispatcherTimer aiMoveTimer;
        private Worm aiTargetWorm;
        private double aiMoveTimeout;
        private DispatcherTimer aiMovementTimer;
        private Worm aiTargetEnemy = null;
        private Box aiTargetBox = null;

        private TextBlock weaponInfoText;
        private Border weaponInfoPanel;

        private List<Box> boxes = new List<Box>();
        private const int BOX_COUNT = 5;
        private Canvas boxCanvas;

        private List<Projectile> projectiles = new List<Projectile>();
        private Canvas projectileCanvas;

        private enum AimingState { None, Aiming, SelectingPower }
        private AimingState currentAimingState = AimingState.None;
        private double aimAngle = 45.0;
        private double powerLevel = 0.0;
        private bool isPowerIncreasing = true;
        private const double POWER_CHANGE_SPEED = 1.0;
        private const double MAX_POWER = 100.0;
        private const double AIM_LINE_LENGTH = 200.0;
        private Line aimLine;
        private ProgressBar powerBar;
        private TextBlock powerText;

        private TerrainMap terrainMap;

        private bool isSelectingTeleport = false;
        private Point teleportTarget;
        private Ellipse teleportMarker;
        private bool isSelectingAirstrike = false;
        private Point airstrikeTarget;
        private Rectangle airstrikeMarker;

        private int currentTurnIndex = 0;

        private TextBlock statusMessageText;
        private DispatcherTimer statusMessageTimer;

        private TextBlock killMessageText;
        private DispatcherTimer killMessageTimer;
        private Queue<Tuple<string, Color>> killMessageQueue = new Queue<Tuple<string, Color>>(); private bool isShowingKillMessage = false;

        private bool isAITurn = false;
        private bool isVsComputerMode = false;
        private bool isPlayerTurn = true;

        private int aiJumpCount = 0;
        private double aiLastPositionX = 0;
        private DateTime aiLastJumpTime = DateTime.MinValue;
        private DateTime aiPathStartTime = DateTime.MinValue;

        private Pathfinder pathfinder;
        private List<Point> currentAIPath;
        private int aiPathIndex;

        private DispatcherTimer turnTimer;
        private const int TURN_TIME_SECONDS = 30;
        private int remainingSeconds;
        private TextBlock turnTimerText;
        private Border turnTimerPanel;

        public GameWindow(GameData data)
        {
            InitializeComponent();
            gameData = data;
            isVsComputerMode = gameData.Teams.Any(t => t.IsComputer);

            wormCanvas = new Canvas { Background = Brushes.Transparent };
            boxCanvas = new Canvas { Background = Brushes.Transparent };
            projectileCanvas = new Canvas { Background = Brushes.Transparent };

            CreateWeaponInfoPanel();
            CreateStatusMessageUI();
            CreateKillMessageUI();
            CreateAimUI();
            CreateTurnTimerUI();
            AddUIElementsToGrid();

            Loaded += (s, e) => InitializeGame();
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            MouseDown += OnGameGridMouseDown;

            PreviewKeyDown += (s, e) =>
            {
                if (isAITurn || isPlayerTurn)
                {
                    if (e.Key == Key.Escape)
                    {
                        if (currentAimingState != AimingState.None || isSelectingTeleport || isSelectingAirstrike)
                        {
                            CancelAiming();
                        }
                        else
                        {
                            Close();
                            new MainWindow().Show();
                        }
                    }
                }
                if (isAITurn) return;
                else if (e.Key == Key.Space)
                {
                    if (isPlayerTurn) SkipTurn();
                }
                else if (e.Key == Key.I)
                {
                    if (currentWorm != null && currentAimingState == AimingState.None && isPlayerTurn)
                    {
                        OpenInventory();
                    }
                }
                else if (e.Key == Key.F && isPlayerTurn)
                {
                    HandleFireKey();
                }
                else if (e.Key == Key.Left || e.Key == Key.Right)
                {
                    HandleAimControl(e.Key);
                }
            };
        }


        private void ExecuteAITurn()
        {
            if (currentWorm == null || !isAITurn || currentWorm.Health <= 0)
            {
                SwitchToNextTeamWorm();
                return;
            }

            currentAIPath = null;
            aiPathIndex = 0;
            aiTargetEnemy = null;
            aiTargetBox = null;

            aiJumpCount = 0;
            aiLastPositionX = currentWorm.X;
            aiLastJumpTime = DateTime.MinValue;
            aiPathStartTime = DateTime.MinValue;

            var enemies = worms.Where(w => w.TeamIndex != currentWorm.TeamIndex && w.Health > 0).ToList();

            if (enemies.Any())
            {
                Worm closestEnemy = enemies
                    .OrderBy(e => CalculateDistance(currentWorm.GetCenter().X, currentWorm.GetCenter().Y,
                                                    e.GetCenter().X, e.GetCenter().Y))
                    .First();

                double enemyDist = CalculateDistance(currentWorm.GetCenter().X, currentWorm.GetCenter().Y,
                                                     closestEnemy.GetCenter().X, closestEnemy.GetCenter().Y);

                aiTargetEnemy = closestEnemy;

                if (enemyDist > 600.0)
                {
                    aiTargetEnemy = null;
                }
                else
                {
                    Point enemyTarget = new Point(
                        closestEnemy.X + closestEnemy.Width / 2,
                        closestEnemy.Y + closestEnemy.Height / 2 - 20
                    );

                    currentAIPath = pathfinder.FindPath(
                        currentWorm.GetCenter(),
                        enemyTarget,
                        currentWorm.Width,
                        currentWorm.Height,
                        JUMP_POWER,
                        GRAVITY
                    );

                    if (currentAIPath != null && currentAIPath.Count > 1)
                    {
                        aiPathIndex = 0;
                        aiPathStartTime = DateTime.Now;
                        return;
                    }

                    aiTargetEnemy = null;
                }
            }

            Box nearestBox = FindNearestBox(currentWorm);
            if (nearestBox != null && !nearestBox.IsOpened)
            {
                double boxDist = CalculateDistance(currentWorm.GetCenter().X, currentWorm.GetCenter().Y,
                                                   nearestBox.X + nearestBox.Width / 2, nearestBox.Y + nearestBox.Height / 2);

                if (boxDist < 500)
                {
                    aiTargetBox = nearestBox;

                    Point boxPoint = new Point(nearestBox.X + nearestBox.Width / 2, nearestBox.Y + nearestBox.Height / 2);

                    currentAIPath = pathfinder.FindPath(
                        currentWorm.GetCenter(),
                        boxPoint,
                        currentWorm.Width,
                        currentWorm.Height,
                        JUMP_POWER,
                        GRAVITY
                    );

                    if (currentAIPath != null && currentAIPath.Count > 1)
                    {
                        aiPathIndex = 0;
                        aiPathStartTime = DateTime.Now;
                        return;
                    }
                }
            }

            double randomAngle = random.NextDouble() * Math.PI * 2;
            double randomDist = 200 + random.NextDouble() * 200;
            Point randomPoint = new Point(
                currentWorm.X + Math.Cos(randomAngle) * randomDist,
                currentWorm.Y + Math.Sin(randomAngle) * randomDist
            );

            double? groundY = FindGroundBelow(randomPoint.X, randomPoint.Y);
            if (groundY.HasValue)
            {
                randomPoint = new Point(randomPoint.X, groundY.Value - currentWorm.Height);
            }

            currentAIPath = pathfinder.FindPath(
                currentWorm.GetCenter(),
                randomPoint,
                currentWorm.Width,
                currentWorm.Height,
                JUMP_POWER,
                GRAVITY
            );

            if (currentAIPath != null && currentAIPath.Count > 1)
            {
                aiPathIndex = 0;
                aiPathStartTime = DateTime.Now;
                return;
            }

            EndAITurnWithDelay();
        }

        private void MoveForward(bool moveRight)
        {
            if (currentWorm == null) return;

            double moveDistance = 35;
            double newX = currentWorm.X + (moveRight ? moveDistance : -moveDistance);

            double groundCheckX = newX + currentWorm.Width / 2;
            double groundCheckY = currentWorm.Y + currentWorm.Height + 30;

            bool hasGroundAhead = false;
            for (int i = 0; i < 50; i += 10)
            {
                if (IsGroundAtScreenPoint(groundCheckX, currentWorm.Y + currentWorm.Height + i))
                {
                    hasGroundAhead = true;
                    break;
                }
            }

            if (hasGroundAhead)
            {
                currentWorm.X = newX;
                currentWorm.UpdatePosition();
            }
            else
            {
                TryJumpOrRetreat();
            }
        }

        private void TryJumpOverObstacle()
        {
            if (currentWorm == null || currentWorm.IsJumping) return;


            double checkX = currentWorm.X + currentWorm.Width / 2;
            double checkY = currentWorm.Y + currentWorm.Height + 5;

            if (IsGroundAtScreenPoint(checkX, checkY))
            {
                currentWorm.VelocityY = -14.0;
                currentWorm.IsJumping = true;
            }
            else
            {
                MoveToSide();
            }
        }

        private void MoveToSide()
        {
            if (currentWorm == null) return;

            currentWorm.FacingRight = !currentWorm.FacingRight;

            double moveDistance = 25;
            double newX = currentWorm.X + (currentWorm.FacingRight ? moveDistance : -moveDistance);

            if (CanMoveToPosition(currentWorm, newX, currentWorm.Y))
            {
                currentWorm.X = newX;
                currentWorm.UpdatePosition();
            }
        }

        private void TryJumpOrRetreat()
        {
            if (currentWorm == null) return;

            if (!currentWorm.IsJumping)
            {
                TryJumpOverObstacle();
            }
            else
            {
                currentWorm.FacingRight = !currentWorm.FacingRight;
                MoveForward(currentWorm.FacingRight);
            }
        }



        private Box FindNearestBox(Worm worm)
        {
            if (boxes.Count == 0) return null;

            Box nearest = null;
            double minDistance = double.MaxValue;

            foreach (var box in boxes)
            {
                if (box.IsOpened) continue;

                double distance = CalculateDistance(
                    worm.GetCenter().X, worm.GetCenter().Y,
                    box.X + box.Width / 2, box.Y + box.Height / 2);

                bool hasLineOfSight = HasLineOfSightToPoint(worm.GetCenter(),
                    new Point(box.X + box.Width / 2, box.Y + box.Height / 2));

                double adjustedDistance = hasLineOfSight ? distance : distance * 1.5;

                if (adjustedDistance < minDistance && adjustedDistance < 500)
                {
                    minDistance = adjustedDistance;
                    nearest = box;
                }
            }

            return nearest;
        }

        private bool HasLineOfSightToPoint(Point from, Point to)
        {
            int steps = 20;
            for (int i = 1; i < steps; i++)
            {
                double t = (double)i / steps;
                double x = from.X + (to.X - from.X) * t;
                double y = from.Y + (to.Y - from.Y) * t;

                if (IsGroundAtScreenPoint(x, y))
                {
                    return false;
                }
            }
            return true;
        }
        private bool CanMoveToPosition(Worm worm, double x, double y)
        {
            if (worm == null) return false;

            double[] checkPointsX = { x + 5, x + worm.Width - 5, x + worm.Width / 2 };
            double[] checkPointsY = { y + 5, y + worm.Height - 5, y + worm.Height / 2 };

            foreach (double checkX in checkPointsX)
            {
                foreach (double checkY in checkPointsY)
                {
                    if (IsGroundAtScreenPoint(checkX, checkY))
                    {
                        return false;
                    }
                }
            }

            double groundCheckX = x + worm.Width / 2;
            bool hasGroundBelow = false;

            for (int i = 0; i < 50; i += 10)
            {
                if (IsGroundAtScreenPoint(groundCheckX, y + worm.Height + i))
                {
                    hasGroundBelow = true;
                    break;
                }
            }

            return hasGroundBelow;
        }

        private double CalculateDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        private void EndAITurnWithDelay()
        {
            currentAIPath = null;
            aiPathIndex = 0;
            aiTargetEnemy = null;
            aiTargetBox = null;

            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
            delayTimer.Tick += (s, ev) =>
            {
                delayTimer.Stop();
                SwitchToNextTeamWorm();
            };
            delayTimer.Start();
        }

        private void ExecuteRangedAttack(Worm enemy, double distance)
        {
            if (currentWorm == null || enemy == null || enemy.Health <= 0) return;

            WeaponType bestWeapon = SelectBestWeaponForDistance(distance);

            if (currentWorm.CurrentWeapon?.Type != bestWeapon)
            {
                currentWorm.ChangeWeapon(bestWeapon);
            }

            currentWorm.FacingRight = enemy.X > currentWorm.X;

            if (bestWeapon == WeaponType.Shotgun || bestWeapon == WeaponType.BaseballBat)
            {
                currentAimingState = AimingState.Aiming;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();

                    if (currentWorm == null || enemy.Health <= 0 || !isAITurn)
                    {
                        EndAITurnWithDelay();
                        return;
                    }


                    if (bestWeapon == WeaponType.Shotgun)
                        FireShotgun();
                    else if (bestWeapon == WeaponType.BaseballBat)
                        UseBaseballBat();

                    currentWorm.UseCurrentWeapon();

                    CancelAiming();
                    UpdateWeaponInfo();
                    EndAITurnWithDelay();
                };
                timer.Start();
                return;
            }

            Point shooter = currentWorm.GetCenter();
            Point target = enemy.GetCenter();

            bool goodAim = CalculateAimForTarget(shooter, target, out aimAngle, out powerLevel);
            if (!goodAim)
            {
                CalculateSimpleAim(shooter, target, out aimAngle, out powerLevel);
            }

            aimAngle += (random.NextDouble() * 8.0 - 4.0);
            powerLevel += (random.NextDouble() * 12.0 - 6.0);

            aimAngle = Math.Max(15.0, Math.Min(75.0, aimAngle));
            powerLevel = Math.Max(40.0, Math.Min(95.0, powerLevel));
            currentAimingState = AimingState.Aiming;

            var aimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.6) };
            aimTimer.Tick += (s, e) =>
            {
                aimTimer.Stop();

                if (currentWorm == null || enemy == null || enemy.Health <= 0 || !isAITurn)
                {
                    EndAITurnWithDelay();
                    return;
                }

                bool wasFacingRight = currentWorm.FacingRight;

                switch (currentWorm.CurrentWeapon.Type)
                {
                    case WeaponType.Bazooka:
                        FireBazooka();
                        break;
                    case WeaponType.Grenade:
                        FireGrenade();
                        break;
                    case WeaponType.Dynamite:
                        FireDynamite();
                        break;
                    case WeaponType.Sheep:
                        FireSheep();
                        break;
                }

                currentWorm.FacingRight = wasFacingRight;
                currentWorm.UseCurrentWeapon();

                CancelAiming();
                UpdateWeaponInfo();
                EndAITurnWithDelay();
            };
            aimTimer.Start();
        }

        private WeaponType SelectBestWeaponForDistance(double distance)
        {
            var availableWeapons = currentWorm.Inventory
                .Where(w => w.IsAvailable)
                .Select(w => w.Type)
                .ToList();

            if (availableWeapons.Count == 0) return WeaponType.Shotgun;

            if (distance < 80 && availableWeapons.Contains(WeaponType.BaseballBat))
                return WeaponType.BaseballBat;

            if (distance < 150 && availableWeapons.Contains(WeaponType.Shotgun))
                return WeaponType.Shotgun;

            if (distance < 250 && availableWeapons.Contains(WeaponType.Grenade))
                return WeaponType.Grenade;

            if (distance < 300 && availableWeapons.Contains(WeaponType.Bazooka))
                return WeaponType.Bazooka;

            if (availableWeapons.Contains(WeaponType.Bazooka))
                return WeaponType.Bazooka;

            return availableWeapons.First();
        }


        private void AIMove_Tick(object sender, EventArgs e)
        {
            if (currentWorm == null || aiTargetWorm == null || !isAITurn)
            {
                aiMoveTimer.Stop();
                return;
            }

            double dist = Math.Abs(aiTargetWorm.X - currentWorm.X);
            double attackRange = 300;
            if (currentWorm.CurrentWeapon.Type == WeaponType.BaseballBat) attackRange = 50;
            if (currentWorm.CurrentWeapon.Type == WeaponType.Shotgun) attackRange = 150;

            aiMoveTimeout += 0.016;
            if (dist <= attackRange || aiMoveTimeout > 4.0)
            {
                aiMoveTimer.Stop();
                currentAimingState = AimingState.Aiming;

                aimAngle = dist > 200 ? 45 : 10;
                powerLevel = Math.Min(100, (dist / 600.0) * 100 + 20);

                var fireDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
                fireDelay.Tick += (s, ev) => {
                    fireDelay.Stop();
                    FireWeapon();
                };
                fireDelay.Start();
                return;
            }

            double direction = aiTargetWorm.X > currentWorm.X ? 1 : -1;
            double newX = currentWorm.X + direction * 2.0;

            currentWorm.FacingRight = direction > 0;
            currentWorm.UpdateSpriteAndDirection();

            bool wallAhead = IsGroundAtScreenPoint(newX + (direction > 0 ? 20 : -20), currentWorm.Y);
            if (wallAhead && !currentWorm.IsJumping)
            {
                currentWorm.VelocityY = -12.0;
                currentWorm.IsJumping = true;
            }

            if (!wallAhead)
            {
                currentWorm.X = newX;
            }
        }

        private void AddUIElementsToGrid()
        {
            if (!GameGrid.Children.Contains(boxCanvas))
                GameGrid.Children.Add(boxCanvas);
            if (!GameGrid.Children.Contains(wormCanvas))
                GameGrid.Children.Add(wormCanvas);
            if (!GameGrid.Children.Contains(projectileCanvas))
                GameGrid.Children.Add(projectileCanvas);
            if (weaponInfoPanel != null && !GameGrid.Children.Contains(weaponInfoPanel))
                GameGrid.Children.Add(weaponInfoPanel);
            if (aimLine != null && !GameGrid.Children.Contains(aimLine))
                GameGrid.Children.Add(aimLine);
            if (powerBar != null && !GameGrid.Children.Contains(powerBar))
                GameGrid.Children.Add(powerBar);
            if (powerText != null && !GameGrid.Children.Contains(powerText))
                GameGrid.Children.Add(powerText);
            if (statusMessageText != null && !GameGrid.Children.Contains(statusMessageText))
                GameGrid.Children.Add(statusMessageText);
            if (killMessageText != null && !GameGrid.Children.Contains(killMessageText))
                GameGrid.Children.Add(killMessageText);
            if (turnTimerPanel != null && !GameGrid.Children.Contains(turnTimerPanel))
                GameGrid.Children.Add(turnTimerPanel);
        }

        private void CreateWeaponInfoPanel()
        {
            weaponInfoPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 20, 0),
                Visibility = Visibility.Collapsed
            };

            var stackPanel = new StackPanel();
            weaponInfoText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Text = "No weapon selected"
            };

            stackPanel.Children.Add(weaponInfoText);
            weaponInfoPanel.Child = stackPanel;
        }

        private void CreateStatusMessageUI()
        {
            statusMessageText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(15, 10, 15, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 50, 0, 0),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void CreateKillMessageUI()
        {
            killMessageText = new TextBlock
            {
                Foreground = Brushes.Red,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(15, 10, 15, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 100, 0, 0),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void ShowStatusMessage(string message, Color color, int durationMs = 2000)
        {
            if (statusMessageText == null) return;

            if (statusMessageTimer != null)
            {
                statusMessageTimer.Stop();
                statusMessageTimer = null;
            }

            statusMessageText.Text = message;
            statusMessageText.Foreground = new SolidColorBrush(color);
            statusMessageText.Visibility = Visibility.Visible;

            statusMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };

            statusMessageTimer.Tick += (s, e) =>
            {
                statusMessageText.Visibility = Visibility.Collapsed;
                statusMessageTimer.Stop();
                statusMessageTimer = null;
            };
            statusMessageTimer.Start();
        }

        private void ShowKillMessage(string message, Color teamColor)
        {
            killMessageQueue.Enqueue(new Tuple<string, Color>(message, teamColor));

            if (!isShowingKillMessage)
            {
                ShowNextKillMessage();
            }
        }

        private void ShowNextKillMessage()
        {
            if (killMessageQueue.Count == 0)
            {
                isShowingKillMessage = false;
                return;
            }

            isShowingKillMessage = true;
            var (message, color) = killMessageQueue.Dequeue();

            killMessageText.Text = message;
            killMessageText.Foreground = new SolidColorBrush(color);

            killMessageText.Visibility = Visibility.Visible;

            DoubleAnimation fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            killMessageText.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            if (killMessageTimer != null)
            {
                killMessageTimer.Stop();
            }

            killMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            killMessageTimer.Tick += (s, e) =>
            {
                DoubleAnimation fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.5)
                };

                fadeOut.Completed += (sender2, e2) =>
                {
                    killMessageText.Visibility = Visibility.Collapsed;
                    killMessageTimer.Stop();
                    killMessageTimer = null;
                    ShowNextKillMessage();
                };

                killMessageText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };

            killMessageTimer.Start();
        }

        private void UpdateWeaponInfo()
        {
            if (currentWorm != null && currentWorm.CurrentWeapon != null)
            {
                var weapon = currentWorm.CurrentWeapon;
                weaponInfoText.Text = $"{weapon.Name}\nAmmo: {(weapon.Ammo == -1 ? "∞" : weapon.Ammo.ToString())}\nDamage: {weapon.Damage}";

                if (weapon.Type == WeaponType.Teleport && isSelectingTeleport)
                {
                    weaponInfoText.Text += "\n[Click to select teleport point]";
                }
                else if (weapon.Type == WeaponType.Airstrike && isSelectingAirstrike)
                {
                    weaponInfoText.Text += "\n[Click to select airstrike point]";
                }
                else if (weapon.Type == WeaponType.Parachute && currentWorm.ParachuteDeployed)
                {
                    weaponInfoText.Text += "\n[Parachute active]";
                }

                weaponInfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                weaponInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OpenInventory()
        {
            if (isAITurn) return;

            try
            {
                var inventoryWindow = new InventoryWindow(currentWorm)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false
                };

                inventoryWindow.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.I)
                    {
                        inventoryWindow.Close();
                        this.Focus();
                    }
                };

                inventoryWindow.Closed += (s, e) =>
                {
                    UpdateWeaponInfo();
                    this.Focus();
                };

                inventoryWindow.ShowDialog();
            }
            catch (Exception ex)
            {
            }
        }

        private void HandleFireKey()
        {
            if (isAITurn) return;
            if (currentWorm == null) return;

            if (currentWorm.CurrentWeapon.RequiresAiming)
            {
                switch (currentAimingState)
                {
                    case AimingState.None:
                        StartAiming();
                        break;
                    case AimingState.Aiming:
                        StartSelectingPower();
                        break;
                    case AimingState.SelectingPower:
                        FireWeapon();
                        break;
                }
            }
            else
            {
                FireWeaponWithoutAiming();
            }
        }

        private void FireWeaponWithoutAiming()
        {
            if (currentWorm == null || currentWorm.CurrentWeapon == null)
                return;

            switch (currentWorm.CurrentWeapon.Type)
            {
                case WeaponType.BaseballBat:
                    UseBaseballBat();
                    currentWorm.UseCurrentWeapon();
                    SwitchToNextTeamWorm();
                    break;
                case WeaponType.Teleport:
                    if (!isSelectingTeleport)
                    {
                        StartTeleportSelection();
                    }
                    break;
                case WeaponType.Parachute:
                    UseParachute();
                    currentWorm.UseCurrentWeapon();
                    UpdateWeaponInfo();
                    SwitchToNextTeamWorm();
                    break;
                case WeaponType.Airstrike:
                    if (!isSelectingAirstrike)
                    {
                        StartAirstrikeSelection();
                    }
                    break;
                default:
                    break;
            }
        }

        private void ExecuteTeleport()
        {
            if (!isSelectingTeleport || currentWorm == null) return;

            if (IsGroundAtScreenPoint(teleportTarget.X, teleportTarget.Y))
            {
                return;
            }

            currentWorm.X = teleportTarget.X - currentWorm.Width / 2;
            currentWorm.Y = teleportTarget.Y - currentWorm.Height / 2;
            currentWorm.UpdatePosition();
            CleanupTeleportSelection();
            currentWorm.UseCurrentWeapon();
            UpdateWeaponInfo();
            SwitchToNextTeamWorm();
        }

        private void StartAiming()
        {
            if (isAITurn) return;
            if (currentWorm.IsJumping)
            {
                return;
            }

            currentAimingState = AimingState.Aiming;
            aimAngle = 45.0;
            powerLevel = 0.0;
            isPowerIncreasing = true;
            aimLine.Visibility = Visibility.Visible;
            powerBar.Visibility = Visibility.Collapsed;
            powerText.Visibility = Visibility.Collapsed;
        }

        private void StartSelectingPower()
        {
            if (isAITurn) return;
            currentAimingState = AimingState.SelectingPower;
            powerBar.Visibility = Visibility.Visible;
            powerText.Visibility = Visibility.Visible;
        }
        private void FireWeapon()
        {
            if (currentWorm == null || currentWorm.CurrentWeapon == null)
                return;

            switch (currentWorm.CurrentWeapon.Type)
            {
                case WeaponType.Shotgun:
                    FireShotgun();
                    break;
                case WeaponType.Grenade:
                    FireGrenade();
                    break;
                case WeaponType.Dynamite:
                    FireDynamite();
                    break;
                case WeaponType.Sheep:
                    FireSheep();
                    break;
                case WeaponType.Bazooka:
                    FireBazooka();
                    break;
                default:
                    break;
            }

            currentWorm.UseCurrentWeapon();
            CancelAiming();
            UpdateWeaponInfo();
            if (!isAITurn)
            {
                SwitchToNextTeamWorm();
            }
            else
            {
                EndAITurnAfterAttack();
            }
        }

        private void EndAITurnAfterAttack()
        {
            var delayTimer = new DispatcherTimer();
            delayTimer.Interval = TimeSpan.FromSeconds(0.5);
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                SwitchToNextTeamWorm();
            };
            delayTimer.Start();
        }

        private void FireBazooka()
        {
            if (currentWorm == null) return;

            double angleRad = aimAngle * Math.PI / 180.0;
            double speed = 15.0 + (powerLevel / MAX_POWER) * 30.0;
            double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
            double velocityX = Math.Cos(angleRad) * speed * directionMultiplier;
            double velocityY = -Math.Sin(angleRad) * speed;

            CreateProjectile(
                currentWorm.X + currentWorm.Width / 2,
                currentWorm.Y + currentWorm.Height / 2,
                velocityX, velocityY,
                currentWorm.CurrentWeapon
            );
        }

        private void CancelAiming()
        {
            currentAimingState = AimingState.None;
            aimLine.Visibility = Visibility.Collapsed;
            powerBar.Visibility = Visibility.Collapsed;
            powerText.Visibility = Visibility.Collapsed;

            if (isSelectingTeleport)
            {
                CleanupTeleportSelection();
                ShowStatusMessage("Teleport cancelled", Colors.Blue, 1500);
            }

            if (isSelectingAirstrike)
            {
                CleanupAirstrikeSelection();
                ShowStatusMessage("Airstrike cancelled", Colors.Red, 1500);
            }
        }

        private void HandleAimControl(Key key)
        {
            if (isAITurn) return;

            if (currentAimingState == AimingState.Aiming || currentAimingState == AimingState.SelectingPower)
            {
                if (key == Key.Left)
                {
                    aimAngle = Math.Max(0, aimAngle - 5);
                }
                else if (key == Key.Right)
                {
                    aimAngle = Math.Min(90, aimAngle + 5);
                }
                UpdateAimLine();
            }
        }

        private void UpdateAimLine()
        {
            if (currentWorm == null) return;

            double angleRad = aimAngle * Math.PI / 180.0;
            double startX = currentWorm.X + currentWorm.Width / 2;
            double startY = currentWorm.Y + currentWorm.Height / 2;
            double length = AIM_LINE_LENGTH;
            double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
            double endX = startX + Math.Cos(angleRad) * length * directionMultiplier;
            double endY = startY - Math.Sin(angleRad) * length;

            aimLine.X1 = startX;
            aimLine.Y1 = startY;
            aimLine.X2 = endX;
            aimLine.Y2 = endY;
        }
        private void FireShotgun()
        {
            if (currentWorm == null) return;

            int pelletCount = 8;
            double spreadAngle = 15.0;

            for (int i = 0; i < pelletCount; i++)
            {
                double pelletAngle = aimAngle + (random.NextDouble() * 2 - 1) * spreadAngle;
                pelletAngle = Math.Max(0, Math.Min(90, pelletAngle));
                double angleRad = pelletAngle * Math.PI / 180.0;
                double speed = 5.0 + (powerLevel / MAX_POWER) * 20.0;
                double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
                double velocityX = Math.Cos(angleRad) * speed * directionMultiplier;
                double velocityY = -Math.Sin(angleRad) * speed;

                var shotgunProjectile = new Projectile(
                    currentWorm.X + currentWorm.Width / 2,
                    currentWorm.Y + currentWorm.Height / 2,
                    velocityX, velocityY,
                    currentWorm.CurrentWeapon.Damage / pelletCount,
                    WeaponType.Shotgun,
                    5,
                    5,
                    0, this,
                    Colors.Gray, currentWorm.CurrentWeapon.ProjectileSprite,
                    currentWorm.Id
                );

                projectiles.Add(shotgunProjectile);
                shotgunProjectile.AddToCanvas(projectileCanvas);
            }
        }

        private void FireGrenade()
        {
            if (currentWorm == null) return;

            double angleRad = aimAngle * Math.PI / 180.0;
            double speed = 10.0 + (powerLevel / MAX_POWER) * 25.0;
            double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
            double velocityX = Math.Cos(angleRad) * speed * directionMultiplier;
            double velocityY = -Math.Sin(angleRad) * speed;

            var grenade = CreateProjectile(
                currentWorm.X + currentWorm.Width / 2,
                currentWorm.Y + currentWorm.Height / 2,
                velocityX, velocityY,
                currentWorm.CurrentWeapon
            );

            grenade.Radius = 12;
        }

        private void FireDynamite()
        {
            if (currentWorm == null) return;

            double angleRad = aimAngle * Math.PI / 180.0;
            double speed = 8.0 + (powerLevel / MAX_POWER) * 20.0;
            double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
            double velocityX = Math.Cos(angleRad) * speed * directionMultiplier;
            double velocityY = -Math.Sin(angleRad) * speed;

            var dynamite = CreateProjectile(
                currentWorm.X + currentWorm.Width / 2,
                currentWorm.Y + currentWorm.Height / 2,
                velocityX, velocityY,
                currentWorm.CurrentWeapon
            );

            dynamite.Radius = 12;
        }

        private void FireSheep()
        {
            if (currentWorm == null) return;

            double angleRad = aimAngle * Math.PI / 180.0;
            double speed = 12.0 + (powerLevel / MAX_POWER) * 22.0;
            double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
            double velocityX = Math.Cos(angleRad) * speed * directionMultiplier;
            double velocityY = -Math.Sin(angleRad) * speed;

            var sheep = CreateProjectile(
                currentWorm.X + currentWorm.Width / 2,
                currentWorm.Y + currentWorm.Height / 2,
                velocityX, velocityY,
                currentWorm.CurrentWeapon
            );

            sheep.Radius = 12;
        }

        private void UseBaseballBat()
        {
            if (currentWorm == null) return;

            double directionMultiplier = currentWorm.FacingRight ? 1 : -1;
            double batX = currentWorm.X + (currentWorm.FacingRight ? currentWorm.Width : -50);
            double batY = currentWorm.Y + currentWorm.Height / 2 - 25;
            Rect batArea = new Rect(batX, batY, 50, 50);

            var wormsCopy = new List<Worm>(worms);
            foreach (var worm in wormsCopy)
            {
                if (worm == currentWorm) continue;
                Rect wormBounds = new Rect(worm.X, worm.Y, worm.Width, worm.Height);
                if (batArea.IntersectsWith(wormBounds))
                {
                    worm.TakeDamage(currentWorm.CurrentWeapon.Damage);
                    double knockbackX = directionMultiplier * currentWorm.CurrentWeapon.KnockbackPower;
                    worm.X += knockbackX;
                    worm.VelocityY = -5;
                }
            }
        }

        private void UseParachute()
        {
            if (currentWorm == null) return;
            currentWorm.IsUsingParachute = true;
            currentWorm.ParachuteDeployed = true;
            currentWorm.UpdateSpriteAndDirection();
        }

        private void StartTeleportSelection()
        {
            if (isAITurn) return;
            if (currentWorm == null) return;

            isSelectingTeleport = true;
            ShowStatusMessage("Click to Teleport", Colors.Blue, 2000);
        }

        private void StartAirstrikeSelection()
        {
            if (isAITurn) return;
            if (currentWorm == null) return;

            isSelectingAirstrike = true;

            ShowStatusMessage("Click for Airstrike", Colors.Red, 2000);
        }

        private void CleanupMarkers()
        {
            isSelectingTeleport = false;
            isSelectingAirstrike = false;

            if (teleportMarker != null) teleportMarker.Visibility = Visibility.Collapsed;
            if (airstrikeMarker != null) airstrikeMarker.Visibility = Visibility.Collapsed;

            if (aimLine != null) aimLine.Visibility = Visibility.Collapsed;
            if (powerBar != null) powerBar.Visibility = Visibility.Collapsed;
        }

        private void OnGameGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isAITurn) return;

            if (isSelectingTeleport)
            {
                Point mousePos = e.GetPosition(GameGrid);
                teleportTarget = mousePos;
                ExecuteTeleport();
            }
            else if (isSelectingAirstrike)
            {
                Point mousePos = e.GetPosition(GameGrid);
                airstrikeTarget = mousePos;
                ExecuteAirstrike();
            }
        }

        private void CleanupTeleportSelection()
        {
            isSelectingTeleport = false;
        }

        private void ExecuteAirstrike()
        {
            if (!isSelectingAirstrike || currentWorm == null) return;
            LaunchAirstrike(airstrikeTarget.X, airstrikeTarget.Y);
            CleanupAirstrikeSelection();
            currentWorm.UseCurrentWeapon();
            UpdateWeaponInfo();
            SwitchToNextTeamWorm();
        }

        private void LaunchAirstrike(double targetX, double targetY)
        {
            CreateProjectile(
                targetX,
                0,
                0,
                30,
                currentWorm.CurrentWeapon
            );
        }

        private void CleanupAirstrikeSelection()
        {
            isSelectingAirstrike = false;
            if (airstrikeMarker != null && GameGrid.Children.Contains(airstrikeMarker))
            {
                GameGrid.Children.Remove(airstrikeMarker);
                airstrikeMarker = null;
            }
        }

        private void SkipTurn()
        {
            if (isAITurn) return;
            if (currentWorm != null)
            {
                StopTurnTimer();
                ShowStatusMessage($"Turn skipped for {currentWorm.Name}", Colors.White, 1500);
                SwitchToNextTeamWorm();
            }
        }

        private Color GetTeamColor(int teamIndex)
        {
            if (teamIndex >= 0 && teamIndex < gameData.Teams.Count)
            {
                return gameData.Teams[teamIndex].Color;
            }
            return Colors.White;
        }

        private void CreateAimUI()
        {
            try
            {
                aimLine = new Line
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Visibility = Visibility.Collapsed,
                    Opacity = 0.8
                };

                powerBar = new ProgressBar
                {
                    Width = 200,
                    Height = 20,
                    Minimum = 0,
                    Maximum = MAX_POWER,
                    Value = 0,
                    Foreground = Brushes.Red,
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.White,
                    Visibility = Visibility.Collapsed,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 100)
                };

                powerText = new TextBlock
                {
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(10, 5, 10, 5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 130),
                    Visibility = Visibility.Collapsed,
                    Text = "Power: 0%"
                };

            }
            catch (Exception ex)
            {
                aimLine = new Line();
                powerBar = new ProgressBar();
                powerText = new TextBlock();
            }
        }

        private void LoadTerrain(string mapFile, string textureFile)
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string mapPath = Path.Combine(basePath, "Sprites", "Maps", mapFile);
                string texturePath = Path.Combine(basePath, "Sprites", "Textures", textureFile);

                BitmapImage maskImage = new BitmapImage(new Uri(mapPath, UriKind.Absolute));
                WriteableBitmap collisionMask = new WriteableBitmap(maskImage);
                collisionMaskObj = new CollisionMask(collisionMask);

                BitmapImage texImage = new BitmapImage(new Uri(texturePath, UriKind.Absolute));
                terrainBitmap = CreateTexturedMap(collisionMask, texImage);
                terrainMap = new TerrainMap(terrainBitmap);

                terrainWidth = terrainBitmap.PixelWidth;
                terrainHeight = terrainBitmap.PixelHeight;

                Image mapImage = new Image { Source = terrainBitmap, Stretch = Stretch.None };
                Viewbox viewbox = new Viewbox { Stretch = Stretch.Fill, Child = mapImage };

                for (int i = GameGrid.Children.Count - 1; i >= 0; i--)
                {
                    if (GameGrid.Children[i] is Viewbox)
                    {
                        GameGrid.Children.RemoveAt(i);
                        break;
                    }
                }

                GameGrid.Children.Insert(0, viewbox);
                scaleX = terrainWidth / GameGrid.ActualWidth;
                scaleY = terrainHeight / GameGrid.ActualHeight;
                Title = $"Worms - {Path.GetFileNameWithoutExtension(mapFile)}";
            }
            catch (Exception ex)
            {
            }
        }

        public void HandleExplosion(Projectile projectile)
        {
            if (projectile.WeaponType != WeaponType.Shotgun)
            {
                DestroyTerrainAt(projectile.X, projectile.Y, projectile.ExplosionRadius);
            }
            ApplyExplosionEffects(projectile);
        }

        public void DestroyTerrainAt(double screenX, double screenY, double radius)
        {
            if (collisionMaskObj == null || terrainMap == null) return;

            int centerX = (int)(screenX * scaleX);
            int centerY = (int)(screenY * scaleY);
            int mapRadius = (int)(radius * scaleX);

            collisionMaskObj.DestroyCircularArea(centerX, centerY, mapRadius);
            terrainMap.DestroyCircularArea(centerX, centerY, mapRadius);
        }

        public void ApplyExplosionEffects(Projectile projectile)
        {
            Point explosionCenter = projectile.GetCenter();
            double radius = projectile.ExplosionRadius;

            int shooterWormId = projectile.ShooterWormId;

            var wormsCopy = new List<Worm>(worms);

            if (projectile.WeaponType == WeaponType.Shotgun)
            {
                foreach (var worm in wormsCopy)
                {
                    if (shooterWormId != -1 && worm.Id == shooterWormId)
                        continue;

                    Point wormCenter = worm.GetCenter();
                    double distance = Math.Sqrt(
                        Math.Pow(wormCenter.X - explosionCenter.X, 2) +
                        Math.Pow(wormCenter.Y - explosionCenter.Y, 2)
                    );

                    if (distance <= radius)
                    {
                        double damageMultiplier = 1.0 - (distance / radius);
                        int baseDamage = (int)(projectile.Damage * damageMultiplier);

                        if (distance < radius * 0.3)
                        {
                            baseDamage = (int)(baseDamage * 1.5);
                        }

                        if (baseDamage > 0)
                        {
                            worm.TakeDamage(baseDamage);
                        }

                        Vector direction = new Vector(
                            wormCenter.X - explosionCenter.X,
                            wormCenter.Y - explosionCenter.Y
                        );

                        if (direction.Length > 0)
                        {
                            direction.Normalize();
                            double knockback = 8 * damageMultiplier;
                            ApplyKnockbackWithCollisionCheck(worm, direction, knockback);
                        }
                    }
                }
            }
            else
            {
                foreach (var worm in wormsCopy)
                {
                    if (shooterWormId != -1 && worm.Id == shooterWormId)
                        continue;

                    Point wormCenter = worm.GetCenter();
                    double distance = Math.Sqrt(
                        Math.Pow(wormCenter.X - explosionCenter.X, 2) +
                        Math.Pow(wormCenter.Y - explosionCenter.Y, 2)
                    );

                    if (distance <= radius)
                    {
                        double damageMultiplier = 1.0 - (distance / radius);
                        int damage = (int)(projectile.Damage * damageMultiplier);

                        if (damage > 0)
                        {
                            worm.TakeDamage(damage);
                        }

                        Vector direction = new Vector(
                            wormCenter.X - explosionCenter.X,
                            wormCenter.Y - explosionCenter.Y
                        );

                        if (direction.Length > 0)
                        {
                            direction.Normalize();
                            double knockback = projectile.KnockbackPower * (1.0 - distance / radius);
                            ApplyKnockbackWithCollisionCheck(worm, direction, knockback);
                        }
                    }
                }
            }
        }

        private void ApplyKnockbackWithCollisionCheck(Worm worm, Vector direction, double knockback)
        {
            double newX = worm.X + direction.X * knockback;
            double newY = worm.Y + direction.Y * knockback;

            bool canMoveToPosition = CanWormMoveToPosition(worm, newX, newY);
            if (canMoveToPosition)
            {
                worm.X = newX;
                worm.Y = newY;
            }
            else
            {
                TryAdjustKnockbackPosition(worm, direction, knockback);
            }
            worm.UpdatePosition();
        }

        private bool CanWormMoveToPosition(Worm worm, double x, double y)
        {
            if (worm == null) return false;

            double[] checkPointsX = { x + 5, x + worm.Width - 5, x + worm.Width / 2 };
            double[] checkPointsY = { y + 5, y + worm.Height - 5, y + worm.Height / 2 };

            foreach (double checkX in checkPointsX)
            {
                foreach (double checkY in checkPointsY)
                {
                    if (IsGroundAtScreenPoint(checkX, checkY))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void TryAdjustKnockbackPosition(Worm worm, Vector direction, double knockback)
        {
            for (double reducedKnockback = knockback * 0.8; reducedKnockback > 0; reducedKnockback -= 5)
            {
                double newX = worm.X + direction.X * reducedKnockback;
                double newY = worm.Y + direction.Y * reducedKnockback;

                if (CanWormMoveToPosition(worm, newX, newY))
                {
                    worm.X = newX;
                    worm.Y = newY;
                    return;
                }
            }
        }

        private WriteableBitmap CreateTexturedMap(WriteableBitmap mask, BitmapImage texture)
        {
            int width = mask.PixelWidth;
            int height = mask.PixelHeight;
            WriteableBitmap result = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            byte[] maskPixels = new byte[width * height * 4];
            mask.CopyPixels(maskPixels, width * 4, 0);

            WriteableBitmap tex = new WriteableBitmap(texture);
            int texW = tex.PixelWidth;
            int texH = tex.PixelHeight;
            byte[] texPixels = new byte[texW * texH * 4];
            tex.CopyPixels(texPixels, texW * 4, 0);

            byte[] resultPixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width + x) * 4;
                    bool isGround = maskPixels[idx + 2] > 200 && maskPixels[idx + 1] > 200 && maskPixels[idx] > 200;

                    if (isGround)
                    {
                        int tx = x % texW;
                        int ty = y % texH;
                        int texIdx = (ty * texW + tx) * 4;
                        resultPixels[idx] = texPixels[texIdx];
                        resultPixels[idx + 1] = texPixels[texIdx + 1];
                        resultPixels[idx + 2] = texPixels[texIdx + 2];
                        resultPixels[idx + 3] = 255;
                    }
                    else
                    {
                        resultPixels[idx] = 235;
                        resultPixels[idx + 1] = 206;
                        resultPixels[idx + 2] = 135;
                        resultPixels[idx + 3] = 255;
                    }
                }
            }

            result.WritePixels(new Int32Rect(0, 0, width, height), resultPixels, width * 4, 0);
            return result;
        }

        private void SpawnWorms()
        {
            wormCanvas.Children.Clear();
            worms.Clear();
            if (collisionMaskObj == null) return;

            var spawnZones = FindSpawnZones();

            var teamToZoneMap = new Dictionary<int, SpawnZone>();
            spawnZones = spawnZones.OrderBy(z => z.MinX).ToList();

            for (int i = 0; i < gameData.Teams.Count; i++)
            {
                if (i < spawnZones.Count)
                {
                    teamToZoneMap[i] = spawnZones[i];
                }
            }

            for (int teamIndex = 0; teamIndex < gameData.Teams.Count; teamIndex++)
            {
                if (!teamToZoneMap.ContainsKey(teamIndex))
                {
                    teamToZoneMap[teamIndex] = spawnZones.FirstOrDefault();
                }

                var zone = teamToZoneMap[teamIndex];
                var team = gameData.Teams[teamIndex];

                var spawnPositions = CreateSpawnPositionsInZone(zone, team.WormNames.Count);

                for (int wormIndex = 0; wormIndex < team.WormNames.Count; wormIndex++)
                {
                    Point position;
                    if (wormIndex < spawnPositions.Count)
                    {
                        position = spawnPositions[wormIndex];
                    }
                    else
                    {
                        int centerX = (zone.MinX + zone.MaxX) / 2;
                        int centerY = (zone.MinY + zone.MaxY) / 2;
                        position = new Point(centerX, centerY);
                    }

                    var worm = SpawnWormAtPosition(team.WormNames[wormIndex], position, team.Color, teamIndex);
                    if (worm != null)
                    {
                        worm.InitializeWeapons();
                        worm.OnDeath += () => KillWorm(worm, "died");
                    }
                }
            }
        }

        private List<Point> CreateSpawnPositionsInZone(SpawnZone zone, int wormCount)
        {
            var positions = new List<Point>();
            var spawnPixels = new List<Point>();

            for (int x = zone.MinX; x <= zone.MaxX; x++)
            {
                for (int y = zone.MinY; y <= zone.MaxY; y++)
                {
                    if (collisionMaskObj.IsSpawnArea(x, y))
                    {
                        spawnPixels.Add(new Point(x, y));
                    }
                }
            }

            if (spawnPixels.Count == 0)
            {
                for (int x = zone.MinX; x <= zone.MaxX; x++)
                {
                    for (int y = zone.MinY; y <= zone.MaxY; y++)
                    {
                        if (collisionMaskObj.IsGround(x, y))
                        {
                            if (!collisionMaskObj.IsGround(x, y - 1))
                            {
                                spawnPixels.Add(new Point(x, y));
                            }
                        }
                    }
                }
            }

            if (spawnPixels.Count == 0) return positions;

            spawnPixels = spawnPixels.OrderBy(p => p.Y).ToList();

            var verticalGroups = new List<List<Point>>();
            var currentGroup = new List<Point>();
            double lastY = spawnPixels[0].Y;

            foreach (var pixel in spawnPixels)
            {
                if (Math.Abs(pixel.Y - lastY) > 5)
                {
                    if (currentGroup.Count > 0)
                    {
                        verticalGroups.Add(currentGroup);
                        currentGroup = new List<Point>();
                    }
                }
                currentGroup.Add(pixel);
                lastY = pixel.Y;
            }
            if (currentGroup.Count > 0)
            {
                verticalGroups.Add(currentGroup);
            }

            var selectedPositions = new List<Point>();
            Random rand = new Random();

            verticalGroups = verticalGroups.OrderBy(g => rand.Next()).ToList();

            foreach (var group in verticalGroups)
            {
                if (selectedPositions.Count >= wormCount) break;

                double avgX = group.Average(p => p.X);
                double avgY = group.Average(p => p.Y);
                var candidate = new Point(avgX, avgY);

                int? groundY = collisionMaskObj?.FindSurfaceBelow((int)avgX, (int)avgY, 100);
                if (groundY.HasValue)
                {
                    var finalPos = new Point(avgX, groundY.Value);

                    bool tooClose = false;
                    foreach (var existingPos in selectedPositions)
                    {
                        double distance = Math.Sqrt(
                            Math.Pow(existingPos.X - finalPos.X, 2) +
                            Math.Pow(existingPos.Y - finalPos.Y, 2)
                        );

                        if (distance < 50)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        selectedPositions.Add(finalPos);
                    }
                }
            }

            if (selectedPositions.Count < wormCount)
            {
                int attempts = 0;
                int maxAttempts = spawnPixels.Count * 3;

                while (selectedPositions.Count < wormCount && attempts < maxAttempts)
                {
                    attempts++;

                    int randomIndex = rand.Next(spawnPixels.Count);
                    var pixel = spawnPixels[randomIndex];

                    int? groundY = collisionMaskObj?.FindSurfaceBelow((int)pixel.X, (int)pixel.Y, 100);
                    if (!groundY.HasValue) continue;

                    var candidate = new Point(pixel.X, groundY.Value);

                    bool tooClose = false;
                    foreach (var existingPos in selectedPositions)
                    {
                        double distance = Math.Sqrt(
                            Math.Pow(existingPos.X - candidate.X, 2) +
                            Math.Pow(existingPos.Y - candidate.Y, 2)
                        );

                        if (distance < 30)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        selectedPositions.Add(candidate);
                    }
                }
            }

            return selectedPositions.Take(wormCount).ToList();
        }

        private Worm SpawnWormAtPosition(string wormName, Point mapPosition, Color color, int teamIndex)
        {
            int mapX = (int)mapPosition.X;
            int mapY = (int)mapPosition.Y;
            mapX = Math.Max(0, Math.Min(mapX, terrainWidth - 1));
            mapY = Math.Max(0, Math.Min(mapY, terrainHeight - 1));

            int? groundY = collisionMaskObj?.FindSurfaceBelow(mapX, mapY, 100);
            if (!groundY.HasValue) groundY = mapY;

            double screenX = mapX / scaleX;
            double screenY = groundY.Value / scaleY - 40;

            var worm = new Worm(wormName, screenX, screenY, color, teamIndex);
            worms.Add(worm);
            worm.AddToCanvas(wormCanvas);
            worm.UpdatePosition();
            return worm;
        }

        private List<SpawnZone> FindSpawnZones()
        {
            var zones = new List<SpawnZone>();
            if (collisionMaskObj == null) return zones;

            bool[,] visited = new bool[terrainWidth, terrainHeight];

            for (int x = 0; x < terrainWidth; x++)
            {
                for (int y = 0; y < terrainHeight; y++)
                {
                    if (!visited[x, y] && collisionMaskObj.IsSpawnArea(x, y))
                    {
                        var zone = new SpawnZone
                        {
                            MinX = int.MaxValue,
                            MaxX = int.MinValue,
                            MinY = int.MaxValue,
                            MaxY = int.MinValue
                        };

                        Queue<Point> queue = new Queue<Point>();
                        queue.Enqueue(new Point(x, y));
                        visited[x, y] = true;
                        int pixelCount = 0;

                        while (queue.Count > 0)
                        {
                            var point = queue.Dequeue();
                            int px = (int)point.X;
                            int py = (int)point.Y;
                            pixelCount++;

                            zone.MinX = Math.Min(zone.MinX, px);
                            zone.MaxX = Math.Max(zone.MaxX, px);
                            zone.MinY = Math.Min(zone.MinY, py);
                            zone.MaxY = Math.Max(zone.MaxY, py);

                            int[] dx = { -1, 1, 0, 0 };
                            int[] dy = { 0, 0, -1, 1 };

                            for (int d = 0; d < 4; d++)
                            {
                                int nx = px + dx[d];
                                int ny = py + dy[d];
                                if (nx >= 0 && nx < terrainWidth && ny >= 0 && ny < terrainHeight)
                                {
                                    if (!visited[nx, ny] && collisionMaskObj.IsSpawnArea(nx, ny))
                                    {
                                        visited[nx, ny] = true;
                                        queue.Enqueue(new Point(nx, ny));
                                    }
                                }
                            }
                        }

                        if (pixelCount > 50)
                        {
                            zone.MinX = Math.Max(0, zone.MinX - 5);
                            zone.MaxX = Math.Min(terrainWidth - 1, zone.MaxX + 5);
                            zone.MinY = Math.Max(0, zone.MinY - 5);
                            zone.MaxY = Math.Min(terrainHeight - 1, zone.MaxY + 5);
                            zones.Add(zone);
                        }
                    }
                }
            }
            return zones.OrderBy(z => z.MinX).ToList();
        }

        private class SpawnZone
        {
            public int MinX { get; set; }
            public int MaxX { get; set; }
            public int MinY { get; set; }
            public int MaxY { get; set; }
        }

        private void SpawnBoxes()
        {
            boxCanvas.Children.Clear();
            boxes.Clear();
            if (collisionMaskObj == null) return;

            for (int i = 0; i < BOX_COUNT; i++)
            {
                WeaponType[] availableWeapons = Enum.GetValues(typeof(WeaponType))
                    .Cast<WeaponType>()
                    .Where(w => w != WeaponType.Shotgun)
                    .ToArray();

                WeaponType randomWeapon = availableWeapons[random.Next(availableWeapons.Length)];
                Point? spawnPoint = FindBoxSpawnPoint();

                if (spawnPoint.HasValue)
                {
                    double screenX = spawnPoint.Value.X / scaleX;
                    double screenY = spawnPoint.Value.Y / scaleY - 40;
                    var box = new Box(screenX, screenY, randomWeapon);
                    boxes.Add(box);
                    box.AddToCanvas(boxCanvas);
                }
            }
        }

        private Point? FindBoxSpawnPoint()
        {
            if (collisionMaskObj == null || terrainMap == null) return null;
            int maxAttempts = 200;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int x = random.Next(100, terrainMap.Width - 100);
                for (int y = 50; y < terrainMap.Height - 100; y += 5)
                {
                    if (collisionMaskObj.IsGround(x, y))
                    {
                        int surfaceY = y;
                        while (surfaceY > 0 && collisionMaskObj.IsGround(x, surfaceY - 1))
                        {
                            surfaceY--;
                        }

                        bool hasEnoughSpace = true;
                        for (int checkY = surfaceY - 1; checkY >= surfaceY - 80; checkY--)
                        {
                            if (checkY < 0 || collisionMaskObj.IsGround(x, checkY))
                            {
                                hasEnoughSpace = false;
                                break;
                            }
                        }

                        if (hasEnoughSpace)
                        {
                            bool positionFree = true;
                            Rect newBoxRect = new Rect(x - 20, surfaceY - 40, 40, 40);
                            foreach (var box in boxes)
                            {
                                Rect boxRect = new Rect(box.X * scaleX, box.Y * scaleY, 40, 40);
                                if (boxRect.IntersectsWith(newBoxRect))
                                {
                                    positionFree = false;
                                    break;
                                }
                            }

                            if (positionFree)
                            {
                                double screenX = x / scaleX;
                                double screenY = surfaceY / scaleY - 40;
                                Rect newBoxScreenRect = new Rect(screenX, screenY, 40, 40);
                                foreach (var worm in worms)
                                {
                                    Rect wormRect = new Rect(worm.X, worm.Y, worm.Width, worm.Height);
                                    if (wormRect.IntersectsWith(newBoxScreenRect))
                                    {
                                        positionFree = false;
                                        break;
                                    }
                                }
                            }

                            if (positionFree) return new Point(x, surfaceY);
                        }
                        break;
                    }
                }
            }
            return null;
        }


        private bool IsGroundAtScreenPoint(double screenX, double screenY)
        {
            if (collisionMaskObj == null) return false;
            int mapX = (int)(screenX * scaleX);
            int mapY = (int)(screenY * scaleY);
            return collisionMaskObj.IsGround(mapX, mapY);
        }

        private void InitializeGame()
        {
            LoadTerrain(gameData.MapFile, gameData.TextureFile);
            SpawnWorms();
            SpawnBoxes();
            pathfinder = new Pathfinder(collisionMaskObj, scaleX, scaleY);
            InitializeTurnSystem();
            CompositionTarget.Rendering += GameLoop;
        }

        private void InitializeTurnSystem()
        {
            teamWormCursors.Clear();
            for (int i = 0; i < gameData.Teams.Count; i++)
            {
                teamWormCursors[i] = 0;
            }
            currentTurnIndex = -1;

            SwitchToNextTeamWorm();
        }

        private void SwitchToNextTeamWorm()
        {
            if (currentWorm != null)
            {
                currentWorm.StopPulseAnimation();
                currentWorm = null;
            }

            CleanupMarkers();

            var aliveTeams = worms
                .Where(w => w.Health > 0)
                .Select(w => w.TeamIndex)
                .Distinct()
                .ToList();

            if (aliveTeams.Count <= 1)
            {
                if (aliveTeams.Count == 1)
                {
                    int winningTeamIndex = aliveTeams[0];
                    var winningTeam = gameData.Teams[winningTeamIndex];

                    CompositionTarget.Rendering -= GameLoop;

                    MessageBox.Show(
                        $"Игра окончена!\nПобедила команда: {winningTeam.Name}",
                        "Победа!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    Close();
                    new MainWindow().Show();
                }
                else
                {
                    ShowStatusMessage("GAME OVER - NO WINNERS", Colors.Red, 3000);

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        Close();
                        new MainWindow().Show();
                    };
                    timer.Start();
                }
                return;
            }

            int attempts = 0;
            int teamCount = gameData.Teams.Count;

            do
            {
                currentTurnIndex = (currentTurnIndex + 1) % teamCount;
                attempts++;

                if (attempts > teamCount * 2)
                {
                    ShowStatusMessage("No active worms found!", Colors.Red, 2000);
                    return;
                }
            }
            while (!worms.Any(w => w.TeamIndex == currentTurnIndex && w.Health > 0));

            var teamWorms = worms
                .Where(w => w.TeamIndex == currentTurnIndex && w.Health > 0)
                .OrderBy(w => w.Id)
                .ToList();

            if (teamWorms.Count == 0)
            {
                SwitchToNextTeamWorm();
                return;
            }

            if (!teamWormCursors.ContainsKey(currentTurnIndex))
            {
                teamWormCursors[currentTurnIndex] = 0;
            }

            int cursor = teamWormCursors[currentTurnIndex];

            if (cursor >= teamWorms.Count)
            {
                cursor = 0;
                teamWormCursors[currentTurnIndex] = 0;
            }

            Worm nextWorm = teamWorms[cursor];

            teamWormCursors[currentTurnIndex] = (cursor + 1) % teamWorms.Count;

            SetCurrentWorm(nextWorm);
        }

        private void SetCurrentWorm(Worm worm)
        {
            StopTurnTimer();

            if (currentWorm != null)
                currentWorm.StopPulseAnimation();

            currentWorm = worm;

            if (currentWorm != null && currentWorm.Health > 0)
            {
                StartTurnTimer();

                currentWorm.StartPulseAnimation();
                UpdateWeaponInfo();

                string teamName = gameData.Teams[currentWorm.TeamIndex].Name;
                bool isComputerTeam = gameData.Teams[currentWorm.TeamIndex].IsComputer;

                string turnMessage = isComputerTeam
                    ? $"[AI] {teamName}'s turn: {currentWorm.Name}"
                    : $"{teamName}'s turn: {currentWorm.Name}";

                ShowStatusMessage(turnMessage, GetTeamColor(currentWorm.TeamIndex), 3000);

                isPlayerTurn = !isComputerTeam;
                isAITurn = isComputerTeam;
                leftPressed = false;
                rightPressed = false;

                if (isComputerTeam)
                {
                    var aiDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.0) };
                    aiDelayTimer.Tick += (s, e) =>
                    {
                        aiDelayTimer.Stop();
                        if (currentWorm == worm && worm.Health > 0 && isAITurn)
                        {
                            ExecuteAITurn();
                        }
                    };
                    aiDelayTimer.Start();
                }
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (collisionMaskObj == null) return;

            var wormsCopy = new List<Worm>(worms);
            foreach (var worm in wormsCopy)
            {
                UpdateWormPhysics(worm);
            }

            CheckBoxCollisions();

            UpdateProjectiles();

            if (currentAimingState != AimingState.None)
            {
                UpdateAimingState();
                UpdateAimLine();
            }

            if (currentWorm != null && !isAITurn)
            {
                HandlePlayerMovement();
            }

            if (isAITurn && currentAIPath != null && aiPathIndex < currentAIPath.Count && currentWorm != null)
            {
                if (aiPathStartTime != DateTime.MinValue && (DateTime.Now - aiPathStartTime).TotalSeconds > 7.0)
                {
                    EndAITurnWithDelay();
                    return;
                }

                Point currentCenter = currentWorm.GetCenter();

                while (aiPathIndex < currentAIPath.Count)
                {
                    Point nextPoint = currentAIPath[aiPathIndex];
                    double distToNext = CalculateDistance(currentCenter.X, currentCenter.Y, nextPoint.X, nextPoint.Y);
                    if (distToNext < 30)
                    {
                        aiPathIndex++;
                        aiJumpCount = 0;
                    }
                    else
                    {
                        break;
                    }
                }

                if (aiPathIndex >= currentAIPath.Count)
                {
                    if (aiTargetEnemy != null && aiTargetEnemy.Health > 0)
                    {
                        double enemyDist = CalculateDistance(currentWorm.GetCenter().X, currentWorm.GetCenter().Y,
                                                             aiTargetEnemy.GetCenter().X, aiTargetEnemy.GetCenter().Y);
                        if (enemyDist < 200)
                        {
                            ExecuteRangedAttack(aiTargetEnemy, enemyDist);
                            return;
                        }
                    }
                    EndAITurnWithDelay();
                    return;
                }

                Point targetPoint = currentAIPath[aiPathIndex];

                double distanceToTarget = CalculateDistance(currentCenter.X, currentCenter.Y, targetPoint.X, targetPoint.Y);

                if (distanceToTarget < 15)
                {
                    aiPathIndex++;
                    aiJumpCount = 0;
                }

                Vector direction = new Vector(targetPoint.X - currentCenter.X, targetPoint.Y - currentCenter.Y);
                if (direction.Length > 0)
                    direction.Normalize();

                double moveSpeed = MOVE_SPEED * 1.3;
                double newX = currentWorm.X + direction.X * moveSpeed;

                if (Math.Abs(direction.X) > 0.1)
                {
                    currentWorm.FacingRight = direction.X > 0;
                    currentWorm.UpdateSpriteAndDirection();
                }

                bool wallAhead = false;
                double aheadDistance = direction.X > 0 ? 40 : -40;
                double checkAheadX = currentWorm.X + currentWorm.Width / 2 + aheadDistance;

                for (int offsetY = -20; offsetY <= currentWorm.Height + 10; offsetY += 10)
                {
                    if (IsGroundAtScreenPoint(checkAheadX, currentWorm.Y + offsetY))
                    {
                        wallAhead = true;
                        break;
                    }
                }

                double? currentGroundY = FindGroundBelow(currentWorm.X + currentWorm.Width / 2, currentWorm.Y + currentWorm.Height);
                double? aheadGroundY = FindGroundBelow(checkAheadX, currentWorm.Y + currentWorm.Height);

                bool significantStepUp = aheadGroundY.HasValue && currentGroundY.HasValue &&
                                         aheadGroundY.Value < currentGroundY.Value - 35;

                bool smallStepUp = aheadGroundY.HasValue && currentGroundY.HasValue &&
                                   aheadGroundY.Value < currentGroundY.Value - 5 &&
                                   aheadGroundY.Value >= currentGroundY.Value - 20;

                bool canMoveHorizontally = !wallAhead && (!significantStepUp || smallStepUp);

                if (canMoveHorizontally)
                {
                    currentWorm.X = newX;
                }

                bool onGround = IsGroundAtScreenPoint(currentWorm.X + currentWorm.Width / 2, currentWorm.Y + currentWorm.Height + 2);

                bool needJump = (wallAhead || significantStepUp) && onGround && distanceToTarget > 30;

                if (needJump)
                {
                    currentWorm.VelocityY = JUMP_POWER;
                    currentWorm.IsJumping = true;
                    currentWorm.X += direction.X * MOVE_SPEED * 0.8;

                    aiJumpCount++;
                    aiLastJumpTime = DateTime.Now;

                    if (aiJumpCount > 5)
                    {
                        EndAITurnWithDelay();
                        return;
                    }
                }
                else
                {
                    if (aiJumpCount > 0) aiJumpCount--;
                }

                aiLastPositionX = currentWorm.X;
                currentWorm.UpdatePosition();
            }
        }


        private double? FindGroundBelow(double screenX, double screenY)
        {
            const int MAX_CHECK = 120;
            for (int offset = 0; offset <= MAX_CHECK; offset += 4)
            {
                if (IsGroundAtScreenPoint(screenX, screenY + offset))
                {
                    return screenY + offset;
                }
            }
            return null;
        }

        private void HandlePlayerMovement()
        {
            if ((leftPressed || rightPressed) && currentAimingState == AimingState.None)
            {
                double direction = leftPressed ? -1 : 1;
                double newX = currentWorm.X + direction * MOVE_SPEED;
                currentWorm.FacingRight = direction > 0;

                bool canMove = true;
                for (int i = 0; i < 3; i++)
                {
                    double checkY = currentWorm.Y + currentWorm.Height * (i / 3.0);
                    double checkX = direction > 0 ? newX + currentWorm.Width - 2 : newX + 2;

                    if (IsGroundAtScreenPoint(checkX, checkY))
                    {
                        canMove = false;
                        break;
                    }
                }

                if (canMove)
                {
                    currentWorm.X = newX;
                    currentWorm.UpdatePosition();
                }
            }
        }

        private void UpdateAimingState()
        {
            if (isAITurn) return;

            if (currentAimingState == AimingState.SelectingPower)
            {
                if (isPowerIncreasing)
                {
                    powerLevel += POWER_CHANGE_SPEED;
                    if (powerLevel >= MAX_POWER)
                    {
                        powerLevel = MAX_POWER;
                        isPowerIncreasing = false;
                    }
                }
                else
                {
                    powerLevel -= POWER_CHANGE_SPEED;
                    if (powerLevel <= 0)
                    {
                        powerLevel = 0;
                        isPowerIncreasing = true;
                    }
                }
                powerBar.Value = powerLevel;
                powerText.Text = $"Power: {(int)powerLevel}%";
            }
        }

        private void CheckBoxCollisions()
        {
            var boxesToCheck = boxes.ToList();
            foreach (var box in boxesToCheck)
            {
                if (box.IsOpened) continue;
                Rect boxBounds = box.GetBounds();
                var wormsCopy = new List<Worm>(worms);

                foreach (var worm in wormsCopy)
                {
                    Rect wormBounds = new Rect(worm.X, worm.Y, worm.Width, worm.Height);
                    if (boxBounds.IntersectsWith(wormBounds))
                    {
                        OpenBox(box, worm);
                        break;
                    }
                }
            }
        }

        private void OpenBox(Box box, Worm worm)
        {
            worm.AddWeapon(box.WeaponInside);
            box.Open();
            boxes.Remove(box);

            Color teamColor = GetTeamColor(worm.TeamIndex);
            ShowStatusMessage($"{worm.Name} found {box.WeaponInside}!", teamColor, 2000);

            if (worm == currentWorm)
            {
                UpdateWeaponInfo();
            }
        }

        private void UpdateProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                if (!projectile.IsActive)
                {
                    projectile.Cleanup();
                    projectile.RemoveFromCanvas();
                    projectiles.RemoveAt(i);
                    continue;
                }

                projectile.Update();
                CheckProjectileWormCollision(projectile);

                if (CheckProjectileCollision(projectile) && !projectile.HasHitGround)
                {
                    projectile.HitGround();
                }

                if (projectile.X < -100 || projectile.X > GameGrid.ActualWidth + 100 ||
                    projectile.Y < -100 || projectile.Y > GameGrid.ActualHeight + 100)
                {
                    projectile.IsActive = false;
                }
            }
        }

        private void CheckProjectileWormCollision(Projectile projectile)
        {
            if (projectile.HasExploded || projectile.HasHitGround) return;

            int shooterWormId = projectile.ShooterWormId;

            var wormsCopy = new List<Worm>(worms);

            foreach (var worm in wormsCopy)
            {
                if (worm.Health <= 0) continue;

                if (shooterWormId != -1 && worm.Id == shooterWormId)
                    continue;

                if (projectile.WeaponType == WeaponType.Shotgun)
                {
                    Rect projectileBounds = projectile.GetBounds();
                    Rect wormBounds = new Rect(worm.X, worm.Y, worm.Width, worm.Height);

                    if (projectileBounds.IntersectsWith(wormBounds))
                    {
                        int damage = (int)(projectile.Damage * 1.2);
                        worm.TakeDamage(damage);

                        Vector direction = new Vector(
                            worm.X + worm.Width / 2 - projectile.X,
                            worm.Y + worm.Height / 2 - projectile.Y
                        );

                        if (direction.Length > 0)
                        {
                            direction.Normalize();
                            ApplyKnockbackWithCollisionCheck(worm, direction, 15);
                        }

                        projectile.IsActive = false;
                        projectile.RemoveFromCanvas();
                        break;
                    }
                }
            }
        }

        private bool CheckProjectileCollision(Projectile projectile)
        {
            if (collisionMaskObj == null) return false;
            for (int i = 0; i < 5; i++)
            {
                double checkX = projectile.X - projectile.VelocityX * (i / 5.0);
                double checkY = projectile.Y - projectile.VelocityY * (i / 5.0);
                if (IsGroundAtScreenPoint(checkX, checkY))
                {
                    return true;
                }
            }
            return false;
        }

        private void KillWorm(Worm worm, string cause = "")
        {

            string deathMessage;
            if (cause.Contains("выпал из мира"))
            {
                deathMessage = $"{worm.Name} fell out of the world!";
            }
            else if (cause.Contains("by"))
            {
                deathMessage = $"{worm.Name} was killed {cause}";
            }
            else
            {
                deathMessage = $"{worm.Name} {cause}";
            }

            ShowKillMessage(deathMessage, worm.TeamColor);

            worm.RemoveFromCanvas();

            worms.Remove(worm);

            if (worm == currentWorm)
            {
                CancelAiming();
                SwitchToNextTeamWorm();
            }
        }

        private void UpdateWormPhysics(Worm worm)
        {
            if (collisionMaskObj == null) return;

            if (worm.Y > GameGrid.ActualHeight + 100 ||
                worm.X < -100 ||
                worm.X > GameGrid.ActualWidth + 100)
            {
                KillWorm(worm, "выпал из мира");
                return;
            }

            double gravity = GRAVITY;
            if (worm.ParachuteDeployed && worm.VelocityY > 0)
            {
                gravity = GRAVITY * 0.9f;
                if (Math.Abs(worm.VelocityY) > 0.5)
                {
                    worm.VelocityY *= 0.95f;
                }
            }
            worm.VelocityY += gravity;
            double newY = worm.Y + worm.VelocityY;

            if (worm.VelocityY < 0)
            {
                double headX = worm.X + worm.Width / 2;
                double headY = newY;
                if (IsGroundAtScreenPoint(headX, headY))
                {
                    worm.VelocityY = 0;
                    for (int offset = 1; offset <= 30; offset++)
                    {
                        if (!IsGroundAtScreenPoint(headX, headY + offset))
                        {
                            worm.Y = headY + offset;
                            break;
                        }
                    }
                }
                else
                {
                    worm.Y = newY;
                }
            }
            else
            {
                double? groundY = null;
                double feetX = worm.X + worm.Width / 2;
                double startFeetY = worm.Y + worm.Height;
                int maxCheckDistance = worm.ParachuteDeployed ? 100 : 50;

                for (int offset = 0; offset <= maxCheckDistance; offset += 2)
                {
                    double checkY = startFeetY + offset;
                    if (IsGroundAtScreenPoint(feetX, checkY))
                    {
                        groundY = checkY;
                        break;
                    }
                }

                if (groundY.HasValue)
                {
                    worm.Y = groundY.Value - worm.Height;
                    worm.VelocityY = 0;
                    worm.IsJumping = false;

                    if (worm.ParachuteDeployed)
                    {
                        worm.ParachuteDeployed = false;
                        worm.IsUsingParachute = false;
                        worm.UpdateSpriteAndDirection();
                    }
                }
                else
                {
                    worm.Y = newY;
                    if (!worm.IsJumping)
                    {
                        worm.IsJumping = true;
                    }
                }
            }
            worm.UpdateSpriteAndDirection();
            worm.UpdatePosition();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (isAITurn) return;
            if (currentWorm == null) return;

            if (e.Key == Key.Left) leftPressed = true;
            if (e.Key == Key.Right) rightPressed = true;

            if (e.Key == Key.Up && !currentWorm.IsJumping &&
                currentAimingState == AimingState.None &&
                !isSelectingTeleport && !isSelectingAirstrike)
            {
                double checkX = currentWorm.X + currentWorm.Width / 2;
                double checkY = currentWorm.Y + currentWorm.Height + 2;
                if (IsGroundAtScreenPoint(checkX, checkY))
                {
                    currentWorm.VelocityY = JUMP_POWER;
                    currentWorm.IsJumping = true;
                }
            }

            if (e.Key == Key.Escape)
            {
                if (isSelectingTeleport || isSelectingAirstrike)
                {
                    CancelAiming();
                }
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (isAITurn) return;
            if (e.Key == Key.Left) leftPressed = false;
            if (e.Key == Key.Right) rightPressed = false;
        }

        private Projectile CreateProjectile(double x, double y, double velocityX, double velocityY, Weapon weapon)
        {
            double radius = 3;
            switch (weapon.Type)
            {
                case WeaponType.Grenade:
                case WeaponType.Dynamite:
                case WeaponType.Sheep:
                    radius = 12;
                    break;
                default:
                    radius = 3;
                    break;
            }

            int shooterWormId = currentWorm?.Id ?? -1;
            var projectile = new Projectile(
                x, y, velocityX, velocityY,
                weapon.Damage, weapon.Type,
                weapon.ExplosionRadius, weapon.KnockbackPower,
                weapon.FuseTime, this,
                Colors.Orange, weapon.ProjectileSprite,
                shooterWormId
            );

            projectile.Radius = radius;
            projectiles.Add(projectile);
            projectile.AddToCanvas(projectileCanvas);
            return projectile;
        }

        private bool CalculateAimForTarget(Point shooterCenter, Point targetCenter, out double angle, out double power)
        {
            angle = 45.0;
            power = 60.0;

            double dx = targetCenter.X - shooterCenter.X;
            double dy = targetCenter.Y - shooterCenter.Y;

            if (Math.Abs(dx) < 10 && dy < -50)
            {
                angle = 80.0;
                power = Math.Min(90.0, Math.Abs(dy) / 10.0);
                return true;
            }

            if (dy >= 0)
            {
                angle = Math.Max(10.0, Math.Min(40.0, 30.0 - Math.Abs(dy) / 10.0));
                power = Math.Min(80.0, Math.Abs(dx) / 5.0);
                return true;
            }

            const double g = 0.8;
            const double v0_min = 10.0;
            const double v0_max = 40.0;

            double bestAngle = 45.0;
            double bestPower = 60.0;
            double bestError = double.MaxValue;

            for (double testAngle = 15.0; testAngle <= 75.0; testAngle += 5.0)
            {
                for (double testPower = 20.0; testPower <= 100.0; testPower += 10.0)
                {
                    double v0 = v0_min + (testPower / 100.0) * (v0_max - v0_min);
                    double angleRad = testAngle * Math.PI / 180.0;

                    double t = dx / (v0 * Math.Cos(angleRad));
                    if (t <= 0) continue;

                    double y = v0 * Math.Sin(angleRad) * t - 0.5 * g * t * t;

                    double error = Math.Abs(y + dy);

                    if (error < bestError)
                    {
                        bestError = error;
                        bestAngle = testAngle;
                        bestPower = testPower;
                    }
                }
            }

            if (bestError < 100.0)
            {
                angle = bestAngle;
                power = bestPower;

                angle += random.NextDouble() * 10.0 - 5.0;
                power += random.NextDouble() * 10.0 - 5.0;

                angle = Math.Max(10.0, Math.Min(80.0, angle));
                power = Math.Max(30.0, Math.Min(95.0, power));

                return true;
            }

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                angle = 45.0;
                power = Math.Min(90.0, Math.Abs(dx) / 8.0 + Math.Abs(dy) / 20.0);
            }
            else
            {
                angle = 60.0 + (Math.Abs(dy) / 100.0) * 20.0;
                power = Math.Min(80.0, Math.Abs(dy) / 10.0);
            }

            angle = Math.Max(15.0, Math.Min(80.0, angle));
            power = Math.Max(40.0, Math.Min(95.0, power));

            return bestError < 150.0;
        }

        private void CalculateSimpleAim(Point shooterCenter, Point targetCenter, out double angle, out double power)
        {
            double dx = targetCenter.X - shooterCenter.X;
            double dy = targetCenter.Y - shooterCenter.Y;

            if (Math.Abs(dx) < 50)
            {
                if (dy < -30)
                {
                    angle = 70.0;
                    power = Math.Max(40.0, Math.Min(70.0, Math.Abs(dy) / 2.0));
                }
                else if (dy > 30)
                {
                    angle = 20.0;
                    power = Math.Max(30.0, Math.Min(60.0, Math.Abs(dx) / 2.0));
                }
                else
                {
                    angle = 25.0;
                    power = Math.Max(35.0, Math.Min(60.0, Math.Abs(dx) / 3.0));
                }
                return;
            }

            double distance = Math.Sqrt(dx * dx + dy * dy);

            angle = 45.0;
            if (distance < 100) angle = 25.0;
            else if (distance < 200) angle = 35.0;
            else if (distance < 300) angle = 45.0;
            else if (distance < 400) angle = 55.0;
            else angle = 65.0;

            if (dy < -50)
            {
                angle += 15.0;
            }
            else if (dy > 50)
            {
                angle -= 15.0;
            }

            power = Math.Min(95.0, distance / 8.0);

            angle = Math.Max(10.0, Math.Min(80.0, angle));
            power = Math.Max(30.0, Math.Min(95.0, power));
        }


        private void CreateTurnTimerUI()
        {
            turnTimerPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20, 20, 0, 0),
                Visibility = Visibility.Collapsed
            };

            turnTimerText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Text = $"00:{TURN_TIME_SECONDS:00}"
            };

            var stackPanel = new StackPanel();
            var titleText = new TextBlock
            {
                Text = "Время хода:",
                Foreground = Brushes.LightGray,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 2)
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(turnTimerText);
            turnTimerPanel.Child = stackPanel;
        }

        private void StartTurnTimer()
        {
            if (turnTimer != null && turnTimer.IsEnabled)
            {
                turnTimer.Stop();
            }

            turnTimer = new DispatcherTimer();
            turnTimer.Interval = TimeSpan.FromSeconds(1);
            turnTimer.Tick += TurnTimer_Tick;

            remainingSeconds = TURN_TIME_SECONDS;

            UpdateTurnTimerText();

            turnTimerPanel.Visibility = Visibility.Visible;

            turnTimer.Start();
        }

        private void StopTurnTimer()
        {
            if (turnTimer != null && turnTimer.IsEnabled)
            {
                turnTimer.Stop();
            }
            turnTimerPanel.Visibility = Visibility.Collapsed;
        }

        private void TurnTimer_Tick(object sender, EventArgs e)
        {
            remainingSeconds--;
            UpdateTurnTimerText();

            if (remainingSeconds <= 0)
            {
                StopTurnTimer();

                if (isPlayerTurn && !isAITurn &&
                    currentAimingState == AimingState.None &&
                    !isSelectingTeleport && !isSelectingAirstrike)
                {
                    ShowStatusMessage("Время вышло! Ход пропущен.", Colors.Orange, 2000);
                    SkipTurn();
                }
            }
            else if (remainingSeconds <= 5)
            {
                turnTimerText.Foreground = (remainingSeconds % 2 == 0) ? Brushes.Red : Brushes.White;
            }
        }

        private void UpdateTurnTimerText()
        {
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            turnTimerText.Text = $"{minutes:00}:{seconds:00}";
        }
    }
}
