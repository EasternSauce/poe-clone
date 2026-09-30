using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    public sealed class PlacedItem
    {
        public ItemData Item { get; }
        public int X { get; }
        public int Y { get; }

        public PlacedItem(ItemData item, int x, int y)
        {
            Item = item;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// A grid of cells holding items of different sizes (like Path of Exile's inventory).
    /// Pure C#, no Unity dependencies, so it can be unit tested.
    /// </summary>
    public sealed class InventoryGrid
    {
        private readonly List<PlacedItem> placed = new List<PlacedItem>();

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<PlacedItem> Items => placed;

        public event Action Changed;

        public InventoryGrid(int width, int height)
        {
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
        }

        public bool InBounds(ItemData item, int x, int y)
        {
            return x >= 0 && y >= 0 && x + item.Width <= Width && y + item.Height <= Height;
        }

        public bool Contains(ItemData item)
        {
            foreach (PlacedItem p in placed)
            {
                if (p.Item == item)
                    return true;
            }
            return false;
        }

        public List<PlacedItem> GetOverlapping(ItemData item, int x, int y)
        {
            List<PlacedItem> result = new List<PlacedItem>();
            foreach (PlacedItem p in placed)
            {
                bool overlaps =
                    x < p.X + p.Item.Width && p.X < x + item.Width &&
                    y < p.Y + p.Item.Height && p.Y < y + item.Height;

                if (overlaps)
                    result.Add(p);
            }
            return result;
        }

        public bool CanPlace(ItemData item, int x, int y)
        {
            if (item == null || Contains(item))
                return false;

            return InBounds(item, x, y) && GetOverlapping(item, x, y).Count == 0;
        }

        public bool TryPlace(ItemData item, int x, int y)
        {
            if (!CanPlace(item, x, y))
                return false;

            placed.Add(new PlacedItem(item, x, y));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Puts the item in the first free spot, scanning left-to-right, top-to-bottom.</summary>
        public bool TryAutoPlace(ItemData item)
        {
            if (item == null || Contains(item))
                return false;

            for (int y = 0; y <= Height - item.Height; y++)
            {
                for (int x = 0; x <= Width - item.Width; x++)
                {
                    if (TryPlace(item, x, y))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Places the item at (x, y). If exactly one item is in the way it is swapped out and returned
        /// in <paramref name="replaced"/>. Fails if it is out of bounds or overlaps two or more items.
        /// </summary>
        public bool TryPlaceOrSwap(ItemData item, int x, int y, out ItemData replaced)
        {
            replaced = null;

            if (item == null || Contains(item) || !InBounds(item, x, y))
                return false;

            List<PlacedItem> overlapping = GetOverlapping(item, x, y);
            if (overlapping.Count > 1)
                return false;

            if (overlapping.Count == 1)
            {
                replaced = overlapping[0].Item;
                placed.Remove(overlapping[0]);
            }

            placed.Add(new PlacedItem(item, x, y));
            Changed?.Invoke();
            return true;
        }

        /// <summary>The item covering cell (x, y), or null.</summary>
        public PlacedItem GetAt(int x, int y)
        {
            foreach (PlacedItem p in placed)
            {
                if (x >= p.X && x < p.X + p.Item.Width && y >= p.Y && y < p.Y + p.Item.Height)
                    return p;
            }
            return null;
        }

        public bool Remove(ItemData item)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Item == item)
                {
                    placed.RemoveAt(i);
                    Changed?.Invoke();
                    return true;
                }
            }
            return false;
        }
    }
}
