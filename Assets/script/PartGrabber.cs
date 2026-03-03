using UnityEngine;

public class PartGrabber : MonoBehaviour
{
    [Header("Grab Settings")]
    public float grabRange = 5f;
    public float holdDistance = 2f;
    public float moveSpeed = 15f;
    public LayerMask grabbableLayer;

    [Header("References")]
    public Camera playerCamera;

    private GameObject grabbedObject;
    private Rigidbody grabbedRigidbody;
    private bool isGrabbing = false;
    private SnapToPlace currentSnapZone;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryGrab();

        if (Input.GetMouseButtonUp(0) && isGrabbing)
            Release();

        if (isGrabbing && grabbedObject != null)
            MoveGrabbedObject();
    }

    void TryGrab()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, grabRange, grabbableLayer))
        {
            if (hit.collider.CompareTag("Grabbable"))
            {
                grabbedObject = hit.collider.gameObject;
                grabbedRigidbody = grabbedObject.GetComponent<Rigidbody>();

                if (grabbedRigidbody != null)
                {
                    grabbedRigidbody.useGravity = false;
                    grabbedRigidbody.linearVelocity = Vector3.zero;
                    grabbedRigidbody.angularVelocity = Vector3.zero;
                }

                isGrabbing = true;
                NotifySnapZones(true);
            }
        }
    }

    void MoveGrabbedObject()
    {
        Vector3 targetPos = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
        
        if (grabbedRigidbody != null)
        {
            Vector3 newPos = Vector3.Lerp(grabbedObject.transform.position, targetPos, Time.deltaTime * moveSpeed);
            grabbedRigidbody.MovePosition(newPos);
        }
        else
        {
            grabbedObject.transform.position = Vector3.Lerp(grabbedObject.transform.position, targetPos, Time.deltaTime * moveSpeed);
        }

        CheckSnapZones();
    }

    void CheckSnapZones()
    {
        SnapToPlace[] snapZones = FindObjectsOfType<SnapToPlace>();
        foreach (SnapToPlace zone in snapZones)
        {
            float dist = Vector3.Distance(grabbedObject.transform.position, zone.snapPoint.position);
            if (dist <= zone.snapRange)
            {
                if (currentSnapZone != zone)
                {
                    if (currentSnapZone != null)
                        currentSnapZone.OnObjectGrabbed(false);
                    currentSnapZone = zone;
                    currentSnapZone.OnObjectGrabbed(true);
                }
                return; // only one zone at a time
            }
        }
        // Not near any zone
        if (currentSnapZone != null)
        {
            currentSnapZone.OnObjectGrabbed(false);
            currentSnapZone = null;
        }
    }

    void Release()
    {
        if (grabbedObject != null)
        {
            if (currentSnapZone != null)
                currentSnapZone.TrySnapObject(grabbedObject);
            else
            {
                // No snap: restore gravity
                if (grabbedRigidbody != null)
                    grabbedRigidbody.useGravity = true;
            }
        }

        NotifySnapZones(false);
        isGrabbing = false;
        grabbedObject = null;
        grabbedRigidbody = null;
        currentSnapZone = null;
    }

    void NotifySnapZones(bool state)
    {
        SnapToPlace[] zones = FindObjectsOfType<SnapToPlace>();
        foreach (SnapToPlace zone in zones)
            zone.OnObjectGrabbed(state);
    }
}