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

    public JSONStorableStringChooser Passenger { get; set; }

    public override void Init()
    {
        try
        {
            InitControls();
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to initialize plugin: " + e);
        }
    }

    private void InitControls()
    {
        var atoms = SuperController.singleton.GetAtoms().Select(atom => atom.uid).ToList();
        atoms.Insert(0, "None");

        _atomJSON = new JSONStorableStringChooser("Passenger", atoms, "None", "Passenger");
        _atomJSON.storeType = JSONStorableParam.StoreType.Physical;
        RegisterStringChooser(_atomJSON);

        var linkPopup = CreateScrollablePopup(_atomJSON, false);
        linkPopup.popupPanelHeight = 600f;
    }
}