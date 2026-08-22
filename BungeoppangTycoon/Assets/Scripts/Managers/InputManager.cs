using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    public static event Action<FishBunController> SelectedFishBunChanged;
    public static event Action<bool> TouchModeChanged;

    const float SwipeScreenRatio = 0.08f;
    const float MinimumSwipePixels = 80f;
    const float VerticalSwipeRatio = 1.4f;

    FishBunController selectedFishBun;
    Vector2 swipeStart;
    int trackedFingerId = -1;
    bool canUseSwipe;
    bool isTouchMode;
    ToolController highlightedTool;
    readonly Dictionary<SpriteRenderer, Color> highlightedRenderers = new();
    readonly Dictionary<SpriteRenderer, SpriteRenderer> toolTargetOutlines = new();

    const float ToolTargetOutlineMinScale = 1.08f;
    const float ToolTargetOutlinePulseScale = 0.035f;
    const float ToolTargetOutlinePulseSpeed = 5f;

    public FishBunController SelectedFishBun => selectedFishBun;
    public bool IsTouchMode => isTouchMode;

    void Awake()
    {
        Instance = this;
        CameraController.ViewChanged += HandleViewChanged;
        SetTouchMode(Application.isMobilePlatform && Touchscreen.current != null);
    }

    void Start()
    {
        GameObject bin = GameObject.Find("bin");
        if (bin != null)
            bin.AddEvent(() => TryHandleSelectedFishBun(bin));
    }

    void OnDestroy()
    {
        CameraController.ViewChanged -= HandleViewChanged;
        ClearSelectedFishBun();
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        HandleTouchInput();
        UpdateToolTargetOutlines();

        if (!IsTouchPressed() && (GameInput.LeftClickPressed || GameInput.AnyKeyboardKeyPressed))
            SetTouchMode(false);

        if (Managers.Game.isRunning == false || UI_Tutorial.IsBlockingFirstCustomer)
            return;

        if (GameInput.KeyPressed(Key.Space) && UI_Tutorial.AllowsManualViewSwitch)
        {
            SetTouchMode(false);
            CameraController.Instance?.ToggleCamera();
        }

        if (GameInput.KeyPressed(Key.Escape) || GameInput.RightClickPressed)
        {
            SetTouchMode(false);
            CancelSelection();
        }

        if (CameraController.Instance != null &&
            CameraController.Instance.CurrentView == GameplayView.Cooking &&
            CameraController.Instance.IsTransitioning == false)
        {
            if (HandleSelectedFishBunShortcuts())
                return;

            HandleToolShortcuts();
        }
    }

    /// <summary>
    /// 선택한 완성 붕어빵을 조리대의 고정 대상에 바로 놓습니다.
    /// 대상이 씬에 없거나 아직 놓을 수 없는 상태면 선택을 유지합니다.
    /// </summary>
    bool HandleSelectedFishBunShortcuts()
    {
        if (selectedFishBun == null)
            return false;

        if (GameInput.KeyPressed(Key.W))
        {
            TryHandleSelectedFishBun(GameObject.FindGameObjectWithTag("displayPlate"));
            return true;
        }

        if (GameInput.KeyPressed(Key.E))
        {
            TryHandleSelectedFishBun(GameObject.FindGameObjectWithTag("bin"));
            return true;
        }

        return false;
    }

    void HandleToolShortcuts()
    {
        if (GameInput.KeyPressed(Key.Q))
        {
            SelectTool(tool => tool.CompareTag("kettle"));
            return;
        }

        if (TryGetPressedFilling(out FillingType filling))
            SelectTool(tool => tool.CompareTag("filling") && tool.filling == filling);
    }

    void SelectTool(Predicate<ToolController> predicate)
    {
        SetTouchMode(false);
        foreach (ToolController tool in FindObjectsByType<ToolController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (predicate(tool))
            {
                tool.Select();
                return;
            }
        }
    }

    static bool TryGetPressedFilling(out FillingType filling)
    {
        (Key numberKey, Key keypadKey, FillingType filling)[] shortcuts =
        {
            (Key.Digit1, Key.Numpad1, FillingType.redBean),
            (Key.Digit2, Key.Numpad2, FillingType.custard),
            (Key.Digit3, Key.Numpad3, FillingType.nutella),
            (Key.Digit4, Key.Numpad4, FillingType.creamCheese),
            (Key.Digit5, Key.Numpad5, FillingType.pizza),
            (Key.Digit6, Key.Numpad6, FillingType.mint),
            (Key.Digit7, Key.Numpad7, FillingType.greenTea),
            (Key.Digit8, Key.Numpad8, FillingType.sweetPotato),
        };

        foreach ((Key numberKey, Key keypadKey, FillingType value) in shortcuts)
        {
            if (GameInput.KeyPressed(numberKey) || GameInput.KeyPressed(keypadKey))
            {
                filling = value;
                return true;
            }
        }

        filling = default;
        return false;
    }

    void HandleTouchInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return;

        var touch = touchscreen.primaryTouch;
        UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
        if (!touch.press.isPressed && phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled)
            return;

        int fingerId = touch.touchId.ReadValue();
        Vector2 position = touch.position.ReadValue();
        SetTouchMode(true);

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            trackedFingerId = fingerId;
            swipeStart = position;
            canUseSwipe = CanStartViewSwipe(fingerId, position);
            return;
        }

        if (fingerId != trackedFingerId)
            return;

        if (phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            ResetSwipe();
            return;
        }

        if (phase != UnityEngine.InputSystem.TouchPhase.Ended)
            return;

        if (canUseSwipe && Managers.Game.isRunning && UI_Tutorial.AllowsManualViewSwitch)
        {
            Vector2 delta = position - swipeStart;
            float threshold = Mathf.Max(MinimumSwipePixels, Screen.height * SwipeScreenRatio);
            if (Mathf.Abs(delta.y) >= threshold && Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * VerticalSwipeRatio)
            {
                if (delta.y > 0f)
                    CameraController.Instance?.ShowCookingView();
                else
                    CameraController.Instance?.ShowCustomerView();
            }
        }

        ResetSwipe();
    }

    static bool CanStartViewSwipe(int fingerId, Vector2 position)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId))
            return false;

        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 world = camera.ScreenToWorldPoint(position);
        return Physics2D.OverlapPoint(world) == null;
    }

    static bool IsTouchPressed() => Touchscreen.current?.primaryTouch.press.isPressed == true;

    void ResetSwipe()
    {
        trackedFingerId = -1;
        canUseSwipe = false;
    }

    void SetTouchMode(bool touchMode)
    {
        if (isTouchMode == touchMode)
            return;

        isTouchMode = touchMode;
        TouchModeChanged?.Invoke(isTouchMode);
    }

    void HandleViewChanged(GameplayView view)
    {
        if (selectedFishBun != null)
            ShowPlacementTargets();
    }

    public void SelectFishBun(FishBunController fishBun)
    {
        if (fishBun == null || selectedFishBun == fishBun)
            return;

        ClearSelectedFishBun();
        ToolController.DeselectCurrent();
        selectedFishBun = fishBun;
        selectedFishBun.SetSelected(true);
        ShowPlacementTargets();
        SelectedFishBunChanged?.Invoke(selectedFishBun);
    }

    public void ClearSelectedFishBun(FishBunController expected = null)
    {
        if (selectedFishBun == null)
            return;

        if (expected != null && selectedFishBun != expected)
            return;

        FishBunController previous = selectedFishBun;
        selectedFishBun = null;
        ClearTargetHighlights();
        if (previous != null)
            previous.SetSelected(false);
        SelectedFishBunChanged?.Invoke(null);
    }

    public bool TryHandleSelectedFishBun(GameObject target)
    {
        if (selectedFishBun == null || target == null)
            return false;

        bool handled = selectedFishBun.TryPlaceOn(target);
        if (handled)
            ClearSelectedFishBun();
        return handled;
    }

    public void CancelSelection()
    {
        if (selectedFishBun != null)
            ClearSelectedFishBun();
        else
            ToolController.DeselectCurrent();
    }

    public void ShowToolTargets(ToolController tool)
    {
        ClearTargetHighlights();
        highlightedTool = tool;
        if (tool == null)
            return;

        foreach (FishBunController fishBun in FindObjectsByType<FishBunController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (fishBun.CanUseTool(tool))
                HighlightToolTarget(fishBun.gameObject);
        }
    }

    public void RefreshToolTargetHighlights()
    {
        if (highlightedTool != null)
            ShowToolTargets(highlightedTool);
    }

    public void ClearTargetHighlights()
    {
        foreach ((SpriteRenderer renderer, Color originalColor) in highlightedRenderers)
        {
            if (renderer != null)
                renderer.color = originalColor;
        }

        highlightedRenderers.Clear();

        foreach ((SpriteRenderer source, SpriteRenderer outline) in toolTargetOutlines)
        {
            if (outline != null)
                Destroy(outline.gameObject);
        }

        toolTargetOutlines.Clear();
        highlightedTool = null;
    }

    void ShowPlacementTargets()
    {
        ClearTargetHighlights();

        foreach (GameObject customer in GameObject.FindGameObjectsWithTag("customer"))
            Highlight(customer);

        GameObject display = GameObject.FindGameObjectWithTag("displayPlate");
        if (display != null)
            Highlight(display);

        GameObject bin = GameObject.FindGameObjectWithTag("bin");
        if (bin != null)
            Highlight(bin);
    }

    void Highlight(GameObject target)
    {
        SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || highlightedRenderers.ContainsKey(renderer))
            return;

        highlightedRenderers.Add(renderer, renderer.color);
        renderer.color = Color.Lerp(renderer.color, new Color(1f, 0.78f, 0.2f, renderer.color.a), 0.35f);
    }

    void HighlightToolTarget(GameObject target)
    {
        SpriteRenderer source = target.GetComponentInChildren<SpriteRenderer>();
        if (source == null || toolTargetOutlines.ContainsKey(source))
            return;

        GameObject outlineObject = new("InteractableOutline", typeof(SpriteRenderer));
        outlineObject.transform.SetParent(source.transform, false);

        SpriteRenderer outline = outlineObject.GetComponent<SpriteRenderer>();
        outline.sprite = source.sprite;
        outline.color = Color.white;
        outline.flipX = source.flipX;
        outline.flipY = source.flipY;
        outline.drawMode = source.drawMode;
        outline.size = source.size;
        outline.maskInteraction = source.maskInteraction;
        outline.sortingLayerID = source.sortingLayerID;
        outline.sortingOrder = source.sortingOrder - 1;
        outlineObject.transform.localScale = Vector3.one * ToolTargetOutlineMinScale;

        toolTargetOutlines.Add(source, outline);
    }

    void UpdateToolTargetOutlines()
    {
        float pulse = ToolTargetOutlineMinScale +
            (Mathf.Sin(Time.unscaledTime * ToolTargetOutlinePulseSpeed) + 1f) * 0.5f * ToolTargetOutlinePulseScale;

        foreach ((SpriteRenderer source, SpriteRenderer outline) in toolTargetOutlines)
        {
            if (source == null || outline == null)
                continue;

            outline.sprite = source.sprite;
            outline.flipX = source.flipX;
            outline.flipY = source.flipY;
            outline.drawMode = source.drawMode;
            outline.size = source.size;
            outline.sortingLayerID = source.sortingLayerID;
            outline.sortingOrder = source.sortingOrder - 1;
            outline.transform.localScale = Vector3.one * pulse;
        }
    }
}
