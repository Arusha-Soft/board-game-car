using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

using TMPro;


public class CarSelectionMenu : MonoBehaviour
{
    [Header("Cars (flexible amount)")]
    public List<CarItem> cars = new List<CarItem>();  // fill in Inspector

    [Header("UI")]
    public Button nextButton;
    public Button prevButton;
    public Button selectButton;

    [Tooltip("Optional label to show current car name")]

    public TextMeshProUGUI carNameLabel; 

    [Header("Layout")]
    [Tooltip("Where the current (focused) car sits in world space")]
    public Transform centerPoint;
    [Tooltip("How far (world units) cars slide in/out on X axis")]
    public float slideDistance = 6f;

    [Header("Animation")]
    public float slideDuration = 0.45f;
    public Ease slideEase = Ease.OutCubic;

    [Header("Focus (selecting phase)")]
    [Tooltip("If true, each car uses its own initial localScale as the base scale.")]
    public bool usePerCarInitialScale = true;
    [Tooltip("If not using per-car scale, this is the base scale for all cars.")]
    public Vector3 defaultBaseScale = Vector3.one;
    [Tooltip("Focused (center) car scale multiplier, e.g. 1.2 = 20% larger.")]
    [Min(0.01f)] public float focusScaleMultiplier = 1.2f;
    public float focusScaleDuration = 0.25f;
    public Ease focusScaleEase = Ease.OutBack;

    [Header("Focus Rotation")]
    public bool spinWhileFocused = true;
    [Tooltip("Axis to rotate around while focused (local space).")]
    public Vector3 spinAxis = Vector3.up;
    [Tooltip("Degrees per second.")]
    public float spinSpeed = 35f;

    [Header("Start State")]
    public int startIndex = 0; // starting car index

    public int CurrentIndex { get; private set; }

    bool isAnimating;

    // per-car base scales (captured at Start or using defaultBaseScale)
    private Vector3[] baseScales;
    // track the current focus spin tween so we can kill it when we slide away
    private Tween focusedSpinTween;

    [Serializable]
    public class CarItem
    {
        public Transform car;              // car root object
        public string displayName = "Car"; // name to show/log
        public bool isLocked = false;      // locked? cannot select
        [Tooltip("Optional lock badge (will be set active if locked).")]
        public GameObject lockBadge;       // optional visual
    }

