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
    readonly Dictionary<SpriteRenderer, SpriteRenderer> toolTargetOutlines = new();
    MaterialPropertyBlock outlinePropertyBlock;
    static Material interactableOutlineMaterial;
    int spriteUvRectId;

    // 모든 상호작용 대상에 공통으로 쓰는 흰색 외곽선이다.
    const string InteractableOutlineMaterialPath = "Materials/InteractableOutline";
    const float ToolTargetOutlinePulseSpeed = 5f;
    const float ToolTargetOutlineMinAlpha = 0.45f;
    const float ToolTargetOutlineMaxAlpha = 1f;

    public FishBunController SelectedFishBun => selectedFishBun;
    public bool IsTouchMode => isTouchMode;

    void Awake()
    {
        Instance = this;
        // Unity API는 MonoBehaviour 필드 초기화 때 호출하면 안 된다.
        // Awake 이후에 ID를 만들면 모든 손님의 외곽선에 안전하게 적용된다.
        spriteUvRectId = Shader.PropertyToID("_SpriteUVRect");
        outlinePropertyBlock = new MaterialPropertyBlock();
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

        // 조리대 위 완성품은 진열대에 먼저 올려야 한다.
        // 진열된 붕어빵을 선택했을 때만 손님을 전달 대상으로 표시한다.
        if (selectedFishBun != null && selectedFishBun.IsOnDisplay)
        {
            foreach (CustomerController customer in FindObjectsByType<CustomerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // 주문을 받은 손님 전체가 아니라, 지금 든 붕어빵 맛을 실제로 주문한 손님만 표시한다.
                if (customer.CanReceiveSelectedFishBun(selectedFishBun.fillingType))
                    HighlightTargetOutline(customer.Customer);
            }
        }

        GameObject display = GameObject.FindGameObjectWithTag("displayPlate");
        if (display != null)
            HighlightTargetOutline(display);

        GameObject bin = GameObject.FindGameObjectWithTag("bin");
        if (bin != null)
            HighlightTargetOutline(bin);
    }

    void HighlightTargetOutline(GameObject target)
    {
        SpriteRenderer source = target.GetComponentInChildren<SpriteRenderer>();
        CreateOutline(source);
    }

    void HighlightToolTarget(GameObject target)
    {
        SpriteRenderer source = target.GetComponentInChildren<SpriteRenderer>();
        CreateOutline(source);
    }

    void CreateOutline(SpriteRenderer source)
    {
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
        outline.sharedMaterial = GetInteractableOutlineMaterial();
        ApplyOutlineSpriteUvRect(source.sprite, outline);

        toolTargetOutlines.Add(source, outline);
    }

    void UpdateToolTargetOutlines()
    {
        // 원본 아트는 건드리지 않고, 셰이더가 만든 외곽선의 밝기만 맥박시킨다.
        float pulse = (Mathf.Sin(Time.unscaledTime * ToolTargetOutlinePulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(ToolTargetOutlineMinAlpha, ToolTargetOutlineMaxAlpha, pulse);

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
            ApplyOutlineSpriteUvRect(source.sprite, outline);
            outline.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    void ApplyOutlineSpriteUvRect(Sprite sprite, SpriteRenderer outline)
    {
        if (sprite == null || outline == null)
            return;

        Vector2[] uvs = sprite.uv;
        float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
        foreach (Vector2 uv in uvs)
        {
            minX = Mathf.Min(minX, uv.x);
            minY = Mathf.Min(minY, uv.y);
            maxX = Mathf.Max(maxX, uv.x);
            maxY = Mathf.Max(maxY, uv.y);
        }

        // 표정 시트의 이 스프라이트 영역 밖은 셰이더가 읽지 못하게 막는다.
        outline.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetVector(spriteUvRectId, new Vector4(minX, minY, maxX, maxY));
        outline.SetPropertyBlock(outlinePropertyBlock);
    }

    static Material GetInteractableOutlineMaterial()
    {
        if (interactableOutlineMaterial == null)
            interactableOutlineMaterial = Resources.Load<Material>(InteractableOutlineMaterialPath);

        return interactableOutlineMaterial;
    }
}
