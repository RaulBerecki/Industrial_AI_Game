using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro; // Necesar pentru afișarea bugetului

[System.Serializable]
public class MapResponse {
    public int width;
    public int height;
    public int[] data;          // Matricea halei (pereți, podea, roboți statici)
    public int[] ai_solution;   // Matricea cu traseul optim (Roz/Galben)
    public float base_budget;   // Bugetul brut de la Python
    public int paid_conveyors;  // Nr piese numărate de AI
    public int paid_robots;     // Nr roboți numărați de AI
}

public class LevelGenerator : MonoBehaviour {
    [Header("Referințe UI & Sisteme")]
    public GridManager gridManager; 
    public CameraController cameraController;
    public TextMeshProUGUI budgetText; 

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
    public GameObject conveyorPrefab;   

    [Header("Prefab-uri Exterior & Tavan")]
    public GameObject surroundingPrefab; 
    public float surroundingRadius = 100f;
    public float surroundingY = 3f; 

    [Header("Stare Curentă (Date AI)")]
    public float finalBudget;           // Bugetul jucătorului (Base + 30% + Rotunjire)
    private int[,] mapGrid;             // Matricea fizică a halei
    private int[,] aiSolutionGrid;      // Salvează rezolvarea optimă
    private GameObject[,] spawnedObjects; // Referințele obiectelor din scenă
    
    private Transform interactableParent;
    private Transform exteriorParent;

    void Start() {
        if (gridManager == null) {
            Debug.LogError("GridManager nu este asignat pe LevelGenerator!");
            return;
        }
        spawnedObjects = new GameObject[25, 25];
        UpdateBudgetUI();
        GenerateNewLevel();
    }

    // --- METODE ACCES DATE ---

    public int GetIDAt(int x, int z) {
        if (mapGrid == null || x < 0 || x >= 25 || z < 0 || z >= 25) return -1;
        return mapGrid[z, x];
    }

    public int GetAISolutionAt(int x, int z) {
        if (aiSolutionGrid == null || x < 0 || x >= 25 || z < 0 || z >= 25) return 0;
        return aiSolutionGrid[z, x];
    }

    public GameObject GetSpawnedObjectAt(int x, int z) {
        if (x < 0 || x >= 25 || z < 0 || z >= 25) return null;
        return spawnedObjects[z, x];
    }

    public void SetIDAt(int x, int z, int newID, GameObject obj = null) {
        if (mapGrid == null || x < 0 || x >= 25 || z < 0 || z >= 25) return;
        mapGrid[z, x] = newID;
        spawnedObjects[z, x] = obj;
        gridManager.GenerateGrid(); 
    }

    // Actualizează textul din Canvas
    public void UpdateBudgetUI() {
        if (budgetText != null) {
            budgetText.text = $"Buget: {Mathf.FloorToInt(finalBudget)} Cr";
        }
    }

    // --- LOGICĂ GENERARE ---

    public void GenerateNewLevel() {
        foreach (Transform child in transform) Destroy(child.gameObject);
        
        spawnedObjects = new GameObject[25, 25];
        interactableParent = new GameObject("Interactable Objects").transform;
        interactableParent.SetParent(this.transform);
        exteriorParent = new GameObject("Exterior World").transform;
        exteriorParent.SetParent(this.transform);

        StartCoroutine(GetMapData());
    }

