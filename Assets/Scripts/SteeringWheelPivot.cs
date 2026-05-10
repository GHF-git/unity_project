using System.Collections;
using UnityEngine;

/// <summary>
/// Replaces knuckle visuals with wheel meshes once the steering assembly is complete.
///
/// Wired to <see cref="SteeringAnimationController"/> which calls <see cref="OnSteeringReady"/>
/// from inside its own <c>OnAnimationReady()</c> — i.e. exactly when all steering parts are snapped.
///
/// On ready:
///   - Immediately hides the two knuckle renderers.
///   - Fades the two wheel renderers in over <see cref="fadeDuration"/> seconds
///     by cross-fading a transparent overlay material → then restoring the opaque materials.
///
/// Every frame (once ready):
///   - Reads <see cref="SteeringAnimationController.NormalizedRackOffset"/> (range [−1, +1]).
///   - Pivots each wheel around world Y using Ackermann angles:
///       positive t = right turn  → right wheel is inner  (larger angle)
///       negative t = left  turn  → left  wheel is inner  (larger angle)
/// </summary>
public class SteeringWheelPivot : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Steering Reference")]
    [Tooltip("SteeringAnimationController that drives NormalizedRackOffset.")]
    public SteeringAnimationController steeringController;

    [Header("Wheel Transforms")]
    [Tooltip("Transform of /SteeringSystem1/wheels/right.")]
    public Transform rightWheel;

    [Tooltip("Transform of /SteeringSystem1/wheels/left.")]
    public Transform leftWheel;

    [Header("Knuckles to Hide")]
    [Tooltip("MeshRenderer on /SteeringSystem1/steering_knuckle_right.")]
    public Renderer knuckleRight;

    [Tooltip("MeshRenderer on /SteeringSystem1/steering_knuckle_left.")]
    public Renderer knuckleLeft;

    [Header("Ackermann Pivot Angles")]
    [Tooltip("Max pivot of the inner wheel in degrees (right wheel during a right turn, left wheel during a left turn).")]
    public float innerMaxAngle = 40f;

    [Tooltip("Max pivot of the outer wheel in degrees (left wheel during a right turn, right wheel during a left turn).")]
    public float outerMaxAngle = 28f;

    [Header("Fade")]
    [Tooltip("Duration in seconds of the wheel fade-in after assembly completes.")]
    public float fadeDuration = 0.6f;

    [Tooltip("Transparent URP material used to cross-fade the wheels in. " +
             "Set to /Assets/materials/transparent.mat (or any URP Lit Transparent).")]
    public Material fadeMaterial;

    // ── Private state ─────────────────────────────────────────────────────────

    bool _isReady;

    // Rest world rotations captured in OnSteeringReady — pivot deltas applied around world Y relative to these.
    Quaternion _rightRestWorldRot;
    Quaternion _leftRestWorldRot;

    // Original shared materials on each wheel renderer — restored after the fade.
    Material[] _rightOriginalMaterials;
    Material[] _leftOriginalMaterials;

    MeshRenderer _rightRenderer;
    MeshRenderer _leftRenderer;

    Coroutine _fadeCoroutine;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Wheels are invisible until the assembly completes.
        _rightRenderer = rightWheel != null ? rightWheel.GetComponent<MeshRenderer>() : null;
        _leftRenderer  = leftWheel  != null ? leftWheel.GetComponent<MeshRenderer>()  : null;

        if (_rightRenderer != null) _rightRenderer.enabled = false;
        if (_leftRenderer  != null) _leftRenderer.enabled  = false;
    }

    void Update()
    {
        if (!_isReady) return;

        if (steeringController == null)
        {
            Debug.LogWarning("[SteeringWheelPivot] steeringController is not assigned.", this);
            return;
        }

        float t = steeringController.NormalizedRackOffset; // [−1, +1]

        // Ackermann: inner wheel turns more than outer.
        // Positive t = right turn → right wheel is inner.
        float rightAngle = t >= 0f ?  t * innerMaxAngle :  t * outerMaxAngle;
        float leftAngle  = t >= 0f ?  t * outerMaxAngle :  t * innerMaxAngle;

        if (rightWheel != null)
            rightWheel.rotation = Quaternion.AngleAxis(rightAngle, Vector3.up) * _rightRestWorldRot;

        if (leftWheel != null)
            leftWheel.rotation = Quaternion.AngleAxis(leftAngle, Vector3.up) * _leftRestWorldRot;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="SteeringAnimationController"/> once all steering parts are snapped.
    /// Hides knuckles, shows wheels, and starts the fade-in coroutine.
    /// Safe to call only once — guarded by <c>_isReady</c>.
    /// </summary>
    public void OnSteeringReady()
    {
        if (_isReady) return;

        // Capture rest world rotations before any pivot is applied.
        if (rightWheel != null) _rightRestWorldRot = rightWheel.rotation;
        if (leftWheel  != null) _leftRestWorldRot  = leftWheel.rotation;

        // Immediately hide knuckles — their materials are opaque so no fade is possible.
        if (knuckleRight != null) knuckleRight.enabled = false;
        if (knuckleLeft  != null) knuckleLeft.enabled  = false;

        // Start wheel fade-in.
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeWheelsIn());
    }

    // ── Fade coroutine ────────────────────────────────────────────────────────

    /// <summary>
    /// Fades both wheel renderers from transparent → opaque over <see cref="fadeDuration"/> seconds.
    /// Strategy:
    ///   1. Cache original shared materials.
    ///   2. Replace all slots with the transparent fadeMaterial (alpha 0) on both renderers.
    ///   3. Enable renderers.
    ///   4. Lerp alpha 0 → 1 via MaterialPropertyBlock each frame.
    ///   5. Restore original shared materials at the end.
    ///   6. Mark _isReady = true so Update() starts pivoting.
    ///
    /// Falls back to instant-show if fadeMaterial is null or fadeDuration ≤ 0.
    /// </summary>
    IEnumerator FadeWheelsIn()
    {
        bool canFade = fadeMaterial != null && fadeDuration > 0f
                       && _rightRenderer != null && _leftRenderer != null;

        if (!canFade)
        {
            // Instant show — no fade available.
            if (_rightRenderer != null) _rightRenderer.enabled = true;
            if (_leftRenderer  != null) _leftRenderer.enabled  = true;
            _isReady = true;
            yield break;
        }

        // Cache original materials.
        _rightOriginalMaterials = _rightRenderer.sharedMaterials;
        _leftOriginalMaterials  = _leftRenderer.sharedMaterials;

        // Build fade arrays (one fadeMaterial per slot so all submeshes fade together).
        var rightFadeSlots = new Material[_rightOriginalMaterials.Length];
        var leftFadeSlots  = new Material[_leftOriginalMaterials.Length];
        for (int i = 0; i < rightFadeSlots.Length; i++) rightFadeSlots[i] = fadeMaterial;
        for (int i = 0; i < leftFadeSlots.Length;  i++) leftFadeSlots[i]  = fadeMaterial;

        _rightRenderer.sharedMaterials = rightFadeSlots;
        _leftRenderer.sharedMaterials  = leftFadeSlots;

        _rightRenderer.enabled = true;
        _leftRenderer.enabled  = true;

        // Fade alpha 0 → 1 via MaterialPropertyBlock — never touches shared material data.
        var block = new MaterialPropertyBlock();
        int baseColorId = Shader.PropertyToID("_BaseColor");

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);

            block.SetColor(baseColorId, new Color(1f, 1f, 1f, alpha));
            _rightRenderer.SetPropertyBlock(block);
            _leftRenderer.SetPropertyBlock(block);

            yield return null;
        }

        // Restore opaque materials and clear the property block.
        _rightRenderer.sharedMaterials = _rightOriginalMaterials;
        _leftRenderer.sharedMaterials  = _leftOriginalMaterials;
        _rightRenderer.SetPropertyBlock(null);
        _leftRenderer.SetPropertyBlock(null);

        _isReady = true;
    }
}
