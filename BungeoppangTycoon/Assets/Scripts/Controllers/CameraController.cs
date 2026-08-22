using System;
using System.Collections;
using UnityEngine;

public enum GameplayView
{
    Customer,
    Cooking,
}

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }
    public static Action toggleCameraAction;
    public static event Action<GameplayView> ViewChanged;

    [SerializeField] float transitionDuration = 0.22f;

    readonly Vector3 cameraUpPos = new(0, 4.5f, -10);
    readonly Vector3 cameraDownPos = new(0, -4.3f, -10);
    Coroutine transitionRoutine;

    public GameplayView CurrentView { get; private set; } = GameplayView.Customer;
    public bool IsTransitioning => transitionRoutine != null;

    void Awake()
    {
        Instance = this;
        toggleCameraAction = ToggleCamera;
    }

    void Start()
    {
        SetView(GameplayView.Customer, true);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        toggleCameraAction = null;
    }

    // 기존 UI와 다른 스크립트에서 사용하던 이름을 유지합니다.
    public void toggleCamera()
    {
        ToggleCamera();
    }

    public void ToggleCamera()
    {
        if (IsTransitioning)
            return;

        SetView(CurrentView == GameplayView.Customer
            ? GameplayView.Cooking
            : GameplayView.Customer);
    }

    public void ShowCustomerView()
    {
        SetView(GameplayView.Customer);
    }

    public void ShowCookingView()
    {
        SetView(GameplayView.Cooking);
    }

    public void SetView(GameplayView view, bool immediate = false)
    {
        if (!immediate && (IsTransitioning || CurrentView == view))
            return;

        CurrentView = view;
        Vector3 target = view == GameplayView.Customer ? cameraUpPos : cameraDownPos;

        if (immediate || transitionDuration <= 0f)
        {
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);

            transitionRoutine = null;
            transform.position = target;
            NotifyViewChanged();
            return;
        }

        transitionRoutine = StartCoroutine(MoveTo(target));
    }

    IEnumerator MoveTo(Vector3 target)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / transitionDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            transform.position = Vector3.LerpUnclamped(start, target, eased);
            yield return null;
        }

        transform.position = target;
        transitionRoutine = null;
        NotifyViewChanged();
    }

    void NotifyViewChanged()
    {
        ViewChanged?.Invoke(CurrentView);
        TutorialSignals.Raise(TutorialEvent.ViewChanged, gameObject);
    }
}
