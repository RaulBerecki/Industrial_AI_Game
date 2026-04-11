using UnityEngine;

public class PlacementSystem : MonoBehaviour {
    [Header("Referințe Sisteme")]
    public GridManager gridManager;
    public LevelGenerator levelGenerator;

    [Header("Prefab-uri")]
    public GameObject robotPrefab;    
    public GameObject conveyorPrefab; 
    public GameObject arrowPrefab;    

    [Header("Costuri")]
    public int conveyorCost = 100;
    public int robotCost = 2000;

    [Header("Setări Hologramă")]
    public Material ghostMaterial;

    private GameObject ghostInstance, currentPrefab;
    private int currentID = -1; 
    private float currentRotation = 0f;

    void Update() {
        if (currentID == -1) return;

        if (currentID == -2) {
            HandleDeleteBehavior();
        } else {
            HandleGhostBehavior();
            if (Input.GetKeyDown(KeyCode.R)) { 
                currentRotation += 90f; 
                if(ghostInstance) ghostInstance.transform.rotation = Quaternion.Euler(0, currentRotation, 0);
            }
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Deselect();
    }

    public void SelectRobot() => SetSelection(8, robotPrefab);
    public void SelectConveyor() => SetSelection(9, conveyorPrefab);
    public void SelectDeleteMode() { Deselect(); currentID = -2; }

    void SetSelection(int id, GameObject p) {
        Deselect(); currentID = id; currentPrefab = p;
        ghostInstance = Instantiate(p);
        if (ghostInstance.GetComponent<Collider>()) ghostInstance.GetComponent<Collider>().enabled = false;
        foreach (MeshRenderer r in ghostInstance.GetComponentsInChildren<MeshRenderer>()) r.material = ghostMaterial;
        
        if (id == 9 && arrowPrefab) {
            GameObject arrow = Instantiate(arrowPrefab, ghostInstance.transform);
            arrow.transform.localPosition = new Vector3(0, 0.6f, 0);
            arrow.name = "DirectionArrow";
        }
    }

    void HandleDeleteBehavior() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            int x = Mathf.FloorToInt(hit.point.x / gridManager.cellSize);
            int z = Mathf.FloorToInt(hit.point.z / gridManager.cellSize);

            if (x >= 0 && x < 25 && z >= 0 && z < 25) {
                GameObject obj = levelGenerator.GetSpawnedObjectAt(x, z);
                int targetID = levelGenerator.GetIDAt(x, z);

                if (obj != null && !obj.name.StartsWith("[Static]") && Input.GetMouseButtonDown(0)) {
                    // RESTITUIRE BANI
                    int refund = (targetID == 8) ? robotCost : conveyorCost;
                    levelGenerator.finalBudget += refund;
                    levelGenerator.UpdateBudgetUI();

                    Destroy(obj);
                    levelGenerator.SetIDAt(x, z, 1, null);
                }
            }
        }
    }

    void HandleGhostBehavior() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            int x = Mathf.FloorToInt(hit.point.x / gridManager.cellSize);
            int z = Mathf.FloorToInt(hit.point.z / gridManager.cellSize);

            if (x >= 0 && x < 25 && z >= 0 && z < 25) {
                ghostInstance.transform.position = gridManager.GetWorldPosition(x, z);
                bool canPlace = levelGenerator.GetIDAt(x, z) == 1;

                // 1. Verificare reguli construcție
                if (canPlace && currentID == 9 && IsPerpendicularToNeighbors(x, z)) canPlace = false;
                if (canPlace && !CheckRobotNeighborLimit(x, z)) canPlace = false;

                // 2. VERIFICARE BUGET (Blocăm dacă nu sunt bani)
                int cost = (currentID == 8) ? robotCost : conveyorCost;
                if (canPlace && levelGenerator.finalBudget < cost) canPlace = false;

                Color c = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
                foreach (MeshRenderer r in ghostInstance.GetComponentsInChildren<MeshRenderer>()) {
                    if (r.gameObject.name != "DirectionArrow") r.material.SetColor("_BaseColor", c);
                }

                if (canPlace && Input.GetMouseButtonDown(0)) Place(x, z, cost);
            }
        }
    }

    // ... IsPerpendicularToNeighbors și CheckRobotNeighborLimit rămân la fel ...
    bool IsPerpendicularToNeighbors(int x, int z) {
        float myRot = currentRotation % 360;
        int[] dx = {0, 0, 1, -1}; int[] dz = {1, -1, 0, 0};
        for (int i = 0; i < 4; i++) {
            GameObject n = levelGenerator.GetSpawnedObjectAt(x + dx[i], z + dz[i]);
            if (n != null && levelGenerator.GetIDAt(x + dx[i], z + dz[i]) == 9) {
                float d = Mathf.Abs(myRot - (n.transform.eulerAngles.y % 360));
                if (Mathf.Approximately(d, 90) || Mathf.Approximately(d, 270)) return true;
            }
        }
        return false;
    }

    bool CheckRobotNeighborLimit(int x, int z) {
        int[] dx = {0,0,1,-1}, dz = {1,-1,0,0};
        if (currentID == 8) {
            int count = 0;
            for (int i=0; i<4; i++) if (levelGenerator.GetIDAt(x+dx[i], z+dz[i]) == 9) count++;
            return count <= 2;
        }
        if (currentID == 9) {
            for (int i=0; i<4; i++) {
                int nx = x+dx[i], nz = z+dz[i];
                if (levelGenerator.GetIDAt(nx, nz) == 8) {
                    int count = 0;
                    for (int j=0; j<4; j++) if (levelGenerator.GetIDAt(nx+dx[j], nz+dz[j]) == 9) count++;
                    if (count >= 2) return false;
                }
            }
        }
        return true;
    }

    void Place(int x, int z, int cost) {
        // SCĂDEM DIN BUGET
        levelGenerator.finalBudget -= cost;
        levelGenerator.UpdateBudgetUI();

        GameObject go = Instantiate(currentPrefab, ghostInstance.transform.position, ghostInstance.transform.rotation);
        go.transform.SetParent(GameObject.Find("Interactable Objects").transform);
        levelGenerator.SetIDAt(x, z, currentID, go);
    }

    public void Deselect() { currentID = -1; if (ghostInstance) Destroy(ghostInstance); }
}