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
    private AnimationPattern _pattern;
    private UserPreferences _preferences;
    private AnimationStep _currentStep;

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

            if (_active)
            {
                // NOTE: activeStep is protected for some reason
                var target = _pattern.steps.FirstOrDefault(step => step.active);

                if (_currentStep != target)
                {
                    _currentStep = target;

                    {
                        var navigationRigRotation = target.transform.rotation;
                        if (!superController.MonitorRig.gameObject.activeSelf)
                            navigationRigRotation *= Quaternion.Euler(0, navigationRig.eulerAngles.y - _possessor.transform.eulerAngles.y, 0f);
                        navigationRig.rotation = navigationRigRotation;
                    }

                    {
                        var up = navigationRig.up;
                        var positionOffset = navigationRig.position + target.transform.position - _possessor.autoSnapPoint.position;
                        // Adjust the player height so the user can adjust as needed
                        var playerHeightAdjustOffset = Vector3.Dot(positionOffset - navigationRig.position, up);
                        navigationRig.position = positionOffset + up * -playerHeightAdjustOffset;
                        superController.playerHeightAdjust += playerHeightAdjustOffset;
                    }
                }
            }
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to update: " + e);
            Restore();
        }
    }

    public void OnDisable()
    {
        Restore();
    }

    private void Restore()
    {
        if (!_active)
            return;

        _currentStep = null;
        SuperController.singleton.navigationRig.rotation = _previousRotation;
        SuperController.singleton.navigationRig.position = _previousPosition;
        SuperController.singleton.playerHeightAdjust = _previousPlayerHeight;
        _active = false;
    }
}
