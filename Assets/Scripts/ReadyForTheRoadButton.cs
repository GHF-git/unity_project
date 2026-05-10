using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the "Ready for the Road →" button in the bottom-right of the screen.
/// Hidden until Show() is called by SteeringAssemblyController.ShowCarPhase().
/// Stays grey/unclickable until Unlock() is called when the player clicks
/// Continue on the Differential success card (the final assembly step).
/// </summary>
public class ReadyForTheRoadButton : MonoBehaviour
{
    [Header("Button UI")]
    public Button          readyButton;
    public Image           buttonImage;
    public Image           borderImage;
    public TextMeshProUGUI buttonLabel;

    [Header("Colors")]
    public Color lockedColor         = new Color(0.10f, 0.11f, 0.14f, 0.92f);
    public Color lockedTextColor     = new Color(0.45f, 0.45f, 0.45f, 1.00f);
    public Color unlockedColor       = new Color(0.08f, 0.62f, 0.25f, 1.00f);
    public Color highlightFlashColor = new Color(0.12f, 0.78f, 0.32f, 1.00f);
    public Color borderLockedColor   = new Color(0.25f, 0.27f, 0.30f, 0.95f);
    public Color borderUnlockedColor = new Color(0.10f, 0.80f, 0.35f, 1.00f);

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Makes the button visible but locked (grey/unclickable).
    /// Called by SteeringAssemblyController.ShowCarPhase() when the player
    /// clicks "Proceed to Car Assembly".
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        SetLocked(true);
    }

    /// <summary>
    /// Unlocks the button (green, clickable) and plays the unlock pulse animation.
    /// Called by InstructionCardContent when the player clicks Continue on the
    /// Differential success card.
    /// </summary>
    public void Unlock()
    {
        SetLocked(false);
        StartCoroutine(UnlockPulse());
    }

    // ── Locked / unlocked state ───────────────────────────────────────────────

    void SetLocked(bool locked)
    {
        if (readyButton != null)
        {
            readyButton.interactable = !locked;

            if (!locked)
            {
                ColorBlock cb       = readyButton.colors;
                cb.normalColor      = Color.white;
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
                cb.pressedColor     = new Color(0.78f, 0.78f, 0.78f, 1f);
                cb.disabledColor    = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                cb.colorMultiplier  = 1f;
                cb.fadeDuration     = 0.12f;
                readyButton.colors  = cb;
            }
        }

        if (buttonImage != null)
            buttonImage.color = locked ? lockedColor : unlockedColor;

        if (borderImage != null)
            borderImage.color = locked ? borderLockedColor : borderUnlockedColor;

        if (buttonLabel != null)
            buttonLabel.color = locked ? lockedTextColor : Color.white;
    }

    // ── Unlock pulse animation ────────────────────────────────────────────────

    IEnumerator UnlockPulse()
    {
        if (readyButton == null) yield break;

        RectTransform rt      = readyButton.GetComponent<RectTransform>();
        const float   Duration = 0.45f;
        float         t        = 0f;

        while (t < Duration)
        {
            t += Time.deltaTime;
            float norm  = t / Duration;
            float scale = 1f + 0.12f * Mathf.Sin(norm * Mathf.PI);
            rt.localScale = Vector3.one * scale;

            float flash = Mathf.Sin(norm * Mathf.PI);
            if (buttonImage != null)
                buttonImage.color = Color.Lerp(unlockedColor, highlightFlashColor, flash);
            if (borderImage != null)
                borderImage.color = Color.Lerp(borderUnlockedColor,
                    new Color(0.40f, 1.00f, 0.55f, 1f), flash);

            yield return null;
        }

        rt.localScale = Vector3.one;
        if (buttonImage != null) buttonImage.color = unlockedColor;
        if (borderImage != null) borderImage.color = borderUnlockedColor;
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    /// <summary>Loads the race scene when the player clicks the unlocked button.</summary>
    void OnReadyClicked()
    {
        SceneManager.LoadScene("race");
    }
}