    void Start()
    {
        // Basic guard
        if (cars == null || cars.Count == 0 || centerPoint == null)
        {
            Debug.LogWarning("[CarSelectionMenu] Please assign cars and centerPoint.");
            SetUIInteractable(false);
            return;
        }

        // Clamp/normalize index
        CurrentIndex = Mathf.Clamp(startIndex, 0, cars.Count - 1);

        // Cache base scales
        baseScales = new Vector3[cars.Count];
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i].car == null) continue;
            baseScales[i] = usePerCarInitialScale ? cars[i].car.localScale : defaultBaseScale;
            if (!usePerCarInitialScale)
                cars[i].car.localScale = defaultBaseScale; // normalize to chosen default
        }

        InitializeCars();

        // Hook buttons
        if (nextButton) nextButton.onClick.AddListener(OnNext);
        if (prevButton) prevButton.onClick.AddListener(OnPrev);
        if (selectButton) selectButton.onClick.AddListener(OnSelect);

        UpdateUI();
        // Start focus effects on initial car
        StartFocusEffects(CurrentIndex, instant: true);
    }

    void OnDisable()
    {
        KillFocusedSpin();
        SetUIInteractable(true); // safety on disable
    }

    void InitializeCars()
    {
        // Deactivate all first & sync lock badges
        for (int i = 0; i < cars.Count; i++)
        {
            var item = cars[i];
            SafeSetActive(item.car, false);
            if (item.lockBadge) item.lockBadge.SetActive(item.isLocked);
        }

        // Activate current at center with base scale
        var current = cars[CurrentIndex];
        SafeSetActive(current.car, true);
        current.car.position = centerPoint.position;
        current.car.localScale = baseScales[CurrentIndex];
    }

    void UpdateUI()
    {
        var current = cars[CurrentIndex];

        // Update label if present
        if (carNameLabel)
        {
#if TMP_PRESENT || TEXTMESHPRO
            carNameLabel.text = current.displayName;
#else
            carNameLabel.text = current.displayName;
#endif
        }

        // Select button is interactable only if unlocked (and not animating)
        if (selectButton) selectButton.interactable = !current.isLocked && !isAnimating;

        // Keep lock badge synced
        if (current.lockBadge) current.lockBadge.SetActive(current.isLocked);
    }

    void SetUIInteractable(bool value)
    {
        if (nextButton) nextButton.interactable = value;
        if (prevButton) prevButton.interactable = value;
        // Select depends on lock state as well
        if (selectButton) selectButton.interactable = value && !cars[CurrentIndex].isLocked;
    }

    void OnNext()
    {
        if (isAnimating || cars.Count <= 1) return;
        SlideTo(IndexWrap(CurrentIndex + 1), +1);
    }

    void OnPrev()
    {
        if (isAnimating || cars.Count <= 1) return;
        SlideTo(IndexWrap(CurrentIndex - 1), -1);
    }

    int IndexWrap(int idx)
    {
        int n = cars.Count;
        if (n <= 0) return 0;
        idx %= n;
        if (idx < 0) idx += n;
        return idx;
    }

    void SlideTo(int nextIndex, int dir)
    {
        // dir: +1 means new car comes from right; -1 from left
        isAnimating = true;
        //SetUIInteractable(false);

        var current = cars[CurrentIndex];
        var next = cars[nextIndex];

        // stop focus effects on current (scale back & stop spin)
        EndFocusEffects(CurrentIndex);

        // Prepare positions
        Vector3 center = centerPoint.position;
        Vector3 offRight = center + new Vector3(slideDistance, 0f, 0f);
        Vector3 offLeft = center + new Vector3(-slideDistance, 0f, 0f);

        // Ensure tweens on transforms are killed to avoid overlap
        if (current.car) current.car.DOKill(true);
        if (next.car) next.car.DOKill(true);

        // Activate next and place it offscreen based on direction; reset to base scale
        SafeSetActive(next.car, true);
        next.car.position = (dir > 0) ? offRight : offLeft;
        next.car.localScale = baseScales[nextIndex];

        // Animate: current goes out, next comes in
        Sequence seq = DOTween.Sequence();

        // Current slides out
        Vector3 currentTarget = (dir > 0) ? offLeft : offRight;
        if (current.car)
            seq.Join(current.car.DOMove(currentTarget, slideDuration).SetEase(slideEase));

        // Next slides in
        if (next.car)
            seq.Join(next.car.DOMove(center, slideDuration).SetEase(slideEase));

        seq.OnComplete(() =>
        {
            // Deactivate old one
            SafeSetActive(current.car, false);

            // Update index
            CurrentIndex = nextIndex;

            // Start focus effects on the new current
            StartFocusEffects(CurrentIndex, instant: false);

            // Re-enable UI according to lock state
            isAnimating = false;
            SetUIInteractable(true);
            UpdateUI();
        });
    }

    void OnSelect()
    {
        var current = cars[CurrentIndex];
        if (current.isLocked)
        {
            Debug.Log($"[CarSelectionMenu] '{current.displayName}' is LOCKED. Cannot select.");
            return;
        }

        Debug.Log($"[CarSelectionMenu] Selected car: {current.displayName}");

        // Set Selected Car
        PlayerPrefs.SetInt("CurrentCar", CurrentIndex);
        PlayerPrefs.SetString("CarName", current.displayName);
        // TODO: trigger event, save choice, etc.
        // OnCarSelected?.Invoke(CurrentIndex, current);
    }

    void SafeSetActive(Transform t, bool active)
    {
        if (!t) return;
        if (t.gameObject.activeSelf != active)
            t.gameObject.SetActive(active);
    }

    // ----- Focus (selecting phase) helpers -----

    void StartFocusEffects(int index, bool instant)
    {
        var item = cars[index];
        if (!item.car) return;

        // Scale up to focused size
        Vector3 targetScale = baseScales[index] * Mathf.Max(0.01f, focusScaleMultiplier);
        item.car.DOKill(true); // clear any previous tweens on this transform
        if (instant)
        {
            item.car.localScale = targetScale;
        }
        else
        {
            item.car.DOScale(targetScale, focusScaleDuration).SetEase(focusScaleEase);
        }

        // Start spin (continuous) while focused
        if (spinWhileFocused)
        {
            KillFocusedSpin(); // just in case
            // one full 360 in duration based on speed = degrees/sec
            float oneRevDuration = (spinSpeed <= 0f) ? 0f : 360f / spinSpeed;
            if (oneRevDuration > 0f)
            {
                // Local rotation incrementally around spinAxis
                // We use DORotate with relative incremental loops for smooth constant speed
                focusedSpinTween = item.car
                    .DOLocalRotate(spinAxis.normalized * 360f, oneRevDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental);
            }
        }
    }

    void EndFocusEffects(int index)
    {
        var item = cars[index];
        if (!item.car) return;

        // Stop spin for the previously focused car
        KillFocusedSpin();

        // Scale back to base
        Vector3 baseScale = baseScales[index];
        item.car.DOKill(true);
        item.car.DOScale(baseScale, focusScaleDuration * 0.8f).SetEase(Ease.OutQuad);
    }

    void KillFocusedSpin()
    {
        if (focusedSpinTween != null && focusedSpinTween.IsActive())
        {
            focusedSpinTween.Kill(true);
            focusedSpinTween = null;
        }
    }

    // ----- Public API -----

    public void SetLocked(int index, bool locked)
    {
        int i = IndexWrap(index);
        cars[i].isLocked = locked;
        if (cars[i].lockBadge) cars[i].lockBadge.SetActive(locked);
        if (i == CurrentIndex) UpdateUI();
    }

    public void SetFocusScaleMultiplier(float multiplier)
    {
        focusScaleMultiplier = Mathf.Max(0.01f, multiplier);
        // refresh current focus scale immediately
        StartFocusEffects(CurrentIndex, instant: false);
    }

    public void GotoGame()
    {
        SceneManager.LoadScene(1);
    }
}
