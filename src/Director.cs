#define VAM_DIAGNOSTICS
using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Director Version 0.0.0
/// Moves the VR camera to specific positions in the scene
/// </summary>
public class Director : MVRScript
{
    private Possessor _possessor;
    private FreeControllerV3 _headControl;
    private JSONStorableBool _activeJSON;
    private Vector3 _previousPosition;
    private float _previousPlayerHeight;
    private Quaternion _previousRotation;
    private bool _active;
    private bool _windowCameraActive;
    private AnimationPattern _pattern;
    private UserPreferences _preferences;
    private AnimationStep _lastStep;
    private JSONStorableBool _attachWindowCameraJSON;
    private Atom _windowCamera;
    private FreeControllerV3 _windowCameraController;
    private JSONStorableBool _activePassenger;

    public override void Init()
    {
        try
        {
            _pattern = containingAtom.GetComponentInChildren<AnimationPattern>();
            _possessor = SuperController.singleton.centerCameraTarget.transform.GetComponent<Possessor>();

            InitControls();
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to initialize plugin: " + e);
        }
    }

    private void InitControls()
    {
        _activeJSON = new JSONStorableBool("Active", false);
        RegisterBool(_activeJSON);
        var activeToggle = CreateToggle(_activeJSON, false);

        // TODO: Changing this while active can screw up things.
        _attachWindowCameraJSON = new JSONStorableBool("WindowCamera", false);
        RegisterBool(_attachWindowCameraJSON);
        var attachWindowCameraToggle = CreateToggle(_attachWindowCameraJSON, false);
        attachWindowCameraToggle.label = "Attach WindowCamera (setup)";
    }

    public void Update()
    {
        try
        {
            if (_active && !_activeJSON.val)
            {
                RestorePassenger();
                Restore();
                return;
            }

            if (!_active && _activeJSON.val)
            {
                var navigationRig = SuperController.singleton.navigationRig;
                _previousRotation = navigationRig.rotation;
                _previousPosition = navigationRig.position;
                _previousPlayerHeight = SuperController.singleton.playerHeightAdjust;
                _active = true;
            }

            // NOTE: activeStep is protected for some reason
            var currentStep = _pattern.steps.FirstOrDefault(step => step.active);

            if (_active)
            {
                if (_lastStep != currentStep)
                {
                    _lastStep = currentStep;

                    RestorePassenger();
                    if (!ApplyPassenger(currentStep))
                    {
                        ApplyRotation(currentStep);
                        ApplyPosition(currentStep);
                    }
                }
            }

            if (_attachWindowCameraJSON.val)
                ApplyWindowCamera(currentStep);
            else if (_windowCamera != null)
                RestoreWindowCamera();
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to update: " + e);
            Restore();
        }
    }

    private void ApplyWindowCamera(AnimationStep currentStep)
    {
        if (_windowCamera == null)
        {
            _windowCamera = SuperController.singleton.GetAtomByUid("WindowCamera");
            if (_windowCamera == null)
            {
                SuperController.LogError("There is no WindowCamera atom in the scene, maybe you deleted or renamed it?");
                _attachWindowCameraJSON.val = false;
                return;
            }
            _windowCamera.GetBoolJSONParam("on").val = true;
            _windowCameraController = _windowCamera.freeControllers[0];
            _windowCameraController.canGrabPosition = false;
            _windowCameraController.canGrabRotation = false;
        }
        _windowCameraController.transform.SetPositionAndRotation(currentStep.transform.position, currentStep.transform.rotation);
    }

    private void ApplyPosition(AnimationStep currentStep)
    {
        var navigationRig = SuperController.singleton.navigationRig;

        var up = navigationRig.up;
        var positionOffset = navigationRig.position + currentStep.transform.position - _possessor.autoSnapPoint.position;
        // Adjust the player height so the user can adjust as needed
        var playerHeightAdjustOffset = Vector3.Dot(positionOffset - navigationRig.position, up);
        navigationRig.position = positionOffset + up * -playerHeightAdjustOffset;
        SuperController.singleton.playerHeightAdjust += playerHeightAdjustOffset;
    }

    private void ApplyRotation(AnimationStep currentStep)
    {
        var navigationRig = SuperController.singleton.navigationRig;
        var navigationRigRotation = currentStep.transform.rotation;
        if (!SuperController.singleton.MonitorRig.gameObject.activeSelf)
            navigationRigRotation *= Quaternion.Euler(0, navigationRig.eulerAngles.y - _possessor.transform.eulerAngles.y, 0f);
        navigationRig.rotation = navigationRigRotation;
    }

    private bool ApplyPassenger(AnimationStep currentStep)
    {
        // TODO: Check with EndsWith, don't rely on being the first
        var stepPlugin = currentStep.containingAtom.GetStorableByID("plugin#0_DirectorStep");
        if (stepPlugin == null)
        {
            return false;
        }
        var stepPassenger = stepPlugin.GetStringChooserParamValue("Passenger");
        if (stepPassenger == "None")
        {
#if (VAM_DIAGNOSTICS)
            SuperController.LogMessage("No passenger target");
#endif
            return false;
        }
        var passengerAtom = SuperController.singleton.GetAtomByUid(stepPassenger);
        if (passengerAtom == null)
        {
#if (VAM_DIAGNOSTICS)
            SuperController.LogMessage("Could not find the specified atom");
#endif
            return false;
        }
        // TODO: Check with EndsWith, don't rely on being the first
        var passengerPlugin = passengerAtom.containingAtom.GetStorableByID("plugin#0_Passenger");
        if (passengerPlugin == null)
        {
#if (VAM_DIAGNOSTICS)
            SuperController.LogMessage("Could not find the passenger plugin storable");
#endif
            return false;
        }
        _activePassenger = passengerPlugin.GetBoolJSONParam("Active");
        if (_activePassenger == null)
        {
#if (VAM_DIAGNOSTICS)
            SuperController.LogMessage("Could not find the passenger active storable");
#endif
            return false;
        }
        _activePassenger.val = true;
        return true;
    }

    private void RestorePassenger()
    {
        if (_activePassenger == null)
            return;

        _activePassenger.val = false;
        _activePassenger = null;
    }

    private void RestoreWindowCamera()
    {
        if (_windowCamera)
        {
            _windowCamera.GetBoolJSONParam("on").val = false;
            _windowCamera = null;
        }

        if (_windowCameraController)
        {
            _windowCameraController.canGrabPosition = true;
            _windowCameraController.canGrabRotation = true;
            _windowCameraController = null;
        }
    }

    public void OnDisable()
    {
        RestorePassenger();
        RestoreWindowCamera();
        Restore();
    }

    private void Restore()
    {
        if (!_active)
            return;

        _active = false;
        _lastStep = null;

        SuperController.singleton.navigationRig.rotation = _previousRotation;
        SuperController.singleton.navigationRig.position = _previousPosition;
        SuperController.singleton.playerHeightAdjust = _previousPlayerHeight;
    }
}
