using UnityEngine;
using DG.Tweening;

public class PanelToggle : MonoBehaviour
{
    [Header("References")]
    public RectTransform panel;   // assign your UI panel in Inspector

    [Header("Animation Settings")]
    public float duration = 0.7f;
    public Ease easeIn = Ease.OutBack;
    public Ease easeOut = Ease.InBack;

    [SerializeField] private Vector2 onScreenPosition;   // middle of screen
    [SerializeField] private Vector2 offScreenPosition;  // hidden left
    private bool isVisible = false;

    //private void Awake()
    //{
    //    // Save the current (final) position as the middle of the screen
    //    onScreenPosition = panel.anchoredPosition;

    //    // Calculate off-screen position (same Y, far left outside canvas)
    //    offScreenPosition = new Vector2(-Screen.width, onScreenPosition.y);

    //    // Start hidden on the left
    //    panel.anchoredPosition = offScreenPosition;
    //}

    public void TogglePanel()
    {
        if (isVisible)
        {
            // Slide OUT to the left
            panel.DOAnchorPos(offScreenPosition, duration).SetEase(easeOut);
        }
        else
        {
            // Slide IN to middle
            panel.DOAnchorPos(onScreenPosition, duration).SetEase(easeIn);
        }

        isVisible = !isVisible;
    }
}
