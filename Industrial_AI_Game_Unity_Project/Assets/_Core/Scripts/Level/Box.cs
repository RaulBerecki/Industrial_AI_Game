using UnityEngine;

public class Box : MonoBehaviour {
    public float speed = 2.0f;
    public float detectionDistance = 1.1f; // Distanța de detecție față de obiectul din față
    
    private bool isStopped = false;
    private Rigidbody rb;

    void Start() {
        rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true; // Forțăm kinematic din cod
        }
        
        // Ne asigurăm că tag-ul este setat pentru logica de coadă
        this.gameObject.tag = "Box"; 
    }

    void Update() {
        // 1. Verificăm dacă avem obstacol în față (Robot sau altă Cutie oprită)
        CheckForObstacles();

        // 2. Dacă nu suntem opriți, ne mișcăm pe bandă
        if (!isStopped) {
            MoveOnConveyor();
        }
    }

    void MoveOnConveyor() {
        RaycastHit hitDown;
        // Detectăm banda de sub noi
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hitDown, 1.0f)) {
            if (hitDown.transform.name.Contains("Conveyor") || hitDown.transform.name.Contains("Door_Conveyor")) {
                
                // Direcția: axa X pozitivă a benzii (cum ai cerut)
                Vector3 moveDirection = hitDown.transform.right; 

                // Mișcare kinematic (simplă translație)
                transform.position += moveDirection * speed * Time.deltaTime;

                // Aliniem rotația cutiei cu direcția de mers
                Quaternion targetRot = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }
    }

    void CheckForObstacles() {
        RaycastHit hitForward;
        // Ridicăm raza puțin ca să nu lovească banda
        Vector3 rayStart = transform.position + Vector3.up * 0.4f;
        Vector3 direction = transform.forward; // Cutia privește mereu spre unde merge datorită LookRotation

        // Debug vizual: Roșu = Stop, Verde = Mergi
        Debug.DrawRay(rayStart, direction * detectionDistance, isStopped ? Color.red : Color.green);

        if (Physics.Raycast(rayStart, direction, out hitForward, detectionDistance)) {
            // Cazul A: Vedem un Robot
            if (hitForward.transform.name.Contains("Robot")) {
                isStopped = true;
                return;
            }

            // Cazul B: Vedem o altă Cutie
            if (hitForward.transform.CompareTag("Box")) {
                Box otherBox = hitForward.transform.GetComponent<Box>();
                // Dacă cutia din față este oprită, ne oprim și noi
                if (otherBox != null && otherBox.IsStopped()) {
                    isStopped = true;
                    return;
                }
            }
        }

        // Dacă nu e nimic în drum, pornim
        isStopped = false;
    }

    public bool IsStopped() {
        return isStopped;
    }

    public bool IsFullyStopped() {
    return isStopped; 
}

    // Metodă pentru robot: Când robotul ia cutia, cealaltă poate pleca
    public void ResumeMovement() {
        isStopped = false;
    }
}