using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Worms
{
    public partial class GameSetupWindow : Window
    {
        private class MapInfo
        {
            public string MapFile { get; set; }
            public string TextureFile { get; set; }
            public string DisplayName { get; set; }
        }

        private class TeamSetupInfo
        {
            public string Name { get; set; }
            public int WormCount { get; set; } = 3;
            public List<string> WormNames { get; set; } = new List<string>();
            public bool IsComputer { get; set; } = false;
        }

        private readonly List<MapInfo> maps = new List<MapInfo>()
        {
            new MapInfo { MapFile = "map1.png", TextureFile = "ground.png", DisplayName = "Caves" },
            new MapInfo { MapFile = "map2.png", TextureFile = "beach.png", DisplayName = "Beach" },
            new MapInfo { MapFile = "map3.png", TextureFile = "snow.png", DisplayName = "Snow Mountain" }
        };

        private readonly List<TeamSetupInfo> teams = new List<TeamSetupInfo>();
        private readonly Color[] teamColors = { Colors.Red, Colors.Blue, Colors.LimeGreen, Colors.Yellow };

        private string selectedMapFile = "map1.png";
        private string selectedTextureFile = "ground.png";

        private int computerTeamsCount = 0;
        private const int MAX_COMPUTER_TEAMS = 3;

        public GameSetupWindow()
        {
            InitializeComponent();
            LoadMaps();
            LoadTeams();
        }

        private void LoadMaps()
        {
            foreach (var map in maps)
            {
                MapComboBox.Items.Add(map.DisplayName);
            }
            MapComboBox.SelectedIndex = 0;
        }

        private void MapComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MapComboBox.SelectedIndex >= 0)
            {
                var map = maps[MapComboBox.SelectedIndex];
                selectedMapFile = map.MapFile;
                selectedTextureFile = map.TextureFile;
            }
        }

        private void LoadTeams()
        {
            AddTeam(0, "Player Team", false);
            AddTeam(1, "PC Team 1", true);
            AddAddTeamButton();
        }

        private void AddTeam(int index, string defaultName = null, bool isComputer = false)
        {
            var team = new TeamSetupInfo
            {
                Name = defaultName ?? $"Team {index + 1}",
                IsComputer = isComputer
            };

            for (int i = 0; i < team.WormCount; i++)
            {
                team.WormNames.Add($"Worm {i + 1}");
            }

            teams.Add(team);
            if (isComputer) computerTeamsCount++;
            CreateTeamUI(team, index);
        }

        private void CreateTeamUI(TeamSetupInfo team, int colorIndex)
        {
            var teamGrid = new Grid { Margin = new Thickness(0, 0, 0, 25) };
            teamGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            teamGrid.ColumnDefinitions.Add(new ColumnDefinition());
            teamGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var colorBox = new Border
            {
                Background = new SolidColorBrush(teamColors[colorIndex]),
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 20, 0)
            };
            Grid.SetColumn(colorBox, 0);
            teamGrid.Children.Add(colorBox);

            var panel = new StackPanel();
            Grid.SetColumn(panel, 1);

            var nameBox = new TextBox
            {
                Text = team.Name,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 80)),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            nameBox.TextChanged += (s, e) => team.Name = nameBox.Text;

            var countRow = new StackPanel { Orientation = Orientation.Horizontal };
            countRow.Children.Add(new TextBlock
            {
                Text = "Worms:",
                Foreground = Brushes.LightGray,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            var countCombo = new ComboBox { Width = 80, SelectedIndex = 0 };
            for (int i = 3; i <= 5; i++) countCombo.Items.Add(i);
            countCombo.SelectedItem = team.WormCount;

            var wormPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            UpdateWormNames(team, wormPanel);

            countCombo.SelectionChanged += (s, e) =>
            {
                team.WormCount = (int)countCombo.SelectedItem;
                UpdateWormNames(team, wormPanel);
            };

            countRow.Children.Add(countCombo);

            panel.Children.Add(nameBox);
            panel.Children.Add(countRow);
            panel.Children.Add(wormPanel);

            teamGrid.Children.Add(panel);

            var aiCheckBox = new CheckBox
            {
                Content = "AI Control",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0),
                IsChecked = team.IsComputer,
                ToolTip = "Команда будет под управлением компьютера"
            };

            aiCheckBox.Checked += (s, e) =>
            {
                if (computerTeamsCount >= MAX_COMPUTER_TEAMS)
                {
                    MessageBox.Show($"Может быть только {MAX_COMPUTER_TEAMS} команды под управлением компьютера!",
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    aiCheckBox.IsChecked = false;
                    return;
                }

                team.IsComputer = true;
                computerTeamsCount++;

                if (string.IsNullOrWhiteSpace(team.Name) || team.Name.StartsWith("Team"))
                {
                    team.Name = $"PC Team {computerTeamsCount}";
                    nameBox.Text = team.Name;
                }
            };

            aiCheckBox.Unchecked += (s, e) =>
            {
                if (team.IsComputer)
                {
                    computerTeamsCount--;
                }
                team.IsComputer = false;

                if (team.Name.StartsWith("PC Team"))
                {
                    team.Name = $"Team {colorIndex + 1}";
                    nameBox.Text = team.Name;
                }
            };

            Grid.SetColumn(aiCheckBox, 2);
            teamGrid.Children.Add(aiCheckBox);

            int pos = TeamsPanel.Children.Count;
            if (TeamsPanel.Children.Count > 0 && TeamsPanel.Children[pos - 1] is Button b && b.Content.ToString() == "+ Add Team")
                pos--;

            TeamsPanel.Children.Insert(pos, teamGrid);
        }

        private void UpdateWormNames(TeamSetupInfo team, StackPanel panel)
        {
            panel.Children.Clear();

            while (team.WormNames.Count < team.WormCount)
            {
                team.WormNames.Add($"Worm {team.WormNames.Count + 1}");
            }

            while (team.WormNames.Count > team.WormCount)
            {
                team.WormNames.RemoveAt(team.WormNames.Count - 1);
            }

            for (int i = 0; i < team.WormCount; i++)
            {
                int idx = i;
                var tb = new TextBox
                {
                    Text = team.WormNames[idx],
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 90)),
                    Foreground = Brushes.White,
                    ToolTip = $"Worm {idx + 1} name"
                };
                tb.TextChanged += (s, e) => team.WormNames[idx] = tb.Text;
                panel.Children.Add(tb);
            }
        }

        private void AddAddTeamButton()
        {
            if (teams.Count >= 4) return;

            var btn = new Button
            {
                Content = "+ Add Team",
                Width = 200,
                Height = 45,
                Background = new SolidColorBrush(Colors.DarkGreen),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 20, 0, 0),
                FontSize = 16
            };

            btn.Click += (s, e) =>
            {
                if (teams.Count < 4)
                {
                    bool isComputer = computerTeamsCount < MAX_COMPUTER_TEAMS;
                    AddTeam(teams.Count, isComputer ? $"PC Team {computerTeamsCount + 1}" : $"Team {teams.Count + 1}", isComputer);

                    if (teams.Count >= 4) TeamsPanel.Children.Remove(btn);
                }
            };

            TeamsPanel.Children.Add(btn);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (teams.Count < 2)
            {
                MessageBox.Show("Должно быть хотя бы две команды!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool hasPlayerTeam = false;
            foreach (var team in teams)
            {
                if (!team.IsComputer)
                {
                    hasPlayerTeam = true;
                    break;
                }
            }

            if (!hasPlayerTeam)
            {
                MessageBox.Show("Хотя бы одна команда должна быть под контролем игрока", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            int playerTeamCount = 0;
            int computerTeamCount = 0;

            foreach (var team in teams)
            {
                if (team.IsComputer)
                    computerTeamCount++;
                else
                    playerTeamCount++;
            }

            var gameData = new GameData
            {
                MapFile = selectedMapFile,
                TextureFile = selectedTextureFile,
                Teams = new List<TeamData>()
            };

            for (int i = 0; i < teams.Count; i++)
            {
                var teamSetup = teams[i];
                var team = new TeamData(
                    teamSetup.Name,
                    teamColors[i],
                    new List<string>(teamSetup.WormNames)
                )
                {
                    IsComputer = teamSetup.IsComputer
                };
                gameData.Teams.Add(team);
            }

            var gameWindow = new GameWindow(gameData);
            gameWindow.Show();

            foreach (Window w in Application.Current.Windows)
            {
                if (w != gameWindow)
                    w.Close();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}