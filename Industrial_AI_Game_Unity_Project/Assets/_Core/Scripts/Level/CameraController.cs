using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Mișcare WASD")]
    public float moveSpeed = 15f;
    public float fastMoveMultiplier = 2f;

    [Header("Rotație (Click Dreapta)")]
    public float lookSensitivity = 2.5f;
    [Tooltip("Unghiul vertical va rămâne fix la această valoare.")]
    [Range(20f, 50f)] public float fixedPitch = 45f; 

    [Header("Smooth Zoom (Rotiță)")]
    public float zoomSensitivity = 1.5f;
    public float zoomSmoothness = 8f; 
    public float minHeight = 10f;
    public float maxHeight = 20f;

    private float rotationY = 0f;
    private float targetHeight = 15f; 

    void Update()
    {
        HandleRotation();
        HandleMovement();
        HandleSmoothZoom();
    }

    void HandleMovement()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMoveMultiplier;

        float moveX = Input.GetAxis("Horizontal"); 
        float moveZ = Input.GetAxis("Vertical");   

        // Direcții relative la orientarea camerei pe plan orizontal
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0;
        right.Normalize();

        Vector3 direction = (forward * moveZ + right * moveX).normalized;
        transform.position += direction * speed * Time.deltaTime;
    }

    void HandleRotation()
    {
        // Rotim doar dacă ținem apăsat Click Dreapta
        if (Input.GetMouseButton(1))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Modificăm DOAR rotationY bazat pe mișcarea mouse-ului pe axa X
            rotationY += Input.GetAxis("Mouse X") * lookSensitivity;

            // Aplicăm rotația: fixedPitch rămâne neschimbat, rotationY se actualizează
            transform.localRotation = Quaternion.Euler(fixedPitch, rotationY, 0);
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    void HandleSmoothZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            targetHeight -= scroll * zoomSensitivity * 10f; 
            targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);
        }

        float currentHeight = transform.position.y;
        float newHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * zoomSmoothness);
        
        Vector3 pos = transform.position;
        pos.y = newHeight;
        transform.position = pos;
    }

    public void CenterOnLevel(Vector3 realCenter, float hallSize)
    {
        // Când se generează nivelul, resetăm rotația orizontală
        rotationY = 0f;
        
        // Unghiul vertical este cel setat în Inspector (fixedPitch)
        transform.localRotation = Quaternion.Euler(fixedPitch, rotationY, 0);

        targetHeight = 15f; 

        // Calculăm distanța Z bazat pe fixedPitch pentru a vedea centrul
        float zOffset = 15f / Mathf.Tan(fixedPitch * Mathf.Deg2Rad);

        transform.position = realCenter + new Vector3(0, 15f, -zOffset);
        
        Debug.Log($"<color=orange>[Camera] Centrată la Y=15, unghi fix pe X: {fixedPitch}°</color>");
    }
}