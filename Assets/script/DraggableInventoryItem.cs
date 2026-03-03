using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DraggableInventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    public Image iconImage;
    public CanvasGroup canvasGroup;
    
    [Header("Drag Settings")]
    public LayerMask placementLayerMask = -1;
    public float dragDistance = 3f;
    public float dragHeight = 1f;
    
    private InventoryItemData itemData;
    private InventoryManager inventoryManager;
    private GameObject draggedObject;
    private Camera mainCamera;
    private bool isDragging;
    
    void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        mainCamera = Camera.main;
    }
    
    public void Initialize(InventoryItemData data, InventoryManager manager)
    {
        itemData = data;
        inventoryManager = manager;
        
        if (iconImage != null && data.itemIcon != null)
            iconImage.sprite = data.itemIcon;
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemData == null || inventoryManager == null)
            return;
        
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        
        Quaternion spawnRotation = Quaternion.identity;
        if (!string.IsNullOrEmpty(itemData.targetSnapZoneName))
        {
            SnapToPlace[] zones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
            foreach (SnapToPlace zone in zones)
            {
                if (ZoneNameMatches(zone.gameObject.name, itemData.targetSnapZoneName))
                {
                    spawnRotation = zone.snapPoint.rotation;
                    break;
                }
            }
        }
        
        Vector3 spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * dragDistance;
        draggedObject = Instantiate(itemData.prefabToSpawn, spawnPosition, spawnRotation);
        
        SetupDraggedObject(draggedObject);
        isDragging = true;
        
        if (InventoryAudioManager.Instance != null)
            InventoryAudioManager.Instance.PlayDragStart();
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || draggedObject == null)
            return;
        
        Ray ray = mainCamera.ScreenPointToRay(eventData.position);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 100f, placementLayerMask))
        {
            draggedObject.transform.position = hit.point + Vector3.up * dragHeight;
        }
        else
        {
            Vector3 targetPos = mainCamera.transform.position + mainCamera.transform.forward * dragDistance;
            draggedObject.transform.position = targetPos;
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (!isDragging || draggedObject == null || itemData == null || inventoryManager == null)
            return;

        isDragging = false;

        Rigidbody rb = draggedObject.GetComponent<Rigidbody>();

        // Hand the part directly to CenterUICursor — player holds it immediately,
        // cursor locks automatically, no need to drop on ground first.
        CenterUICursor cursor = FindFirstObjectByType<CenterUICursor>();
        if (cursor != null && rb != null)
        {
            inventoryManager.OnItemSpawned(itemData, draggedObject, this.gameObject);
            cursor.GrabDirect(rb);
        }
        else
        {
            // Fallback: place on ground if no cursor found
            Ray ray = mainCamera.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, placementLayerMask))
            {
                draggedObject.transform.position = hit.point + Vector3.up * dragHeight;
                if (rb != null) { rb.useGravity = true; rb.isKinematic = false; }
                inventoryManager.OnItemSpawned(itemData, draggedObject, this.gameObject);
            }
            else
            {
                Destroy(draggedObject);
            }
        }

        draggedObject = null;
    }
    
    private void SetupDraggedObject(GameObject obj)
    {
        // Set Interactable layer on root and all children so CenterUICursor can grab it
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = interactableLayer;

        obj.tag = "Grabbable";

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.mass = 1f;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // ContinuousSpeculative is required when isKinematic = true
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Use GetComponentInChildren so colliders on child meshes are detected
        if (obj.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider col = obj.AddComponent<BoxCollider>();
            MeshFilter meshFilter = obj.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                col.center = meshFilter.sharedMesh.bounds.center;
                col.size = meshFilter.sharedMesh.bounds.size;
            }
        }

        SnapZoneAssignment assignment = obj.GetComponent<SnapZoneAssignment>();
        if (assignment == null)
            assignment = obj.AddComponent<SnapZoneAssignment>();

        if (!string.IsNullOrEmpty(itemData.targetSnapZoneName))
        {
            SnapToPlace[] zones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
            foreach (SnapToPlace zone in zones)
            {
                if (ZoneNameMatches(zone.gameObject.name, itemData.targetSnapZoneName))
                {
                    assignment.assignedSnapZone = zone;
                    break;
                }
            }
        }

        // Apply configured scale so the part visually matches the car
        obj.transform.localScale = Vector3.one * itemData.spawnScale;
    }

    /// <summary>
    /// Matches zone GameObjects named "hood-snap" against item data named "Hood Zone".
    /// Strips "-snap" and " Zone" suffixes then compares case-insensitively.
    /// </summary>
    private bool ZoneNameMatches(string zoneName, string targetName)
    {
        if (string.IsNullOrEmpty(zoneName) || string.IsNullOrEmpty(targetName)) return false;
        string a = zoneName.Replace("-snap", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        string b = targetName.Replace(" Zone", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        return a == b || a.Contains(b) || b.Contains(a);
    }
}
