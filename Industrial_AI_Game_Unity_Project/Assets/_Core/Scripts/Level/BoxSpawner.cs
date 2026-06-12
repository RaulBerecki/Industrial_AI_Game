using UnityEngine;
using System.Collections;

public class BoxSpawner : MonoBehaviour {
    [Header("Referințe")]
    public LevelGenerator levelGenerator;
    public GameObject boxPrefab;

    [Header("Setări Spawning")]
    public float spawnInterval = 2.0f;
    public Vector3 boxOffset = new Vector3(0, 0.5f, 0); 

    private Vector3 spawnPosition;
    private bool canSpawn = false;
    private bool isProductionStarted = false; // Flag pentru butonul de Start

    void Start() {
        // Doar inițializăm căutarea punctului de spawn, dar NU pornim producția
        StartCoroutine(InitializeSpawner());
    }

    IEnumerator InitializeSpawner() {
        // Așteptăm să se genereze harta
        while (levelGenerator.GetIDAt(0,0) == -1) {
            yield return new WaitForSeconds(0.5f);
        }
        
        // Găsim punctul unde vor apărea cutiile
        FindActualConveyorSpawnPoint();
    }

    // Această metodă va fi apelată de butonul de START din UI
    public void StartProduction() {
        if (canSpawn && !isProductionStarted) {
            isProductionStarted = true;
            StartCoroutine(SpawnRoutine());
            Debug.Log("<color=green>Producția a început!</color>");
        } else if (!canSpawn) {
            Debug.LogWarning("Nu pot porni! Punctul de spawn nu a fost găsit.");
        }
    }

    void FindActualConveyorSpawnPoint() {
        int doorX = -1, doorZ = -1;

        // 1. Găsim ușa de intrare (ID 5)
        for (int z = 0; z < 25; z++) {
            for (int x = 0; x < 25; x++) {
                if (levelGenerator.GetIDAt(x, z) == 5) {
                    doorX = x;
                    doorZ = z;
                    break;
                }
            }
            if (doorX != -1) break;
        }

        if (doorX != -1) {
            // 2. Căutăm conveiorul static de lângă ușă (ID 9)
            int[] dx = { 0, 0, 1, -1 };
            int[] dz = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++) {
                int nx = doorX + dx[i];
                int nz = doorZ + dz[i];

                if (levelGenerator.GetIDAt(nx, nz) == 9) {
                    spawnPosition = levelGenerator.gridManager.GetWorldPosition(nx, nz) + boxOffset;
                    canSpawn = true;
                    return;
                }
            }
        }
    }

    IEnumerator SpawnRoutine() {
        while (isProductionStarted) {
            Instantiate(boxPrefab, spawnPosition, Quaternion.identity);
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    
    // Opțional: O metodă de STOP dacă vrei să oprești fabrica
    public void StopProduction() {
        isProductionStarted = false;
    }
}