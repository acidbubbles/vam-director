#define VAM_DIAGNOSTICS
using System;
using System.Linq;

/// <summary>
/// Director Version 0.0.0
/// Configures specific steps
/// </summary>
public class DirectorStep : MVRScript
{
    private JSONStorableStringChooser _atomJSON;
    private AnimationStep _step;

    public JSONStorableStringChooser Passenger { get; set; }

    public override void Init()
    {
        try
        {
            _step = containingAtom.parentAtom.GetComponentInChildren<AnimationStep>();
            if (_step == null)
                throw new Exception("Can only apply DirectorStep on AnimationStep");

            InitControls();
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to initialize plugin: " + e);
        }
    }

    private void InitControls()
    {
        var atoms = SuperController.singleton.GetAtoms().Select(atom => atom.uid).OrderBy(uid => uid).ToList();
        atoms.Insert(0, "None");

        _atomJSON = new JSONStorableStringChooser("Passenger", atoms, "None", "Passenger");
        _atomJSON.storeType = JSONStorableParam.StoreType.Physical;
        RegisterStringChooser(_atomJSON);

        var linkPopup = CreateScrollablePopup(_atomJSON, false);
        linkPopup.popupPanelHeight = 600f;

        CreateButton("Set Time To This Step", true).button.onClick.AddListener(() =>
        {
            _step.animationParent.GetFloatJSONParam("currentTime").val = _step.timeStep;
        });
        CreateButton("Select Animation Pattern", true).button.onClick.AddListener(() =>
        {
            SuperController.singleton.SelectController(_step.animationParent.containingAtom.freeControllers.FirstOrDefault(fc => fc.name == "control"));
        });
    }
}