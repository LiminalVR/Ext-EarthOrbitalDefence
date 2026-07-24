using Liminal.SDK.V2;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Resolves the XRI ray interactor the player is currently aiming with. The upgraded platform
/// drives aiming through the ray interactors on the spawned rig (via LiminalControllerManager)
/// rather than the legacy IVRPointer, which is no longer updated.
/// </summary>
public static class PlayerAim
{
    public static XRRayInteractor GetRayInteractor()
    {
        var manager = LiminalControllerManager.Current;
        var rig = manager != null ? manager.XRRigReferences : null;
        if (rig == null)
            return null;

        var hand = VRDevice.Device?.PrimaryInputDevice?.Hand ?? VRInputDeviceHand.Right;
        if (hand == VRInputDeviceHand.None)
            hand = VRInputDeviceHand.Right;

        if (hand == VRInputDeviceHand.Left)
            return manager.IsHandTracked(VRInputDeviceHand.Left) ? rig.LeftHandRayInteractor : rig.LeftControllerRayInteractor;

        return manager.IsHandTracked(VRInputDeviceHand.Right) ? rig.RightHandRayInteractor : rig.RightControllerRayInteractor;
    }

    public static Transform GetAimOrigin(XRRayInteractor interactor)
        => interactor.rayOriginTransform != null ? interactor.rayOriginTransform : interactor.transform;
}
