using UnityEngine;

public class CenterUICursor : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Header("Grab")]
    public float maxGrabDistance = 3f;
    public LayerMask interactableMask;     // Layer der greifbaren Objekte (z.B. Interactable)

    [Header("Hybrid Hold: Tisch + Frei")]
    public LayerMask tableMask;            // Layer des Tisch-Objekts
    public float tableRayDistance = 10f;

    [Tooltip("Nur Treffer zählen als 'Tischoberfläche', wenn Normal.y >= dieser Wert (bei MeshCollider wichtig).")]
    [Range(0f, 1f)] public float minUpNormalY = 0.6f;

    [Tooltip("Kleiner Abstand über der Tischoberfläche (gegen 'im Tisch').")]
    public float surfaceExtraOffset = 0.003f;

    [Tooltip("Um im Free-Mode nicht in Geometrie zu clippen (Wände/Tisch/etc.).")]
    public LayerMask environmentMask;

    [Tooltip("Sicherheitsradius für SphereCast im Free-Mode.")]
    public float freeModeSphereRadius = 0.08f;

    [Header("Follow")]
    [Tooltip("Höher = steiferes Folgen.")]
    public float followSpeed = 60f;

    [Header("Zoom while Holding (Keyboard ONLY)")]
    [Tooltip("Zoom-In (näher ran)")]
    public KeyCode zoomInKey = KeyCode.R;

    [Tooltip("Zoom-Out (weiter weg)")]
    public KeyCode zoomOutKey = KeyCode.F;

    [Tooltip("Aktueller Abstand im Free-Mode (Meter).")]
    public float holdDistance = 1.2f;
    public float minHoldDistance = 0.4f;
    public float maxHoldDistance = 2.5f;

    [Tooltip("Zoom-Geschwindigkeit in Meter pro Sekunde.")]
    public float zoomMetersPerSecond = 0.8f;

    [Header("Held Layer (optional)")]
    public string heldLayerName = "Held";

    [Header("Snap")]
    public bool lockAfterSnap = true;

    // --- Held state ---
    private Rigidbody heldRb;
    private Collider heldCol;

    /// <summary>The GameObject currently held by the cursor, or null if nothing is held.</summary>
    public GameObject HeldObject => heldRb != null ? heldRb.gameObject : null;
    private int oldLayer;

    // Restore rb/collider states
    private bool oldUseGravity;
    private bool oldIsKinematic;
    private bool oldDetectCollisions;
    private RigidbodyConstraints oldConstraints;
    private RigidbodyInterpolation oldInterpolation;
    private CollisionDetectionMode oldCollisionMode;
    private bool oldIsTrigger;

    private int heldLayer;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        heldLayer = LayerMask.NameToLayer(heldLayerName);
        Physics.queriesHitTriggers = true;
    }

    void Update()
    {
        // Zoom nur mit Tasten (während Objekt gehalten wird)
        if (heldRb != null)
        {
            float delta = zoomMetersPerSecond * Time.deltaTime;

            if (Input.GetKey(zoomInKey))
                holdDistance -= delta;

            if (Input.GetKey(zoomOutKey))
                holdDistance += delta;

            holdDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
        }

        if (Input.GetMouseButtonDown(0)) TryGrab();
        if (Input.GetMouseButtonUp(0)) DropObject();
    }

    void FixedUpdate()
    {
        if (heldRb == null) return;
        MoveHeldHybrid();
    }

    void TryGrab()
    {
        // Don't grab 3D objects when the pointer is over a UI element
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (heldRb != null || cam == null) return;
        if (interactableMask.value == 0) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, maxGrabDistance, interactableMask, QueryTriggerInteraction.Collide))
            return;

        if (hit.rigidbody == null) return;

        heldRb = hit.rigidbody;
        heldCol = heldRb.GetComponent<Collider>();

        // Save old state
        oldLayer = heldRb.gameObject.layer;
        oldUseGravity = heldRb.useGravity;
        oldIsKinematic = heldRb.isKinematic;
        oldDetectCollisions = heldRb.detectCollisions;
        oldConstraints = heldRb.constraints;
        oldInterpolation = heldRb.interpolation;
        oldCollisionMode = heldRb.collisionDetectionMode;
        oldIsTrigger = (heldCol != null) ? heldCol.isTrigger : false;

        // Optional: Layer switch
        if (heldLayer >= 0) heldRb.gameObject.layer = heldLayer;

        // Holding: kinematic + trigger, damit nichts verdrängt und nicht “fliegt”
        if (heldCol != null) heldCol.isTrigger = true;

        heldRb.useGravity             = false;
        heldRb.isKinematic            = false; // force non-kinematic so velocity reset is valid
        heldRb.linearVelocity         = Vector3.zero;
        heldRb.angularVelocity        = Vector3.zero;
        heldRb.isKinematic            = true;
        heldRb.detectCollisions       = true;
        heldRb.constraints            = RigidbodyConstraints.None;
        heldRb.interpolation          = RigidbodyInterpolation.Interpolate;
        heldRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Snap-Zone highlight (falls vorhanden)
        var assign = heldRb.GetComponent<SnapZoneAssignment>();
        if (assign != null && assign.assignedSnapZone != null)
            assign.assignedSnapZone.OnObjectGrabbed(true);

    }
