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

    // Exact world position where the 3D preview was spawned at drag start.
    // Used to teleport the part back when the order validation fails.
    private Vector3 dragStartSpawnPosition;
    
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
    
    /// <summary>Returns the 3D object currently being dragged, or null if not dragging.</summary>
    public GameObject GetDraggedObject() => draggedObject;

    /// <summary>Returns the itemName of the item assigned to this slot, or null if not initialised.</summary>
    public string CurrentItemName => itemData != null ? itemData.itemName : null;

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

        // Store original spawn position before any drag movement so we can always
        // restore the object to this exact location if validation fails.
        dragStartSpawnPosition = spawnPosition;

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

        // ── Assembly order validation ─────────────────────────────────────────
        // Check the order BEFORE any snap-to-place logic runs.
        // If the part is wrong: destroy the preview, restore inventory slot, show alert.
        // Nothing else changes — index, inventory, and car state stay identical.
        if (AssemblyOrderManager.Instance != null &&
            !AssemblyOrderManager.Instance.IsCorrectPart(itemData.itemName))
        {
            CancelDragAndRestoreInventory();
            AssemblyOrderManager.Instance.ShowWrongOrderAlert();
            draggedObject = null;
            return;
        }
        // ─────────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Called when order validation fails.
    /// Teleports the spawned preview back to its exact spawn origin, freezes it in
    /// place so physics cannot move it, then immediately destroys it and re-enables
    /// the inventory slot — all before any snap-to-place logic can execute.
    /// </summary>
    private void CancelDragAndRestoreInventory()
    {
        if (draggedObject != null)
        {
            // Freeze in place — stops any residual physics movement
            Rigidbody rb = draggedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity   = Vector3.zero;
                rb.angularVelocity  = Vector3.zero;
                rb.constraints      = RigidbodyConstraints.FreezeAll;
            }

            // Return to exact original spawn position
            draggedObject.transform.position = dragStartSpawnPosition;

            // Destroy the preview — it never made it to the car
            Destroy(draggedObject);
        }

        // Re-enable the inventory slot exactly as it was before the drag started
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        InventorySlotVisuals visuals = GetComponent<InventorySlotVisuals>();
        if (visuals != null)
            visuals.SetDisabled(false);
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
