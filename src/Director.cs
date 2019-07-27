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
    private JSONStorableStringChooser _activeJSON;
    private Vector3 _previousPosition;
    private float _previousPlayerHeight;
    private Quaternion _previousRotation;
    private bool _cameraActive;
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
            UpdateActivation(_activeJSON.val);
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to initialize plugin: " + e);
        }
    }

    private void InitControls()
    {
        _activeJSON = new JSONStorableStringChooser("Active", (new[] { "None", "Camera", "WindowCamera" }).ToList(), "None", "Activation Mode", val => UpdateActivation(val));
        RegisterStringChooser(_activeJSON);
        var activePopup = CreateScrollablePopup(_activeJSON, false);
        activePopup.popupPanelHeight = 600f;
    }

    private void UpdateActivation(string val)
    {
        Deactivate();
        switch (val)
        {
            case "Camera":
                ActivateCamera();
                break;
            case "WindowCamera":
                ActivateWindowCamera();
                break;
        }
    }

    private void ActivateCamera()
    {
        var navigationRig = SuperController.singleton.navigationRig;
        _previousRotation = navigationRig.rotation;
        _previousPosition = navigationRig.position;
        _previousPlayerHeight = SuperController.singleton.playerHeightAdjust;
        _cameraActive = true;
    }

    private void ActivateWindowCamera()
    {
        _windowCameraActive = true;
    }

    private void Deactivate()
    {
        _lastStep = null;

        if (_cameraActive)
        {
            RestorePassenger();
            RestoreCameraPosition();
            _cameraActive = false;
        }

        if (_windowCameraActive)
        {
            RestoreWindowCamera();
            _windowCameraActive = false;
        }
    }

    public void Update()
    {
        try
        {
            if (!_cameraActive && !_windowCameraActive)
                return;

            // NOTE: activeStep is protected for some reason
            var currentStep = _pattern.steps.FirstOrDefault(step => step.active);

            if (_lastStep != currentStep)
            {
                _lastStep = currentStep;


                if (_windowCameraActive)
                {
                    ApplyWindowCamera(currentStep);
                }

                if (_cameraActive)
                {
                    RestorePassenger();

                    _activePassenger = GetStepPassengerTarget(currentStep);
                    if (_activePassenger != null)
                    {
                        _activePassenger.SetVal(true);
                    }
                    else
                    {
                        ApplyRotation(currentStep);
                        ApplyPosition(currentStep);
                    }
                }
            }
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to update: " + e);
            RestoreCameraPosition();
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

    private JSONStorableBool GetStepPassengerTarget(AnimationStep currentStep)
    {
        var directorStepStorableID = currentStep.containingAtom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("DirectorStep"));
        if (directorStepStorableID == null) return null;
        // if (directorStepStorableID == null) SuperController.LogMessage(string.Join(", ", currentStep.containingAtom.GetStorableIDs().ToArray()));
        var directorStepStorable = currentStep.containingAtom.GetStorableByID(directorStepStorableID);

        var stepPassenger = directorStepStorable.GetStringChooserParamValue("Passenger");
        if (stepPassenger == "None")
            return null;

        var passengerAtom = SuperController.singleton.GetAtomByUid(stepPassenger);
        if (passengerAtom == null)
            return null;

        var passengerStorableID = containingAtom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("Passenger"));
        if (passengerStorableID == null) return null;
        var passengerStorable = currentStep.containingAtom.GetStorableByID(passengerStorableID);

        return passengerStorable?.GetBoolJSONParam("Active");
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

    private void RestoreCameraPosition()
    {
        SuperController.singleton.navigationRig.rotation = _previousRotation;
        SuperController.singleton.navigationRig.position = _previousPosition;
        SuperController.singleton.playerHeightAdjust = _previousPlayerHeight;
    }

    public void OnDisable()
    {
        Deactivate();
    }
}
