using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides all exterior mesh renderers on the 1964 Porsche 911 cockpit model.
/// Only renderers whose material names belong to <see cref="InteriorMaterialNames"/>
/// are kept visible. Everything else is disabled, leaving only the interior
/// visible from the driver's first-person perspective.
/// Call <see cref="ApplyInteriorOnlyVisibility"/> from Awake or via the
/// Inspector button to apply the filter at startup.
/// </summary>
public class CockpitInteriorSetup : MonoBehaviour
{
    // ── Interior material names (from the OBJ .mtl file) ─────────────────────

    private static readonly HashSet<string> InteriorMaterialNames = new HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase)
    {
        "internal",
        "interior_1",
        "interior_2_seat",
        "interior_carpet",
        "interior_wood",
        "glass_window",
    };

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private bool _initialized = false;

    // OnEnable fires even when the GameObject is activated after being disabled,
    // unlike Awake which does not fire on initially-inactive GameObjects.
    private void OnEnable()
    {
        if (_initialized) return;
        _initialized = true;
        ApplyInteriorOnlyVisibility();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Iterates every <see cref="MeshRenderer"/> in this GameObject's hierarchy
    /// and enables only those whose first shared material name is in
    /// <see cref="InteriorMaterialNames"/>. All others are disabled.
    /// </summary>
    public void ApplyInteriorOnlyVisibility()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        foreach (MeshRenderer renderer in renderers)
        {
            bool isInterior = false;

            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                // Material names inside prefab instances include " (Instance)" suffix;
                // strip it for a clean comparison.
                string matName = mat.name.Replace(" (Instance)", "").Trim();

                if (InteriorMaterialNames.Contains(matName))
                {
                    isInterior = true;
                    break;
                }
            }

            renderer.enabled = isInterior;
        }
    }
}
