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
    public static class Modes
    {
        public const string None = "None";
        public const string WindowCamera = "WindowCamera";
        public const string NavigationRig = "NavigationRig";
    }

    public class NavigationRigBackup
    {
        private Vector3 _previousPosition;
        private float _previousPlayerHeight;
        private Quaternion _previousRotation;

        public static NavigationRigBackup Snapshot()
        {
            var value = new NavigationRigBackup();
            var navigationRig = SuperController.singleton.navigationRig;
            value._previousRotation = navigationRig.rotation;
            value._previousPosition = navigationRig.position;
            value._previousPlayerHeight = SuperController.singleton.playerHeightAdjust;
            return value;
        }

        public void Restore()
        {
            SuperController.singleton.navigationRig.rotation = _previousRotation;
            SuperController.singleton.navigationRig.position = _previousPosition;
            SuperController.singleton.playerHeightAdjust = _previousPlayerHeight;
        }
    }

    public class WindowCameraBackup
    {
        private Atom _atom;
        private FreeControllerV3 _controller;
        private bool _previousOn;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;

        public static WindowCameraBackup Snapshot(Atom windowCamera, FreeControllerV3 windowCameraController)
        {
            var value = new WindowCameraBackup();
            value._atom = windowCamera;
            value._controller = windowCameraController;
            value._previousOn = windowCamera.GetBoolJSONParam("on").val;
            value._previousRotation = windowCameraController.transform.rotation;
            value._previousPosition = windowCameraController.transform.position;
            return value;
        }

        public void Restore()
        {
            _controller.containingAtom.hidden = false;
            _atom.GetBoolJSONParam("on").val = false;
            _controller.transform.SetPositionAndRotation(_previousPosition, _previousRotation);
        }
    }

    public class Transition
    {
        private readonly JSONStorableFloat _camExposure;
        private readonly float _camExposureFrom;
        private readonly float _camExposureTo;
        private readonly float _duration;
        private readonly bool _destroyOnComplete;
        private float _transitionTime = 0f;

        public Transition(JSONStorableFloat camExposure, float camExposureFrom, float camExposureTo, float duration, bool destroyOnComplete)
        {
            _camExposure = camExposure;
            _camExposureFrom = camExposureFrom;
            _camExposureTo = camExposureTo;
            _duration = duration;
            _destroyOnComplete = destroyOnComplete;
        }

        public bool Update()
        {
            _transitionTime += Time.deltaTime;
            _camExposure.val = Mathf.Lerp(_camExposureFrom, _camExposureTo, _transitionTime / _duration);
            return _destroyOnComplete && _transitionTime >= _duration;
        }

        public void Complete()
        {
            _camExposure.val = _camExposureTo;
        }
    }

    private JSONStorableBool _activeJSON;
    private JSONStorableStringChooser _modeJSON;
    private JSONStorableFloat _transitionTimeJSON;
    private JSONStorableFloat _extendTransitionTime;

    private JSONStorableAction _playOnceFromBeginningActionJSON;

    private Possessor _possessor;
    private AnimationPattern _pattern;
    private Transition _transition;
    private AnimationStep _lastStep;
    private Atom _windowCamera;
    private FreeControllerV3 _windowCameraController;
    private JSONStorableBool _activePassenger;
    private JSONStorableFloat _speedJSON;
    private JSONStorableFloat _camExposureJSON;

    private bool _navigationRigActive;
    private bool _windowCameraActive;
    private bool _failedOnce;
    private float _camExposureBackup;
    private NavigationRigBackup _navigationRigBackup;
    private WindowCameraBackup _windowCameraBackup;
    private float _previousPatternTimeCounter;
    private bool _deactivateOnComplete;

    public override void Init()
    {
        try
        {
            _pattern = containingAtom.GetComponentInChildren<AnimationPattern>();
            if (_pattern == null) throw new Exception("The Director plugin can only be applied on AnimationPattern.");
            _possessor = SuperController.singleton.centerCameraTarget.transform.GetComponent<Possessor>();
            _speedJSON = _pattern.GetFloatJSONParam("speed");
            _camExposureJSON = GameObject.FindObjectOfType<SkyshopLightController>()?.GetFloatJSONParam("camExposure");

            InitControls();
            UpdateActivation();
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to initialize plugin: " + e);
        }
    }

    private void InitControls()
    {
        // Left side
        _activeJSON = new JSONStorableBool(
            "Active",
            false,
            new JSONStorableBool.SetBoolCallback(val => UpdateActivation())
        );
        RegisterBool(_activeJSON);
        CreateToggle(_activeJSON, false);

        var defaultMode = Modes.None;
#if (VAM_DIAGNOSTICS)
        defaultMode = Modes.WindowCamera;
#endif
        _modeJSON = new JSONStorableStringChooser(
            "Mode",
            (new[] { Modes.None, Modes.NavigationRig, Modes.WindowCamera }).ToList(),
            defaultMode,
            "Mode",
            new JSONStorableStringChooser.SetStringCallback(val => UpdateActivation())
        );
        RegisterStringChooser(_modeJSON);
        var activePopup = CreateScrollablePopup(_modeJSON, false);
        activePopup.popupPanelHeight = 600f;

        _transitionTimeJSON = new JSONStorableFloat("Transition Time", 0.6f, 0f, 1f, false);
        RegisterFloat(_transitionTimeJSON);
        CreateSlider(_transitionTimeJSON, false);

        _extendTransitionTime = new JSONStorableFloat("Extend Transition Time", 0.2f, 0f, 1f, false);
        RegisterFloat(_extendTransitionTime);
        CreateSlider(_extendTransitionTime, false);

        // Right side
        var currentTimeJSON = _pattern.GetFloatJSONParam("currentTime");
        if (currentTimeJSON == null)
            throw new NullReferenceException("There was no currentTime JSON param on this animation pattern.");

        _playOnceFromBeginningActionJSON = new JSONStorableAction("Play From Beginning", () => PlayOnceFromBeginning());
        RegisterAction(_playOnceFromBeginningActionJSON);
        CreateButton("Play Once From Beginning", true).button.onClick.AddListener(() => PlayOnceFromBeginning());
        CreateButton("Play", true).button.onClick.AddListener(() => _pattern.Play());
        CreateButton("Pause", true).button.onClick.AddListener(() => _pattern.Pause());
        CreateButton("Previous Step", true).button.onClick.AddListener(() =>
        {
            var previousStep = _pattern.steps.Reverse().SkipWhile(s => !s.active).Skip(1).FirstOrDefault();
            if (previousStep != null)
                currentTimeJSON.val = previousStep.timeStep;
        });
        CreateButton("Next Step", true).button.onClick.AddListener(() =>
        {
            var nextStep = _pattern.steps.SkipWhile(s => !s.active).Skip(1).FirstOrDefault();
            if (nextStep != null)
                currentTimeJSON.val = nextStep.timeStep;
        });
    }

    private void PlayOnceFromBeginning()
    {
        _pattern.ResetAndPlay();
        _deactivateOnComplete = true;
        _activeJSON.val = true;
    }

    private void UpdateActivation()
    {
        Deactivate();
        if (!_activeJSON.val) return;

        _camExposureBackup = _camExposureJSON.val;
        switch (_modeJSON.val)
        {
            case Modes.NavigationRig:
                ActivateNavigationRig();
                break;
            case Modes.WindowCamera:
                ActivateWindowCamera();
                break;
        }
    }

    private void ActivateNavigationRig()
    {
        _failedOnce = false;
        _navigationRigBackup = NavigationRigBackup.Snapshot();
        _navigationRigActive = true;
    }

    private void ActivateWindowCamera()
    {
        _windowCamera = SuperController.singleton.GetAtomByUid("WindowCamera");
        if (_windowCamera == null)
        {
            SuperController.LogError("There is no 'WindowCamera' atom in the scene, maybe you deleted or renamed it?");
            Deactivate();
            _modeJSON.val = Modes.None;
            return;
        }

        _failedOnce = false;
        _windowCameraController = _windowCamera.freeControllers[0];
        _windowCameraBackup = WindowCameraBackup.Snapshot(_windowCamera, _windowCameraController);
        _windowCamera.GetBoolJSONParam("on").val = true;
        _windowCameraController.containingAtom.hidden = true;
        _windowCameraActive = true;
    }

    private void Deactivate()
    {
        _lastStep = null;
        _transition = null;

        if (_camExposureBackup != 0f)
        {
            _camExposureJSON.val = _camExposureBackup;
            _camExposureBackup = 0f;
        }

        if (_navigationRigActive)
        {
            RestorePassenger();
            _navigationRigBackup?.Restore();
            _navigationRigBackup = null;
            _navigationRigActive = false;
        }

        if (_windowCameraActive)
        {
            _windowCameraBackup?.Restore();
            _windowCameraBackup = null;
            _windowCameraActive = false;
        }
    }

    public void Update()
    {
        try
        {
            if (!_navigationRigActive && !_windowCameraActive || !_pattern.isActiveAndEnabled)
                return;

            // NOTE: _pattern.isPlaying is protected for some reason
            var currentPatternTimeCounter = _pattern.GetCurrentTimeCounter();
            var isPlaying = currentPatternTimeCounter != _previousPatternTimeCounter;
            _previousPatternTimeCounter = currentPatternTimeCounter;

            // NOTE: _pattern.activeStep is protected for some reason
            var currentStep = _pattern.steps.FirstOrDefault(step => step.active);

            if (currentStep == null)
            {
#if (VAM_DIAGNOSTICS)
                PrintDebugInfo();
#endif
                return;
            }

            if (_deactivateOnComplete && _pattern.GetCurrentTimeCounter() == _pattern.GetTotalTime())
            {
                _deactivateOnComplete = false;
                _activeJSON.val = false;
                _pattern.Pause();
                _pattern.ResetAnimation();
                return;
            }

            if (isPlaying)
            {
                if (_transition != null)
                {
                    UpdateTransition();
                }
                else
                {
                    float outTransitionStartTime = _transitionTimeJSON.val * _speedJSON.val + _extendTransitionTime.val * _speedJSON.val;
                    if (_pattern.GetCurrentTimeCounter() >= currentStep.timeStep + currentStep.transitionToTime - outTransitionStartTime)
                        CreateOutTransition();
                }
            }

            if (_windowCameraActive)
                UpdateWindowCamera(currentStep);

            if (_lastStep == currentStep)
                return;

            if (isPlaying)
                CreateInTransition();

            _lastStep = currentStep;

            if (_navigationRigActive)
            {
                RestorePassenger();

                _activePassenger = GetStepPassengerTarget(currentStep);
                if (_activePassenger != null)
                {
                    _activePassenger.SetVal(true);
                }
                else
                {
                    UpdateNavigationRigRotation(currentStep);
                    UpdateNavigationRigPosition(currentStep);
                }
            }

#if (VAM_DIAGNOSTICS)
            PrintDebugInfo();
#endif
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to update: " + e);
            Deactivate();
        }
    }

    private void CreateInTransition()
    {
        _transition?.Complete();
        // TODO: 1f is the transition time. Set to null if disabled, option for fade or black. Get transition overrides from step. Start and end transition.
        _transition = new Transition(_camExposureJSON, 0f, _camExposureBackup, _transitionTimeJSON.val, true);
    }

    private void CreateOutTransition()
    {
        _transition?.Complete();
        _transition = new Transition(_camExposureJSON, _camExposureBackup, 0f, _transitionTimeJSON.val, false);
    }

    private void UpdateTransition()
    {
        if (!_transition.Update())
            return;

        _transition.Complete();
        _transition = null;
    }

    private void UpdateWindowCamera(AnimationStep currentStep)
    {
        _windowCameraController.transform.SetPositionAndRotation(currentStep.transform.position, currentStep.transform.rotation);
    }

    private void UpdateNavigationRigPosition(AnimationStep currentStep)
    {
        var navigationRig = SuperController.singleton.navigationRig;

        var up = navigationRig.up;
        var positionOffset = navigationRig.position + currentStep.transform.position - _possessor.autoSnapPoint.position;
        // Adjust the player height so the user can adjust as needed
        var playerHeightAdjustOffset = Vector3.Dot(positionOffset - navigationRig.position, up);
        navigationRig.position = positionOffset + up * -playerHeightAdjustOffset;
        SuperController.singleton.playerHeightAdjust += playerHeightAdjustOffset;
    }

    private void UpdateNavigationRigRotation(AnimationStep currentStep)
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
            FailOnce("Atom " + passengerAtomID + " specified in step " + currentStep.containingAtom.name + " does not exit");
            return null;
        }

        var passengerStorableID = passengerAtom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("Passenger"));
        if (passengerStorableID == null)
        {
            FailOnce("Atom " + passengerAtomID + " does not have the Passenger script");
            return null;
        }

        var passengerStorable = passengerAtom.GetStorableByID(passengerStorableID);

        return passengerStorable?.GetBoolJSONParam("Active");
    }

    private void FailOnce(string message)
    {
        if (_failedOnce) return;
        SuperController.LogError(message);
        _failedOnce = true;
    }

    private void RestorePassenger()
    {
        if (_activePassenger == null)
            return;

        _activePassenger.val = false;
        _activePassenger = null;
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
            return;
        }
        var info = new System.Collections.Generic.List<string>();
        info.Add("Step " + _lastStep.containingAtom.name);
        if (_navigationRigActive) info.Add(" [cam]");
        if (_windowCameraActive) info.Add(" [win]");
        var target = GetStepPassengerTarget(_lastStep);
        if (target != null)
            info.Add(" [passenger]");
        if (_deactivateOnComplete)
            info.Add(" [once]");
        info.Add(" time: " + _pattern.GetCurrentTimeCounter() + "s, step time: " + _lastStep.timeStep + "s/" + _pattern.GetTotalTime() + "s, dur: " + _lastStep.transitionToTime + "s, speed: " + _speedJSON.val);

        SuperController.LogMessage("Director: " + string.Join(", ", info.ToArray()));
    }
#endif
}
