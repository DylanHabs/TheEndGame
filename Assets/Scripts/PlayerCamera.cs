using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    public float sensitivity = 0.1f;
    public float distance;
    private Transform head;
    public Vector3 PlanarDirection { get; set; }
    public LayerMask ignoreLayers;
    private Camera cam;
    private Quaternion currentHeadRot;
    private Vector3 currentCamPos;
    private Vector2 rot = Vector2.zero;

    InputAction lookAction;
    void Start()
    {
        cam = Camera.main;
        lookAction = InputSystem.actions.FindAction("Look");
        //Cursor.lockState = CursorLockMode.Locked;
    }

    // Set the transform that the camera will orbit around
    public void SetFollowTransform(Transform t)
    {
        head = t;
    }

    void LateUpdate()
    {
        if (head == null) //do nothin
            return;

        Vector2 lookValue = lookAction.ReadValue<Vector2>();
        lookValue *= sensitivity;
        rot += lookValue;
        rot.y = Mathf.Clamp(rot.y, -90f, 90f);

        // Target head rotation
        Quaternion targetHeadRot = Quaternion.Euler(-rot.y, rot.x, 0f);
        head.rotation = targetHeadRot;

        // Camera follow with raycast-based shoulder collision
        Vector3 targetCamPos;
        RaycastHit hit;
        if (Physics.Raycast(head.position, -head.forward, out hit, distance, ~ignoreLayers))
        {
            targetCamPos = hit.point;
        }
        else
        {
            targetCamPos = head.position - head.forward * distance;
        }

        transform.position = targetCamPos;

        transform.rotation = head.rotation;
    }
}
