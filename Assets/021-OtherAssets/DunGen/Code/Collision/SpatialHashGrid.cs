using DunGen.Common;
using DunGen.Pooling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Collision
{
    /// <summary>
    /// A generic spatial hash grid that divides space into uniform cells for fast spatial queries.
    /// The grid operates on a 2D plane perpendicular to the specified up axis.
    /// </summary>
    /// <typeparam name="T">The type of objects stored in the grid.</typeparam>
    public class SpatialHashGrid<T>
    {
        private readonly Dictionary<long, List<T>> cells;
        private readonly float cellSize;
        private readonly Func<T, Bounds> getBounds;
        private readonly AxisDirection upAxis;
        private readonly (int, int) primaryAxes; // Indices for the two axes that form the 2D plane


        /// <summary>
        /// Constructs a spatial hash grid.
        /// </summary>
        /// <param name="cellSize">Size of each grid cell</param>
        /// <param name="getBounds">Delegate to extract bounds from objects</param>
        /// <param name="upDirection">The up axis direction (determines the 2D plane)</param>
        public SpatialHashGrid(float cellSize, Func<T, Bounds> getBounds, AxisDirection upDirection = AxisDirection.PosY)
        {
            this.cells = new Dictionary<long, List<T>>();
            this.cellSize = cellSize;
            this.getBounds = getBounds;
            this.upAxis = upDirection;

            // Determine which axes to use for the 2D grid based on up direction
            this.primaryAxes = GetPrimaryAxes(upDirection);
        }

        private (int, int) GetPrimaryAxes(AxisDirection upDirection)
        {
            switch (upDirection)
            {
                case AxisDirection.PosY:
                case AxisDirection.NegY:
                    return (0, 2); // Use X and Z axes
                case AxisDirection.PosX:
                case AxisDirection.NegX:
                    return (1, 2); // Use Y and Z axes
                case AxisDirection.PosZ:
                case AxisDirection.NegZ:
                    return (0, 1); // Use X and Y axes
                default:
                    throw new ArgumentException("Invalid axis direction", nameof(upDirection));
            }
        }

        private Vector2 GetGridPosition(Vector3 worldPos)
        {
            // Extract the two coordinates for our 2D plane based on the up axis
            float x = worldPos[primaryAxes.Item1];
            float y = worldPos[primaryAxes.Item2];
            return new Vector2(x, y);
        }

        private long GetCellKey(int x, int y)
        {
            return ((long)x << 32) | (y & 0xffffffffL);
        }

        /// <summary>
        /// Inserts an object into the spatial hash grid.
        /// </summary>
        public void Insert(T obj)
        {
            Bounds bounds = getBounds(obj);
            Vector2 min = GetGridPosition(bounds.min);
            Vector2 max = GetGridPosition(bounds.max);

            int minX = Mathf.FloorToInt(min.x / cellSize);
            int minY = Mathf.FloorToInt(min.y / cellSize);
            int maxX = Mathf.FloorToInt(max.x / cellSize);
            int maxY = Mathf.FloorToInt(max.y / cellSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    long key = GetCellKey(x, y);

                    if (!cells.TryGetValue(key, out List<T> cell))
                    {
                        cell = new List<T>();
                        cells[key] = cell;
                    }

                    cell.Add(obj);
                }
            }
        }

        /// <summary>
        /// Removes an object from the spatial hash grid.
        /// </summary>
        public bool Remove(T obj)
        {
            bool removed = false;
            Bounds bounds = getBounds(obj);
            Vector2 min = GetGridPosition(bounds.min);
            Vector2 max = GetGridPosition(bounds.max);

            int minX = Mathf.FloorToInt(min.x / cellSize);
            int minY = Mathf.FloorToInt(min.y / cellSize);
            int maxX = Mathf.FloorToInt(max.x / cellSize);
            int maxY = Mathf.FloorToInt(max.y / cellSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Int64 key = GetCellKey(x, y);
                    if (cells.TryGetValue(key, out List<T> cell))
                    {
                        if (cell.Remove(obj))
                        {
                            removed = true;
                            if (cell.Count == 0)
                            {
                                cells.Remove(key);
                            }
                        }
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// Queries the spatial hash grid for objects that might intersect with the specified bounds.
        /// </summary>
        public void Query(Bounds queryBounds, ref List<T> results)
        {
            Vector3 queryBoundsMin = queryBounds.min;
            Vector3 queryBoundsMax = queryBounds.max;

            Vector2 min = GetGridPosition(queryBoundsMin);
            Vector2 max = GetGridPosition(queryBoundsMax);

            int minX = Mathf.FloorToInt(min.x / cellSize);
            int minY = Mathf.FloorToInt(min.y / cellSize);
            int maxX = Mathf.FloorToInt(max.x / cellSize);
            int maxY = Mathf.FloorToInt(max.y / cellSize);

			using (CollectionPool.HashSet<T>.Get(out HashSet<T> uniqueResults))
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        long key = GetCellKey(x, y);

                        if (cells.TryGetValue(key, out List<T> cell))
                        {
                            foreach (T obj in cell)
                            {
                                if (!uniqueResults.Contains(obj))
                                {
                                    uniqueResults.Add(obj);
                                    results.Add(obj);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears all objects from the grid.
        /// </summary>
        public void Clear()
        {
            cells.Clear();
        }

        /// <summary>
        /// Draws debug visualization of the grid and contained objects.
        /// </summary>
        /// <param name="duration">How long the debug lines should remain visible</param>
        public void DrawDebug(float duration = 0.0f)
        {
            Color gridCellColour = Color.white * 0.75f;
            Color boundsColour = Color.green;

            // Get unique cell coordinates to draw grid
            var cellCoords = new HashSet<(int x, int y)>();
            foreach (var key in cells.Keys)
            {
                int x = (int)(key >> 32);
                int y = (int)(key & 0xffffffffL);
                cellCoords.Add((x, y));
            }

            // Draw grid cells
            foreach (var coord in cellCoords)
            {
                Vector3 min = Vector3.zero;
                Vector3 max = Vector3.zero;

                // Set the coordinates based on primary axes
                min[primaryAxes.Item1] = coord.x * cellSize;
                min[primaryAxes.Item2] = coord.y * cellSize;
                max[primaryAxes.Item1] = (coord.x + 1) * cellSize;
                max[primaryAxes.Item2] = (coord.y + 1) * cellSize;

                Vector3 p1 = min;
                Vector3 p2 = min;
                p2[primaryAxes.Item1] = max[primaryAxes.Item1];

                Vector3 p3 = max;
                Vector3 p4 = max;
                p4[primaryAxes.Item1] = min[primaryAxes.Item1];

                Debug.DrawLine(p1, p2, gridCellColour, duration);
                Debug.DrawLine(p2, p3, gridCellColour, duration);
                Debug.DrawLine(p3, p4, gridCellColour, duration);
                Debug.DrawLine(p4, p1, gridCellColour, duration);
            }

            // Draw object bounds
            var drawnObjects = new HashSet<T>();
            foreach (var cellObjects in cells.Values)
            {
                foreach (var obj in cellObjects)
                {
                    if (drawnObjects.Add(obj)) // Only draw each object once
                    {
                        var bounds = getBounds(obj);
                        Vector3 min = bounds.min;
                        Vector3 max = bounds.max;

                        // Create four corners all at the same height (using min for the up axis)
                        Vector3 p1 = Vector3.zero;
                        Vector3 p2 = Vector3.zero;
                        Vector3 p3 = Vector3.zero;
                        Vector3 p4 = Vector3.zero;

                        // Set the coordinates for the primary axes
                        p1[primaryAxes.Item1] = min[primaryAxes.Item1];
                        p1[primaryAxes.Item2] = min[primaryAxes.Item2];

                        p2[primaryAxes.Item1] = max[primaryAxes.Item1];
                        p2[primaryAxes.Item2] = min[primaryAxes.Item2];

                        p3[primaryAxes.Item1] = max[primaryAxes.Item1];
                        p3[primaryAxes.Item2] = max[primaryAxes.Item2];

                        p4[primaryAxes.Item1] = min[primaryAxes.Item1];
                        p4[primaryAxes.Item2] = max[primaryAxes.Item2];

                        // Set the up axis coordinate to 0 for all points
                        int upAxisIndex = (int)(upAxis) / 2; // Convert AxisDirection to index (0=X, 1=Y, 2=Z)
                        float upCoord = 0f;
                        p1[upAxisIndex] = upCoord;
                        p2[upAxisIndex] = upCoord;
                        p3[upAxisIndex] = upCoord;
                        p4[upAxisIndex] = upCoord;

                        Debug.DrawLine(p1, p2, boundsColour, duration);
                        Debug.DrawLine(p2, p3, boundsColour, duration);
                        Debug.DrawLine(p3, p4, boundsColour, duration);
                        Debug.DrawLine(p4, p1, boundsColour, duration);
                    }
                }
            }
        }
    }
}
