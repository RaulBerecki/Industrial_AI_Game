using UnityEngine;

public class Node
{
    public bool isWalkable;      // Poate AI-ul să treacă pe aici?
    public Vector3 worldPosition; // Poziția în spațiul 3D
    public int gridX;            // Coordonata X în matrice
    public int gridZ;            // Coordonata Z în matrice

    public int gCost;            // Distanța de la punctul de Start
    public int hCost;            // Distanța estimată până la Destinație (Heuristică)
    public Node parent;          // Nodul anterior (pentru a reconstrui drumul)

    // Proprietate pentru calculul costului total
    public int fCost => gCost + hCost;

    // Acesta este constructorul cu 4 argumente care lipsea
    public Node(bool _isWalkable, Vector3 _worldPos, int _gridX, int _gridZ)
    {
        isWalkable = _isWalkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridZ = _gridZ;
    }
}