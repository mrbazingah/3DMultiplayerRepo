using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Remembers which prefabs you unticked (always skip) or ticked from the skipped list (always include)
// in the Prop Registration Tool. Stored by prefab GUID so it survives renames and moves.
[FilePath("ProjectSettings/PropRegistrationChoices.asset", FilePathAttribute.Location.ProjectFolder)]
public class PropRegistrationChoices : ScriptableSingleton<PropRegistrationChoices>
{
    [SerializeField] List<string> skippedGuids = new List<string>();
    [SerializeField] List<string> includedGuids = new List<string>();

    public bool IsSkipped(string guid)
    {
        return !string.IsNullOrEmpty(guid) && skippedGuids.Contains(guid);
    }

    public bool IsIncluded(string guid)
    {
        return !string.IsNullOrEmpty(guid) && includedGuids.Contains(guid);
    }

    public void Skip(string guid)
    {
        includedGuids.Remove(guid);
        if (!skippedGuids.Contains(guid))
        {
            skippedGuids.Add(guid);
        }
        Save(true);
    }

    public void Include(string guid)
    {
        skippedGuids.Remove(guid);
        if (!includedGuids.Contains(guid))
        {
            includedGuids.Add(guid);
        }
        Save(true);
    }

    public void Clear()
    {
        skippedGuids.Clear();
        includedGuids.Clear();
        Save(true);
    }
}
