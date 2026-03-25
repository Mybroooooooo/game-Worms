using System;
using System.Collections.Generic;
using System.Windows;

namespace Worms
{
    public class Pathfinder
    {
        private CollisionMask collisionMask;
        private double scaleX;
        private double scaleY;

        public Pathfinder(CollisionMask mask, double scaleX, double scaleY)
        {
            this.collisionMask = mask;
            this.scaleX = scaleX;
            this.scaleY = scaleY;
        }

        public List<Point> FindPath(Point start, Point goal, double wormWidth, double wormHeight, double jumpPower, double gravity)
        {
            if (Distance(start, goal) < 20)
            {
                return new List<Point> { start, goal };
            }

            var openSet = new PriorityQueue<Node, double>();
            var closedSet = new HashSet<string>();
            var cameFrom = new Dictionary<string, Node>();
            var gScore = new Dictionary<string, double>();

            string startKey = PointToKey(start);

            var startNode = new Node { Position = start, G = 0, H = Heuristic(start, goal) };
            openSet.Enqueue(startNode, startNode.F);
            gScore[startKey] = 0;

            int maxIterations = 10000;
            int iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                var current = openSet.Dequeue();
                string currentKey = PointToKey(current.Position);

                if (Math.Abs(current.Position.X - goal.X) < 25 && Math.Abs(current.Position.Y - goal.Y) < 45)
                {
                    return ReconstructPath(cameFrom, current);
                }

                closedSet.Add(currentKey);

                var neighbors = new List<Point>
        {
            new Point(current.Position.X - 30, current.Position.Y),
            new Point(current.Position.X + 30, current.Position.Y),
            new Point(current.Position.X, current.Position.Y - 60),
            new Point(current.Position.X - 20, current.Position.Y - 40),
            new Point(current.Position.X + 20, current.Position.Y - 40),

            new Point(current.Position.X, current.Position.Y + 60),
            new Point(current.Position.X - 20, current.Position.Y + 40),
            new Point(current.Position.X + 20, current.Position.Y + 40),

            new Point(current.Position.X - 15, current.Position.Y),
            new Point(current.Position.X + 15, current.Position.Y),
            new Point(current.Position.X - 15, current.Position.Y + 20),
            new Point(current.Position.X + 15, current.Position.Y + 20),
            new Point(current.Position.X - 15, current.Position.Y - 20),
            new Point(current.Position.X + 15, current.Position.Y - 20)
        };

                foreach (var neighborPos in neighbors)
                {
                    string neighborKey = PointToKey(neighborPos);

                    if (closedSet.Contains(neighborKey)) continue;

                    if (!IsWalkable(neighborPos, wormWidth, wormHeight)) continue;

                    double tentativeG = current.G + Distance(current.Position, neighborPos);

                    if (!gScore.TryGetValue(neighborKey, out double existingG) || tentativeG < existingG)
                    {
                        var neighborNode = new Node
                        {
                            Position = neighborPos,
                            Parent = current,
                            G = tentativeG,
                            H = Heuristic(neighborPos, goal)
                        };

                        cameFrom[neighborKey] = current;
                        gScore[neighborKey] = tentativeG;
                        openSet.Enqueue(neighborNode, neighborNode.F);
                    }
                }
            }

            return null;
        }

        private bool IsWalkable(Point pos, double width, double height)
        {
            if (collisionMask == null) return false;

            int steps = 5;
            for (int i = 0; i <= steps; i++)
            {
                for (int j = 0; j <= steps; j++)
                {
                    double offsetX = -width / 2 + (i / (double)steps) * width;
                    double offsetY = -height / 2 + (j / (double)steps) * height;

                    double checkScreenX = pos.X + offsetX;
                    double checkScreenY = pos.Y + offsetY;

                    int mapX = (int)(checkScreenX * scaleX);
                    int mapY = (int)(checkScreenY * scaleY);

                    if (mapX < 0 || mapX >= collisionMask.Width || mapY < 0 || mapY >= collisionMask.Height)
                        return false;

                    if (j < steps * 0.8)
                    {
                        if (collisionMask.IsGround(mapX, mapY))
                            return false;
                    }
                    else
                    {
                        if (collisionMask.IsGround(mapX, mapY))
                        {
                        }
                    }
                }
            }
            return true;
        }

        private string PointToKey(Point p) => $"{(int)p.X}_{(int)p.Y}";

        private double Distance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private double Heuristic(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        private List<Point> ReconstructPath(Dictionary<string, Node> cameFrom, Node current)
        {
            var path = new List<Point> { current.Position };
            while (cameFrom.TryGetValue(PointToKey(current.Position), out var parent))
            {
                current = parent;
                path.Add(current.Position);
            }
            path.Reverse();
            return path;
        }

        private class Node
        {
            public Point Position { get; set; }
            public Node Parent { get; set; }
            public double G { get; set; }
            public double H { get; set; }
            public double F => G + H;
        }

        private class PriorityQueue<TItem, TPriority> where TPriority : IComparable<TPriority>
        {
            private List<(TItem Item, TPriority Priority)> _heap = new List<(TItem, TPriority)>();

            public int Count => _heap.Count;

            public void Enqueue(TItem item, TPriority priority)
            {
                _heap.Add((item, priority));
                int i = _heap.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_heap[parent].Priority.CompareTo(_heap[i].Priority) <= 0) break;
                    (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                    i = parent;
                }
            }

            public TItem Dequeue()
            {
                if (_heap.Count == 0) throw new InvalidOperationException("Queue empty");

                var result = _heap[0].Item;
                _heap[0] = _heap[_heap.Count - 1];
                _heap.RemoveAt(_heap.Count - 1);

                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;
                    int smallest = i;

                    if (left < _heap.Count && _heap[left].Priority.CompareTo(_heap[smallest].Priority) < 0)
                        smallest = left;
                    if (right < _heap.Count && _heap[right].Priority.CompareTo(_heap[smallest].Priority) < 0)
                        smallest = right;

                    if (smallest == i) break;

                    (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                    i = smallest;
                }

                return result;
            }
        }
    }
}