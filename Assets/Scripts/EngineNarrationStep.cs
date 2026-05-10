using UnityEngine;

/// <summary>
/// Data container for a single engine narration step.
/// Assign one per step in the EngineNarrationController inspector.
/// </summary>
[CreateAssetMenu(menuName = "Engine/Narration Step", fileName = "NarrationStep")]
public class EngineNarrationStep : ScriptableObject
{
    [Tooltip("Short label displayed prominently in the instructor panel (e.g. 'PISTONS').")]
    public string stepLabel;

    [Tooltip("Subtitle text narrating this step, shown below the label.")]
    [TextArea(2, 5)]
    public string narrationText;

    [Tooltip("Voice-over audio clip for this step. Leave null during development — a fallback delay is used instead.")]
    public AudioClip voiceOver;

    [Tooltip("Names of GameObjects whose renderers should be highlighted during this step. Empty = no highlight.")]
    public string[] highlightPartNames = System.Array.Empty<string>();

    [Tooltip("Names of group GameObjects (no renderer on parent) whose entire sub-tree renderers are highlighted.")]
    public string[] highlightGroupNames = System.Array.Empty<string>();
}
