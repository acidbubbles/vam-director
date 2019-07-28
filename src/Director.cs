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
    private const string ActionNone = "None";
    private const string ActionWindowCamera = "WindowCamera";
    private const string ActionNavigationRig = "NavigationRig";

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
    private bool _failedOnce;

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
        var defaultActive = ActionNone;
#if (VAM_DIAGNOSTICS)
        defaultActive = ActionWindowCamera;
#endif
        _activeJSON = new JSONStorableStringChooser("Active", (new[] { ActionNone, ActionNavigationRig, ActionWindowCamera }).ToList(), defaultActive, "Activation Mode", val => UpdateActivation(val));
        RegisterStringChooser(_activeJSON);
        var activePopup = CreateScrollablePopup(_activeJSON, false);
        activePopup.popupPanelHeight = 600f;
    }

    private void UpdateActivation(string val)
    {
        Deactivate();
        switch (val)
        {
            case ActionNavigationRig:
                ActivateCamera();
                break;
            case ActionWindowCamera:
                ActivateWindowCamera();
                break;
        }
    }

    private void ActivateCamera()
    {
        _failedOnce = false;
        var navigationRig = SuperController.singleton.navigationRig;
        _previousRotation = navigationRig.rotation;
        _previousPosition = navigationRig.position;
        _previousPlayerHeight = SuperController.singleton.playerHeightAdjust;
        _cameraActive = true;
    }

    private void ActivateWindowCamera()
    {
        _failedOnce = false;
        _windowCameraActive = true;
    }

    private void Deactivate()
    {
        _lastStep = null;

        if (_cameraActive)
        {
            RestorePassenger();
            RestoreNavigationRig();
            _cameraActive = false;
        }

        if (_windowCameraActive)
        {
            RestoreWindowCameraAtom();
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

#if (VAM_DIAGNOSTICS)
                PrintDebugInfo();
#endif
            }
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to update: " + e);
            RestoreNavigationRig();
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
        // Get the step's DirectorStep script
        var directorStepStorableID = currentStep.containingAtom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("DirectorStep"));
        if (directorStepStorableID == null) return null;
        var directorStepStorable = currentStep.containingAtom.GetStorableByID(directorStepStorableID);

        // Get the Passenger setting (an atom ID)
        var passengerAtomID = directorStepStorable.GetStringChooserParamValue("Passenger");
        if (passengerAtomID == "None") return null;

        // Get the specified atom
        var passengerAtom = SuperController.singleton.GetAtomByUid(passengerAtomID);
        if (passengerAtom == null)
        {
            if (!_failedOnce)
            {
                SuperController.LogError("Atom " + passengerAtomID + " specified in step " + currentStep.containingAtom.name + " does not exit");
                _failedOnce = true;
            }
            return null;
        }

        var passengerStorableID = passengerAtom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("Passenger"));
        if (passengerStorableID == null)
        {
            if (!_failedOnce)
            {
                SuperController.LogError("Atom " + passengerAtomID + " does not have the Passenger script");
                _failedOnce = true;
            }
            return null;
        }

        var passengerStorable = passengerAtom.GetStorableByID(passengerStorableID);

        return passengerStorable?.GetBoolJSONParam("Active");
    }

    private void RestorePassenger()
    {
        if (_activePassenger == null)
            return;

        _activePassenger.val = false;
        _activePassenger = null;
    }

    private void RestoreWindowCameraAtom()
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

    private void RestoreNavigationRig()
    {
        SuperController.singleton.navigationRig.rotation = _previousRotation;
        SuperController.singleton.navigationRig.position = _previousPosition;
        SuperController.singleton.playerHeightAdjust = _previousPlayerHeight;
    }

    public void OnDisable()
    {
        Deactivate();
    }

#if (VAM_DIAGNOSTICS)
    private void PrintDebugInfo()
    {
        SuperController.singleton.ClearMessages();
        if (_lastStep == null)
        {
            SuperController.LogMessage("Director: Step (null)");
        }
        var info = new System.Collections.Generic.List<string>();
        info.Add("Step " + _lastStep.containingAtom.name);
        if (_cameraActive) info.Add(" [cam]");
        if (_windowCameraActive) info.Add(" [win]");
        var target = GetStepPassengerTarget(_lastStep);
        if (target != null)
            info.Add(" [passenger]");

        SuperController.LogMessage("Director: " + string.Join(", ", info.ToArray()));
    }
#endif
}
