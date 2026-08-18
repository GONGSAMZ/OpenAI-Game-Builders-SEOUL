using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    //public static CameraController instance;
    public static Action toggleCameraAction;

    bool isUpper = true;
    Vector3 cameraUpPos = new Vector3(0, 4.5f, -10);
    Vector3 cameraDownPos = new Vector3(0, -4.3f, -10);

    private void Start()
    {
        toggleCameraAction = toggleCamera;
        ShowCustomerView();
    }

    public void toggleCamera()
    {
        if (isUpper)
            ShowCookingView();
        else
            ShowCustomerView();
    }

    public void ShowCustomerView()
    {
        transform.position = cameraUpPos;
        isUpper = true;
    }

    public void ShowCookingView()
    {
        transform.position = cameraDownPos;
        isUpper = false;
    }
}
