#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that re-centers all immediate children of a selected root
/// GameObject so the compound mesh visual center aligns with the root's origin.
///
/// Usage:
///   1. Select the root GameObject in the Hierarchy (e.g. the steering-column group parent).
///   2. GameObject → Recenter Children to Root Pivot.
///
/// What it does at edit time (runs once, undoable):
///   - Computes the world-space centroid of every MeshRenderer.bounds found in the
///     direct children and their descendants.
///   - Calculates the delta between that centroid and the root's world position.
///   - Shifts every direct child by -delta in world space (moves the mesh visual
///     center to the root's origin) while keeping the root's own Transform unchanged.
///
/// At runtime the root's transform.position is the visual center, so
/// SteeringSnapSetup.SnapGroupNextFrame() — which already teleports the root to
/// the ghost group's position — places the part correctly with no further changes.
/// </summary>
public static class PivotRecenterTool
{
    private const string MenuPath = "GameObject/Recenter Children to Root Pivot";

    [MenuItem(MenuPath, false, 49)]
    private static void RecenterChildren()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogError("[PivotRecenterTool] No GameObject selected.");
            return;
        }

        if (root.transform.childCount == 0)
        {
            Debug.LogWarning($"[PivotRecenterTool] '{root.name}' has no children — nothing to recenter.", root);
            return;
        }

        // ── 1. Gather all MeshRenderer bounds in the hierarchy ────────────────
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[PivotRecenterTool] No MeshRenderers found under '{root.name}'.", root);
            return;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        Vector3 worldCenter = combined.center;

        // ── 2. Compute delta from root origin to mesh centroid ────────────────
        Vector3 delta = worldCenter - root.transform.position;

        if (delta.sqrMagnitude < 1e-8f)
        {
            Debug.Log($"[PivotRecenterTool] '{root.name}' is already centered — no change needed.");
            return;
        }

        // ── 3. Register the entire child hierarchy with Undo ─────────────────
        Undo.SetCurrentGroupName("Recenter Children to Root Pivot");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Transform child in root.transform)
            Undo.RecordObject(child, "Recenter child");

        // ── 4. Shift every direct child by -delta in world space ──────────────
        // Moving children by -delta brings the visual center to the root's origin.
        foreach (Transform child in root.transform)
            child.position -= delta;

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[PivotRecenterTool] '{root.name}': shifted {root.transform.childCount} child(ren) by {-delta} " +
                  $"(visual center was {delta} from root origin).", root);

        // Mark the scene dirty so the change is saved
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    [MenuItem(MenuPath, true)]
    private static bool RecenterChildrenValidate()
    {
        return Selection.activeGameObject != null;
    }
}
#endif
