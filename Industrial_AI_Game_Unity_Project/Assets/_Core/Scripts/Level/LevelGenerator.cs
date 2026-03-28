using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class MapResponse {
    public int width;
    public int height;
    public int[] data;
}

public class LevelGenerator : MonoBehaviour {
    [Header("Referințe Sisteme")]
    public GridManager gridManager; 
    public CameraController cameraController;

    [Header("Configurație Server")]
    public string url = "http://127.0.0.1:8000/generate";

    [Header("Prefab-uri Structură Hală")]
    public GameObject floorPrefab;      
    public GameObject wallPrefab;       
    public GameObject cornerExtPrefab;  
    public GameObject cornerIntPrefab;  
    public GameObject doorInPrefab;     
    public GameObject doorOutPrefab;    

    [Header("Prefab-uri Interior")]
    public GameObject robotArmPrefab;   

    [Header("Prefab-uri Exterior & Tavan")]
    public GameObject surroundingPrefab; 
    public float surroundingRadius = 100f;
    public float surroundingY = 3f; // Fixat la 3 conform cerinței

    private int[,] mapGrid;
    private Transform interactableParent;
    private Transform exteriorParent;

    void Start() {
        if (gridManager == null) return;
        GenerateNewLevel();
    }

    public void GenerateNewLevel() {
        foreach (Transform child in transform) Destroy(child.gameObject);

        GameObject interactableFolder = new GameObject("Interactable Objects");
        interactableFolder.transform.SetParent(this.transform);
        interactableParent = interactableFolder.transform;

        GameObject exteriorFolder = new GameObject("Exterior World");
        exteriorFolder.transform.SetParent(this.transform);
        exteriorParent = exteriorFolder.transform;

        StartCoroutine(GetMapData());
    }

    IEnumerator GetMapData() {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url)) {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success) {
                MapResponse response = JsonUtility.FromJson<MapResponse>(webRequest.downloadHandler.text);
                gridManager.width = response.width;
                gridManager.height = response.height;
                mapGrid = new int[response.height, response.width];
                for (int y = 0; y < response.height; y++) {
                    for (int x = 0; x < response.width; x++) {
                        mapGrid[y, x] = response.data[y * response.width + x];
                    }
                }
                BuildLevel(response.width, response.height);
            }
        }
    }

    void BuildLevel(int width, int height) {
        int spawnCount = 0;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        for (int z = 0; z < height; z++) {
            for (int x = 0; x < width; x++) {
                int id = mapGrid[z, x];
                Vector3 position = gridManager.GetWorldPosition(x, z);

                // LOGICA PENTRU ID 0 (Tavan/Exterior în grid) -> Forțăm Y=3
                if (id == 0) {
                    if (surroundingPrefab != null) {
                        Vector3 ceilingPos = new Vector3(position.x, surroundingY, position.z);
                        Instantiate(surroundingPrefab, ceilingPos, Quaternion.identity).transform.SetParent(exteriorParent);
                    }
                    continue;
                }

                if (id == 7) continue;

                // Actualizăm marginile pentru cameră
                if (position.x < minX) minX = position.x;
                if (position.x > maxX) maxX = position.x;
                if (position.z < minZ) minZ = position.z;
                if (position.z > maxZ) maxZ = position.z;

                if (id == 8) {
                    Instantiate(floorPrefab, position, Quaternion.identity).transform.SetParent(this.transform);
                    if (robotArmPrefab != null) {
                        Quaternion robotRot = Quaternion.Euler(0, Random.Range(0, 4) * 90, 0);
                        GameObject robot = Instantiate(robotArmPrefab, position, robotRot);
                        robot.transform.SetParent(interactableParent);
                    }
                    spawnCount++;
                    continue;
                }

                GameObject prefab = GetPrefabByID(id);
                if (prefab != null) {
                    Quaternion rotation = CalculateRotation(z, x, id);
                    GameObject go = Instantiate(prefab, position, rotation);
                    go.transform.SetParent(this.transform);
                    spawnCount++;
                }
            }
        }

        // GENERARE RAMĂ EXTERIOARĂ MASIVĂ la Y=3
        GenerateMassiveExterior(width, height);

        gridManager.GenerateGrid();

        Vector3 realCenter = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
        float hallSize = Mathf.Max(maxX - minX, maxZ - minZ);
        if (cameraController != null) cameraController.CenterOnLevel(realCenter, hallSize);
    }

    void GenerateMassiveExterior(int gridW, int gridH) {
        if (surroundingPrefab == null) return;
        float cSize = gridManager.cellSize;
        float rad = surroundingRadius;
        float totalGridW = gridW * cSize;
        float totalGridH = gridH * cSize;

        // Cele 4 blocuri mari, toate poziționate la surroundingY (Y=3)
        CreateScaledBlock(new Vector3(totalGridW / 2f, surroundingY, -rad / 2f), new Vector3(totalGridW + 2 * rad, 1, rad));
        CreateScaledBlock(new Vector3(totalGridW / 2f, surroundingY, totalGridH + rad / 2f), new Vector3(totalGridW + 2 * rad, 1, rad));
        CreateScaledBlock(new Vector3(-rad / 2f, surroundingY, totalGridH / 2f), new Vector3(rad, 1, totalGridH));
        CreateScaledBlock(new Vector3(totalGridW + rad / 2f, surroundingY, totalGridH / 2f), new Vector3(rad, 1, totalGridH));
    }

    void CreateScaledBlock(Vector3 position, Vector3 scale) {
        GameObject block = Instantiate(surroundingPrefab, position, Quaternion.identity);
        block.transform.SetParent(exteriorParent);
        block.transform.localScale = scale; 
    }

    GameObject GetPrefabByID(int id) {
        return id switch {
            1 => floorPrefab, 2 => wallPrefab, 3 => cornerExtPrefab,
            4 => cornerIntPrefab, 5 => doorInPrefab, 6 => doorOutPrefab,
            _ => null
        };
    }

    Quaternion CalculateRotation(int z, int x, int id) {
        if (id == 2 || id == 5 || id == 6) {
            if (IsWalkable(z + 1, x)) return Quaternion.Euler(0, 180, 0); 
            if (IsWalkable(z - 1, x)) return Quaternion.Euler(0, 0, 0); 
            if (IsWalkable(z, x - 1)) return Quaternion.Euler(0, 90, 0); 
            if (IsWalkable(z, x + 1)) return Quaternion.Euler(0, 270, 0); 
        }
        if (id == 3) {
            if (IsWalkable(z + 1, x + 1)) return Quaternion.Euler(0, 270, 0);
            if (IsWalkable(z + 1, x - 1)) return Quaternion.Euler(0, 180, 0);
            if (IsWalkable(z - 1, x + 1)) return Quaternion.Euler(0, 0, 0);
            if (IsWalkable(z - 1, x - 1)) return Quaternion.Euler(0, 90, 0);
        }
        if (id == 4) {
            if (!IsWalkable(z + 1, x + 1)) return Quaternion.Euler(0, 90, 0);
            if (!IsWalkable(z + 1, x - 1)) return Quaternion.Euler(0, 0, 0);
            if (!IsWalkable(z - 1, x + 1)) return Quaternion.Euler(0, 180, 0);
            if (!IsWalkable(z - 1, x - 1)) return Quaternion.Euler(0, 270, 0);
        }
        return Quaternion.identity;
    }

    bool IsWalkable(int z, int x) {
        if (z < 0 || z >= 25 || x < 0 || x >= 25) return false;
        return mapGrid[z, x] == 1 || mapGrid[z, x] == 8;
    }
}