#if false // hidden broken section


    void _InitialMove() {


        MoveHeldHybrid();
    }
    }
    void _Stub2() {

    void _Stub() {


    /// <summary>
    /// Directly grabs a part spawned from the inventory without a raycast.
    /// Locks the cursor immediately so the player can walk to the snap zone and release.
    /// </summary>
    public void GrabDirect(Rigidbody rb)
    {
        if (rb == null || heldRb != null) return;
        GrabRigidbody(rb);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void GrabRigidbody(Rigidbody rb)
    {
        heldRb = rb;
        heldCol = heldRb.GetComponentInChildren<Collider>();

        oldLayer            = heldRb.gameObject.layer;
        oldUseGravity       = heldRb.useGravity;
        oldIsKinematic      = heldRb.isKinematic;
        oldDetectCollisions = heldRb.detectCollisions;
        oldConstraints      = heldRb.constraints;
        oldInterpolation    = heldRb.interpolation;
        oldCollisionMode    = heldRb.collisionDetectionMode;
        oldIsTrigger        = (heldCol != null) ? heldCol.isTrigger : false;

        if (heldLayer >= 0) heldRb.gameObject.layer = heldLayer;
        if (heldCol != null) heldCol.isTrigger = true;

        heldRb.useGravity             = false;
        heldRb.isKinematic            = true;
        heldRb.detectCollisions       = true;
        heldRb.constraints            = RigidbodyConstraints.None;
        heldRb.interpolation          = RigidbodyInterpolation.Interpolate;
        heldRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        heldRb.linearVelocity         = Vector3.zero;
        heldRb.angularVelocity        = Vector3.zero;

        var assign = heldRb.GetComponent<SnapZoneAssignment>();
        if (assign != null && assign.assignedSnapZone != null)
            assign.assignedSnapZone.OnObjectGrabbed(true);

        MoveHeldHybrid();
    }


#endif

    /// <summary>
    /// Directly grabs a part spawned from the inventory without a raycast.
    /// Cursor stays free — RMB is used for look, so no locking needed.
    /// </summary>
    public void GrabDirect(Rigidbody rb)
    {
        if (rb == null || heldRb != null) return;
        GrabRigidbody(rb);
    }

    void GrabRigidbody(Rigidbody rb)
    {
        heldRb = rb;
        heldCol = heldRb.GetComponentInChildren<Collider>();

        oldLayer            = heldRb.gameObject.layer;
        oldUseGravity       = heldRb.useGravity;
        oldIsKinematic      = heldRb.isKinematic;
        oldDetectCollisions = heldRb.detectCollisions;
        oldConstraints      = heldRb.constraints;
        oldInterpolation    = heldRb.interpolation;
        oldCollisionMode    = heldRb.collisionDetectionMode;
        oldIsTrigger        = (heldCol != null) ? heldCol.isTrigger : false;

        if (heldLayer >= 0) heldRb.gameObject.layer = heldLayer;
        if (heldCol != null) heldCol.isTrigger = true;

        heldRb.useGravity             = false;
        heldRb.isKinematic            = false; // force non-kinematic so velocity reset is valid
        heldRb.linearVelocity         = Vector3.zero;
        heldRb.angularVelocity        = Vector3.zero;
        heldRb.isKinematic            = true;
        heldRb.detectCollisions       = true;
        heldRb.constraints            = RigidbodyConstraints.None;
        heldRb.interpolation          = RigidbodyInterpolation.Interpolate;
        heldRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var assign = heldRb.GetComponent<SnapZoneAssignment>();
        if (assign != null && assign.assignedSnapZone != null)
            assign.assignedSnapZone.OnObjectGrabbed(true);

        MoveHeldHybrid();
    }

#if false


#endif
#if false


#endif
#if false


#endif
    void _Dead() {


    }

    void MoveHeldHybrid()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        bool hitTableTop = TryGetTableTopHit(ray, out RaycastHit tableHit);
        Vector3 targetPos;

        if (hitTableTop)
        {
            float tableTopY = tableHit.collider.bounds.max.y;

            float halfHeight = 0.05f;
            if (heldCol != null) halfHeight = heldCol.bounds.extents.y;

            targetPos = new Vector3(
                tableHit.point.x,
                tableTopY + halfHeight + surfaceExtraOffset,
                tableHit.point.z
            );
        }
        else
        {
            // FREE MODE: Abstand per Tastatur steuerbar
            Vector3 desired = ray.origin + ray.direction * holdDistance;

            if (environmentMask.value != 0 &&
                Physics.SphereCast(ray, freeModeSphereRadius, out RaycastHit envHit, holdDistance,
                    environmentMask, QueryTriggerInteraction.Ignore))
            {
                desired = envHit.point - ray.direction * (freeModeSphereRadius + 0.01f);
            }

            targetPos = desired;
        }

        SmoothMoveKinematic(targetPos);
    }

    bool TryGetTableTopHit(Ray ray, out RaycastHit bestHit)
    {
        bestHit = default;
        if (tableMask.value == 0) return false;

        RaycastHit[] hits = Physics.RaycastAll(ray, tableRayDistance, tableMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        float bestDist = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.normal.y < minUpNormalY) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }

    void SmoothMoveKinematic(Vector3 targetPos)
    {
        float t = 1f - Mathf.Exp(-followSpeed * Time.fixedDeltaTime);
        Vector3 newPos = Vector3.Lerp(heldRb.position, targetPos, t);
        heldRb.MovePosition(newPos);
    }

    void DropObject()
    {
        if (heldRb == null) return;

        // Snap prüfen
        var assign = heldRb.GetComponent<SnapZoneAssignment>();
        if (assign != null && assign.assignedSnapZone != null)
        {
            var zone = assign.assignedSnapZone;
            float d = Vector3.Distance(heldRb.transform.position, zone.snapPoint.position);

            if (d <= zone.snapRange)
            {
                zone.TrySnapObject(heldRb.gameObject);
                zone.OnObjectGrabbed(false);

                if (lockAfterSnap) LockAfterSnap(heldRb);

                CleanupHeld();
                return;
            }

            zone.OnObjectGrabbed(false);
        }

        RestoreHeldPhysics();
        CleanupHeld();
    }

    void RestoreHeldPhysics()
    {
        if (heldCol != null) heldCol.isTrigger = oldIsTrigger;

        heldRb.useGravity = oldUseGravity;
        heldRb.isKinematic = oldIsKinematic;
        heldRb.detectCollisions = oldDetectCollisions;
        heldRb.constraints = oldConstraints;
        heldRb.interpolation = oldInterpolation;
        heldRb.collisionDetectionMode = oldCollisionMode;
    }

    void LockAfterSnap(Rigidbody rb)
    {
        if (heldCol != null) heldCol.isTrigger = false;
        rb.useGravity       = false;
        rb.detectCollisions = true;
        rb.isKinematic      = true;
        rb.constraints      = RigidbodyConstraints.FreezeAll;
    }

    void CleanupHeld()
    {
        heldRb.gameObject.layer = oldLayer;
        heldRb = null;
        heldCol = null;
    }
}
