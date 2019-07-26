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
                Restore();
                return;
            }

            var superController = SuperController.singleton;
            var navigationRig = superController.navigationRig;

            if (!_active && _activeJSON.val)
            {
                _previousRotation = navigationRig.rotation;
                _previousPosition = navigationRig.position;
                _previousPlayerHeight = superController.playerHeightAdjust;
                _active = true;
            }

            // NOTE: activeStep is protected for some reason
            var currentStep = _pattern.steps.FirstOrDefault(step => step.active);

            if (_active)
            {
                if (_lastStep != currentStep)
                {
                    _lastStep = currentStep;

                    {
                        var navigationRigRotation = currentStep.transform.rotation;
                        if (!superController.MonitorRig.gameObject.activeSelf)
                            navigationRigRotation *= Quaternion.Euler(0, navigationRig.eulerAngles.y - _possessor.transform.eulerAngles.y, 0f);
                        navigationRig.rotation = navigationRigRotation;
                    }

                    {
                        var up = navigationRig.up;
                        var positionOffset = navigationRig.position + currentStep.transform.position - _possessor.autoSnapPoint.position;
                        // Adjust the player height so the user can adjust as needed
                        var playerHeightAdjustOffset = Vector3.Dot(positionOffset - navigationRig.position, up);
                        navigationRig.position = positionOffset + up * -playerHeightAdjustOffset;
                        superController.playerHeightAdjust += playerHeightAdjustOffset;
                    }
                }
            }

            if (_attachWindowCameraJSON.val)
            {
                if (_windowCamera == null)
                {
                    _windowCamera = superController.GetAtomByUid("WindowCamera");
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
            else if (_windowCamera != null)
            {
                RestoreWindowCamera();
            }
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to update: " + e);
            Restore();
        }
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
        Restore();
        RestoreWindowCamera();
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
