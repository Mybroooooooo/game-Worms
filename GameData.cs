using System.Collections.Generic;
using System.Windows.Media;

namespace Worms
{
    public class GameData
    {
        public string MapFile { get; set; }
        public string TextureFile { get; set; }
        public List<TeamData> Teams { get; set; } = new List<TeamData>();
    }

    public class TeamData
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public List<string> WormNames { get; set; } = new List<string>();
        public bool IsComputer { get; set; } = false;

        public TeamData(string name, Color color, List<string> wormNames)
        {
            Name = name;
            Color = color;
            WormNames = wormNames;
        }

        public TeamData(string name, Color color, List<string> wormNames, bool isComputer)
        {
            Name = name;
            Color = color;
            WormNames = wormNames;
            IsComputer = isComputer;
        }
    }
}