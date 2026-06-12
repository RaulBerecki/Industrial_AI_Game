using UnityEngine;
using System.Collections;

public class RobotController : MonoBehaviour {
    [Header("Referințe Sisteme")]
    public LevelGenerator levelGenerator;

    [Header("Setări Înălțime și Distanță")]
    [SerializeField] private float delayBeforeTeleport = 1.0f; // Timp de așteptare (secunde)
    [SerializeField] private float boxHeight = 0.8f;            // Înălțimea la care plutește cutia
    [SerializeField] private float rayHeightForBox = 0.85f;     // Înălțimea razei care caută cutia
    [SerializeField] private float rayHeightForConveyor = 0.1f; // Înălțimea razei care caută benzile

    private bool isProcessing = false;
    private Vector3 inputDirection;
    private Vector3 outputDirection;
    private Vector3 targetPlacementPosition;
    private Box currentBox;

    void Start() {
        // Încercăm să găsim automat LevelGenerator în scenă dacă nu a fost legat manual
        if (levelGenerator == null) {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }
    }

    void Update() {
        // Robotul nu scanează dacă procesează deja o cutie sau dacă nu are acces la grid
        if (isProcessing || levelGenerator == null) return;
        
        ScanForIncomingBox();
    }

    /// Scanează cele 4 direcții din jurul robotului din centrul celulei sale de grid.
    void ScanForIncomingBox() {
        Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };
        float scanDistance = levelGenerator.gridManager.cellSize * 1.2f;
        Vector3 tileCenter = GetCurrentTileCenter();
        
        Vector3 rayStart = new Vector3(tileCenter.x, rayHeightForBox, tileCenter.z);

        foreach (Vector3 dir in directions) {
            RaycastHit hit;
            if (Physics.Raycast(rayStart, dir, out hit, scanDistance)) {
                
                // Ignorăm coliziunile accidentale cu robotul însuși
                if (hit.transform.gameObject == this.gameObject) continue;

                // Verificăm dacă obiectul lovit este o cutie validă și oprită
                if (IsObjectABox(hit.transform)) {
                    Box box = hit.transform.GetComponent<Box>();
                    
                    if (box != null && box.IsFullyStopped()) {
                        currentBox = box;
                        inputDirection = dir; // Salvăm direcția de unde vine cutia

                        // Dacă găsim o bandă validă de ieșire, pornim teleportarea
                        if (FindValidOutputConveyor(tileCenter, scanDistance)) {
                            StartCoroutine(TeleportBoxRoutine());
                        }
                        return;
                    }
                }
            }
        }
    }

    /// Căutăm o bandă vecină (în afară de cea de intrare) unde direcția X+ coincide cu fluxul robotului.
    bool FindValidOutputConveyor(Vector3 tileCenter, float scanDistance) {
        Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };
        Vector3 rayStart = new Vector3(tileCenter.x, transform.position.y + rayHeightForConveyor, tileCenter.z);

        foreach (Vector3 dir in directions) {
            if (dir == inputDirection) continue; // Nu trimitem cutia înapoi de unde a venit

            RaycastHit hit;
            if (Physics.Raycast(rayStart, dir, out hit, scanDistance)) {
                if (hit.transform.name.Contains("Conveyor") || hit.transform.name.Contains("Door_Conveyor")) {
                    
                    // Verificăm dacă banda este orientată în exterior (Dot Product > 0.7)
                    if (Vector3.Dot(dir, hit.transform.right) > 0.7f) {
                        outputDirection = dir;
                        // Calculăm poziția finală unde va fi teleportată cutia
                        targetPlacementPosition = new Vector3(hit.transform.position.x, boxHeight, hit.transform.position.z);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// Corutină care oprește procesul timp de 1 secundă și apoi mută instant cutia.
    IEnumerator TeleportBoxRoutine() {
        isProcessing = true;
        
        yield return new WaitForSeconds(delayBeforeTeleport);

        if (currentBox != null) {
            // Teleportare fizică și aliniere rotație cu noua direcție a benzii
            currentBox.transform.position = targetPlacementPosition;
            currentBox.transform.rotation = Quaternion.LookRotation(outputDirection);
            
            // Îi redăm cutiei permisiunea de a se mișca prin scriptul ei
            currentBox.ResumeMovement(); 
        }

        currentBox = null;
        isProcessing = false;
    }

    #region Funcții Ajutătoare (Helper Methods)

    private Vector3 GetCurrentTileCenter() {
        int robotX = Mathf.FloorToInt(transform.position.x / levelGenerator.gridManager.cellSize);
        int robotZ = Mathf.FloorToInt(transform.position.z / levelGenerator.gridManager.cellSize);
        return levelGenerator.gridManager.GetWorldPosition(robotX, robotZ);
    }

    private bool IsObjectABox(Transform target) {
        return target.CompareTag("Box") || target.name.Contains("Box") || target.name.Contains("Cube");
    }

    #endregion
}