    IEnumerator GetMapData() {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url)) {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success) {
                MapResponse response = JsonUtility.FromJson<MapResponse>(webRequest.downloadHandler.text);
                
                gridManager.width = response.width;
                gridManager.height = response.height;
                
                // --- LOGICĂ CALCUL BUGET + ROTUNJIRE ---
                float rawBudget = response.base_budget * 1.30f; // Adăugăm 30%
                finalBudget = Mathf.Ceil(rawBudget / 100f) * 100f; // Rotunjim în sus la prag de 100
                
                UpdateBudgetUI();

                // Procesare Matrice Hală
                mapGrid = new int[25, 25];
                for (int y = 0; y < 25; y++)
                    for (int x = 0; x < 25; x++)
                        mapGrid[y, x] = response.data[y * 25 + x];

                // Procesare Matrice Soluție AI
                aiSolutionGrid = new int[25, 25];
                if (response.ai_solution != null) {
                    for (int y = 0; y < 25; y++)
                        for (int x = 0; x < 25; x++)
                            aiSolutionGrid[y, x] = response.ai_solution[y * 25 + x];
                }

                BuildLevel(25, 25);
            } else {
                Debug.LogError("Eroare Server: " + webRequest.error);
            }
        }
    }

    void BuildLevel(int width, int height) {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

        for (int z = 0; z < height; z++) {
            for (int x = 0; x < width; x++) {
                int id = mapGrid[z, x];
                Vector3 position = gridManager.GetWorldPosition(x, z);

                if (id == 0) {
                    if (surroundingPrefab) Instantiate(surroundingPrefab, new Vector3(position.x, surroundingY, position.z), Quaternion.identity).transform.SetParent(exteriorParent);
                    continue;
                }
                if (id == 7) continue;

                minX = Mathf.Min(minX, position.x); maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z); maxZ = Mathf.Max(maxZ, position.z);

                if (id == 5 || id == 6) {
                    Instantiate(floorPrefab, position, Quaternion.identity).transform.SetParent(this.transform);
                    Quaternion doorRot = CalculateRotation(z, x, id);
                    GameObject dPrefab = (id == 5) ? doorInPrefab : doorOutPrefab;
                    if (dPrefab) Instantiate(dPrefab, position, doorRot).transform.SetParent(this.transform);
                    PlaceSingleConveyorInFront(x, z, doorRot);
                    continue;
                }

                if (id == 8 || id == 9) {
                    if (spawnedObjects[z, x] != null) continue;

                    Instantiate(floorPrefab, position, Quaternion.identity).transform.SetParent(this.transform);
                    GameObject prefab = (id == 8) ? robotArmPrefab : conveyorPrefab;
                    
                    if (prefab) {
                        Quaternion rot = Quaternion.Euler(0, Random.Range(0, 4) * 90, 0);
                        GameObject go = Instantiate(prefab, position, rot);
                        go.transform.SetParent(interactableParent);
                        go.name = "[Static] " + prefab.name; 
                        spawnedObjects[z, x] = go;
                    }
                    continue;
                }

                GameObject structPrefab = GetPrefabByID(id);
                if (structPrefab) {
                    Quaternion rotation = CalculateRotation(z, x, id);
                    Instantiate(structPrefab, position, rotation).transform.SetParent(this.transform);
                }
            }
        }

        GenerateMassiveExterior(width, height);
        gridManager.GenerateGrid();
        if (cameraController) cameraController.CenterOnLevel(new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f), Mathf.Max(maxX - minX, maxZ - minZ));
    }

    void PlaceSingleConveyorInFront(int x, int z, Quaternion doorRot) {
        int tx = x, tz = z;
        if (IsWalkable(z + 1, x)) tz++;
        else if (IsWalkable(z - 1, x)) tz--;
        else if (IsWalkable(z, x - 1)) tx--;
        else if (IsWalkable(z, x + 1)) tx++;

        mapGrid[tz, tx] = 9; 
        Vector3 fPos = gridManager.GetWorldPosition(tx, tz);
        Quaternion fRot = doorRot * Quaternion.Euler(0, 90f, 0); 
        GameObject conv = Instantiate(conveyorPrefab, fPos, fRot);
        conv.transform.SetParent(interactableParent);
        conv.name = "[Static] Door_Conveyor"; 
        spawnedObjects[tz, tx] = conv;
    }

    // --- METODE UTILS ---

    void GenerateMassiveExterior(int w, int h) {
        if (surroundingPrefab == null) return;
        float cs = gridManager.cellSize; float r = surroundingRadius;
        float tw = w * cs; float th = h * cs;
        CreateScaledBlock(new Vector3(tw/2f, surroundingY, -r/2f), new Vector3(tw + 2*r, 1, r));
        CreateScaledBlock(new Vector3(tw/2f, surroundingY, th + r/2f), new Vector3(tw + 2*r, 1, r));
        CreateScaledBlock(new Vector3(-r/2f, surroundingY, th/2f), new Vector3(r, 1, th));
        CreateScaledBlock(new Vector3(tw + r/2f, surroundingY, th/2f), new Vector3(r, 1, th));
    }

    void CreateScaledBlock(Vector3 p, Vector3 s) {
        GameObject b = Instantiate(surroundingPrefab, p, Quaternion.identity);
        b.transform.SetParent(exteriorParent); b.transform.localScale = s; 
    }

    GameObject GetPrefabByID(int id) {
        return id switch { 1 => floorPrefab, 2 => wallPrefab, 3 => cornerExtPrefab, 4 => cornerIntPrefab, _ => null };
    }

    Quaternion CalculateRotation(int z, int x, int id) {
        if (id == 2 || id == 5 || id == 6) {
            if (IsWalkable(z + 1, x)) return Quaternion.Euler(0, 180, 0); 
            if (IsWalkable(z - 1, x)) return Quaternion.Euler(0, 0, 0); 
            if (IsWalkable(z, x - 1)) return Quaternion.Euler(0, 90, 0); 
            return Quaternion.Euler(0, 270, 0); 
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
        int id = mapGrid[z, x];
        return id == 1 || id == 8 || id == 9; 
    }
}