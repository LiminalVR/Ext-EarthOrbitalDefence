using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class Reticule : MonoBehaviour
{
    public Transform Crosshairs;
    public Button Button;
    public Image EnergyFill;
    public float FillSpeed;
    public float RaycastRadius = 15f;
    public float DefaultDistance = 100f;

    private float _targetFillValue;
    private bool _start = false;

    private void Start()
    {
        Button.onClick.AddListener(StartG);
        SetCrosshairVisibility(false);
        EnergyFill.fillAmount = 1;
    }


    private void FixedUpdate()
    {
        if (!_start)
            return;

        var rayInteractor = PlayerAim.GetRayInteractor();
        if (rayInteractor == null)
            return;

        // The upgraded platform aims with the rig's XRI ray interactors; the legacy
        // IVRPointer.CurrentRaycastResult is never updated any more. Raycast the world
        // ourselves from the interactor's ray origin, matching the spherecast Fire.cs shoots with.
        var aim = PlayerAim.GetAimOrigin(rayInteractor);
        var targetPoint = Physics.SphereCast(aim.position, RaycastRadius, aim.forward, out var hit, Mathf.Infinity)
            ? hit.point
            : aim.position + aim.forward * DefaultDistance;

        Crosshairs.SetPositionAndRotation(targetPoint, aim.rotation);
    }

    void StartG()
    {
        if (_start == false)
        {
            _start = true;
            Button.gameObject.SetActive(false);
            SetCrosshairVisibility(true);
            return;
        }

        if (_start == true)
        {
            _start = false;
            return;
        }
    }

    private void Update()
    {
        EnergyFill.fillAmount = Mathf.MoveTowards(EnergyFill.fillAmount, _targetFillValue, FillSpeed * Time.deltaTime);
    }

    public void SetTargetFillAmount(float targetValue)
    {
        _targetFillValue = targetValue;
    }

    public void SetCrosshairVisibility(bool state)
    {
        Crosshairs.gameObject.SetActive(state);
    }
}
