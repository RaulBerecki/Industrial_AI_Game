using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

public class GridManager : MonoBehaviour
{
    [Header("Setări Grid")]
    public int width = 15;
    public int height = 15;
    public float cellSize = 1f;
    public LayerMask obstacleLayer; // Setează zidurile pe acest Layer!

    private Node[,] grid;

    void Awake()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        grid = new Node[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 worldPoint = GetWorldPosition(x, z);

                // Scanăm dacă există un collider (zid) în această celulă
                bool isWalkable = !(Physics.CheckSphere(worldPoint, cellSize / 2.1f, obstacleLayer));

                grid[x, z] = new Node(isWalkable, worldPoint, x, z);
            }
        }
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(x * cellSize, 0, z * cellSize) + new Vector3(cellSize / 2, 0, cellSize / 2);
    }

    // Vizualizăm grid-ul în Editor pentru a vedea ce "vede" AI-ul
    void OnDrawGizmos()
    {
        if (grid == null) return;

        foreach (Node n in grid)
        {
            Gizmos.color = n.isWalkable ? Color.white : Color.red;
            Gizmos.DrawWireCube(n.worldPosition, new Vector3(cellSize - 0.1f, 0.1f, cellSize - 0.1f));
        }
    }
}