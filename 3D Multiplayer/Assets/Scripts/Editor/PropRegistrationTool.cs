using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Prop Registration Tool
//
// Scans everything under a root object (default "Map") in the open scene and finds prop
// prefabs that aren't registered yet. For each one in the list, Register will:
//   1. add NetworkObject + Prop to the prefab asset and set the whole prefab to the Prop layer,
//      and add a convex MeshCollider if the prefab has no collider on its root
//   2. move the prefab asset into the props folder
//   3. add a new value to the Prop.PropType enum in Prop.cs
//   4. add an entry for it to the PropRegistry prefab
// It also lists prefabs that already have a Prop script but are missing from the registry.
//
// Unticking a row skips that prefab on every future scan. Ticking a row in the skipped list
// includes it on every future scan, even if it's static or matches a skip word. Both choices
// are saved in ProjectSettings/PropRegistrationChoices.asset.
public class PropRegistrationTool : EditorWindow
{
    const string PropLayerName = "Prop";
    const string DoorLayerName = "Door";
    const string PropTypeField = "<propType>k__BackingField";

    static readonly Regex IdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");

    string rootName = "Map";
    string propsFolder = "Assets/Prefabs/Props";
    string excludedKeywords = "Wall, Door, Carpet, Shelve, Paint";
    int defaultMaxHealth = 20;
    float defaultYOffset = 0f;
    bool addNetworkObject = true;
    bool addMeshCollider = true;
    GameObject registryPrefab;

    readonly List<NewProp> newProps = new List<NewProp>();
    readonly List<UnregisteredProp> unregisteredProps = new List<UnregisteredProp>();
    readonly List<SkippedItem> skipped = new List<SkippedItem>();
    readonly Dictionary<string, int> instanceCounts = new Dictionary<string, int>();
    bool hasScanned;
    bool showSkipped = true;
    Vector2 scroll;

    class NewProp
    {
        public string path;
        public string guid;
        public GameObject prefab;
        public int instanceCount;
        public string typeName;
        public int maxHealth;
        public float yOffset;
    }

    class UnregisteredProp
    {
        public string path;
        public string guid;
        public GameObject prefab;
        public Prop.PropType type;
        public int instanceCount;
        public int maxHealth;
        public float yOffset;
    }

    class SkippedItem
    {
        public string name;
        public string reason;
        public GameObject prefab;
        public string guid; // null if it can't be ticked
        public int count = 1;
    }

    static PropRegistrationChoices Choices => PropRegistrationChoices.instance;

    [MenuItem("Tools/Prop Registration Tool")]
    static void Open()
    {
        GetWindow<PropRegistrationTool>("Prop Registration");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        rootName = EditorGUILayout.TextField(new GUIContent("Root Object", "Name of the root object in the open scene to scan under"), rootName);
        propsFolder = EditorGUILayout.TextField(new GUIContent("Props Folder", "Registered prefabs get moved here"), propsFolder);
        registryPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prop Registry Prefab", "Found automatically if left empty"), registryPrefab, typeof(GameObject), false);
        excludedKeywords = EditorGUILayout.TextField(new GUIContent("Skip Names Containing", "Comma separated. Objects whose name contains one of these are skipped"), excludedKeywords);
        defaultMaxHealth = EditorGUILayout.IntField("Default Max Health", defaultMaxHealth);
        defaultYOffset = EditorGUILayout.FloatField("Default Y Offset", defaultYOffset);
        addNetworkObject = EditorGUILayout.Toggle(new GUIContent("Add NetworkObject", "Add a NetworkObject to prefabs that don't have one"), addNetworkObject);
        addMeshCollider = EditorGUILayout.Toggle(new GUIContent("Add MeshCollider If Missing", "Add a convex MeshCollider to props with no collider on their root object"), addMeshCollider);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan", GUILayout.Height(25)))
        {
            Scan();
        }
        if (GUILayout.Button(new GUIContent("Forget Ticked/Unticked", "Clear every remembered tick and untick"), GUILayout.Height(25), GUILayout.Width(160)) &&
            EditorUtility.DisplayDialog("Prop Registration", "Forget every object you've unticked or ticked from the skipped list?", "Forget", "Cancel"))
        {
            Choices.Clear();
            RescanLater();
        }
        EditorGUILayout.EndHorizontal();

        if (!hasScanned)
        {
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"New Props ({newProps.Count})", EditorStyles.boldLabel);
        if (newProps.Count == 0)
        {
            EditorGUILayout.HelpBox("No unregistered props found.", MessageType.Info);
        }
        else
        {
            DrawNewPropsHeader();
            foreach (NewProp prop in newProps)
            {
                DrawNewProp(prop);
            }
        }

        if (unregisteredProps.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Have Prop Script But Missing From Registry ({unregisteredProps.Count})", EditorStyles.boldLabel);
            foreach (UnregisteredProp prop in unregisteredProps)
            {
                DrawUnregisteredProp(prop);
            }
        }

        EditorGUILayout.Space();
        showSkipped = EditorGUILayout.Foldout(showSkipped, $"Skipped Objects ({skipped.Count})", true);
        if (showSkipped)
        {
            EditorGUILayout.LabelField("Tick an object to register it from now on.", EditorStyles.miniLabel);
            foreach (SkippedItem item in skipped)
            {
                DrawSkippedItem(item);
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        List<string> errors = ValidateNames();
        foreach (string error in errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        bool anythingToDo = newProps.Count > 0 || unregisteredProps.Count > 0;
        using (new EditorGUI.DisabledScope(errors.Count > 0 || !anythingToDo))
        {
            if (GUILayout.Button("Register", GUILayout.Height(30)))
            {
                Register();
            }
        }
    }

    void DrawNewPropsHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        EditorGUILayout.LabelField("Prefab", EditorStyles.miniBoldLabel, GUILayout.MinWidth(120));
        EditorGUILayout.LabelField("In Scene", EditorStyles.miniBoldLabel, GUILayout.Width(55));
        EditorGUILayout.LabelField("Prop Type Name", EditorStyles.miniBoldLabel, GUILayout.MinWidth(120));
        EditorGUILayout.LabelField("Health", EditorStyles.miniBoldLabel, GUILayout.Width(50));
        EditorGUILayout.LabelField("Y Offset", EditorStyles.miniBoldLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    void DrawNewProp(NewProp prop)
    {
        EditorGUILayout.BeginHorizontal();
        if (!EditorGUILayout.Toggle(true, GUILayout.Width(16)))
        {
            Choices.Skip(prop.guid);
            RescanLater();
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(prop.prefab, typeof(GameObject), false, GUILayout.MinWidth(120));
        }
        EditorGUILayout.LabelField("x" + prop.instanceCount, GUILayout.Width(55));
        prop.typeName = EditorGUILayout.TextField(prop.typeName, GUILayout.MinWidth(120));
        prop.maxHealth = EditorGUILayout.IntField(prop.maxHealth, GUILayout.Width(50));
        prop.yOffset = EditorGUILayout.FloatField(prop.yOffset, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    void DrawUnregisteredProp(UnregisteredProp prop)
    {
        EditorGUILayout.BeginHorizontal();
        if (!EditorGUILayout.Toggle(true, GUILayout.Width(16)))
        {
            Choices.Skip(prop.guid);
            RescanLater();
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(prop.prefab, typeof(GameObject), false, GUILayout.MinWidth(120));
        }
        EditorGUILayout.LabelField("x" + prop.instanceCount, GUILayout.Width(55));
        EditorGUILayout.LabelField(prop.type.ToString(), GUILayout.MinWidth(120));
        prop.maxHealth = EditorGUILayout.IntField(prop.maxHealth, GUILayout.Width(50));
        prop.yOffset = EditorGUILayout.FloatField(prop.yOffset, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    void DrawSkippedItem(SkippedItem item)
    {
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(item.guid == null))
        {
            if (EditorGUILayout.Toggle(false, GUILayout.Width(16)))
            {
                Choices.Include(item.guid);
                RescanLater();
            }
        }
        if (item.prefab != null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(item.prefab, typeof(GameObject), false, GUILayout.MinWidth(120));
            }
        }
        else
        {
            EditorGUILayout.LabelField(item.name, GUILayout.MinWidth(120));
        }
        EditorGUILayout.LabelField(item.count > 1 ? "x" + item.count : "", GUILayout.Width(55));
        EditorGUILayout.LabelField(item.reason, EditorStyles.miniLabel, GUILayout.MinWidth(120));
        EditorGUILayout.EndHorizontal();
    }

    // Changing the lists in the middle of drawing them breaks the GUI layout, so rescan after this frame
    void RescanLater()
    {
        EditorApplication.delayCall += () =>
        {
            Scan();
            Repaint();
        };
    }

    // ---------- Scanning ----------

    void Scan()
    {
        // Keep whatever was typed into the rows when rescanning
        Dictionary<string, NewProp> previousNew = newProps.ToDictionary(p => p.path);
        Dictionary<string, UnregisteredProp> previousUnregistered = unregisteredProps.ToDictionary(p => p.path);

        newProps.Clear();
        unregisteredProps.Clear();
        skipped.Clear();
        instanceCounts.Clear();
        hasScanned = false;

        if (registryPrefab == null)
        {
            registryPrefab = FindRegistryPrefab();
        }
        if (registryPrefab == null || registryPrefab.GetComponent<PropRegistry>() == null)
        {
            EditorUtility.DisplayDialog("Prop Registration", "Couldn't find a prefab with a PropRegistry component. Assign it in the window.", "OK");
            return;
        }

        List<GameObject> roots = FindRoots();
        if (roots.Count == 0)
        {
            EditorUtility.DisplayDialog("Prop Registration", $"No root object named \"{rootName}\" found in the open scene(s).", "OK");
            return;
        }

        Dictionary<string, NewProp> newByPath = new Dictionary<string, NewProp>();
        HashSet<string> propPrefabPaths = new HashSet<string>();
        foreach (GameObject root in roots)
        {
            foreach (Transform child in root.transform)
            {
                Classify(child.gameObject, newByPath, propPrefabPaths);
            }
        }

        FindUnregisteredProps(propPrefabPaths, newByPath, previousUnregistered);

        // Name new props after their prefab, keeping names unique against the enum and each other.
        // Names from the last scan go first so a generated name can't take one of them.
        HashSet<string> takenNames = new HashSet<string>(Enum.GetNames(typeof(Prop.PropType)), StringComparer.OrdinalIgnoreCase);
        foreach (NewProp prop in newByPath.Values)
        {
            if (previousNew.TryGetValue(prop.path, out NewProp previous))
            {
                prop.typeName = previous.typeName;
                prop.maxHealth = previous.maxHealth;
                prop.yOffset = previous.yOffset;
                takenNames.Add(prop.typeName);
            }
        }
        foreach (NewProp prop in newByPath.Values.OrderBy(p => p.prefab.name))
        {
            prop.instanceCount = InstanceCount(prop.path);
            if (prop.typeName == null)
            {
                prop.typeName = MakeUnique(DeriveTypeName(prop.prefab.name), takenNames);
                takenNames.Add(prop.typeName);
            }
            newProps.Add(prop);
        }

        // A prefab that's going to be registered doesn't need to show up as skipped too
        HashSet<string> handled = new HashSet<string>(newProps.Select(p => p.guid).Concat(unregisteredProps.Select(p => p.guid)));
        List<SkippedItem> sorted = skipped
            .Where(item => item.guid == null || !handled.Contains(item.guid))
            .OrderBy(item => item.guid == null)
            .ThenBy(item => item.reason)
            .ThenBy(item => item.name)
            .ToList();
        skipped.Clear();
        skipped.AddRange(sorted);

        hasScanned = true;
    }

    List<GameObject> FindRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }
            roots.AddRange(scene.GetRootGameObjects().Where(go => go.name == rootName));
        }
        return roots;
    }

    void Classify(GameObject go, Dictionary<string, NewProp> newByPath, HashSet<string> propPrefabPaths)
    {
        string label = GetHierarchyPath(go);
        bool isPrefabRoot = PrefabUtility.IsAnyPrefabInstanceRoot(go);
        string assetPath = isPrefabRoot ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) : null;

        if (isPrefabRoot && GetAssetType(assetPath) == PrefabAssetType.Model)
        {
            AddSkipped(label, "model/FBX dropped in directly, make a prefab of it first", assetPath, false);
            return;
        }

        // Rooms and plain grouping objects get looked inside instead. Only single prefabs can be ticked.
        bool isContainer = !isPrefabRoot || ContainsPrefabInstances(go);
        bool canTick = !isContainer && assetPath.StartsWith("Assets/");
        bool included = canTick && Choices.IsIncluded(AssetDatabase.AssetPathToGUID(assetPath));

        string skipWordReason = GetSkipWordReason(go);
        if (skipWordReason != null && !included)
        {
            AddSkipped(label, skipWordReason, assetPath, canTick);
            return;
        }

        if (isContainer)
        {
            if (!isPrefabRoot && go.transform.childCount == 0 && go.GetComponent<Renderer>() != null)
            {
                AddSkipped(label, "not a prefab", null, false);
            }
            foreach (Transform child in go.transform)
            {
                Classify(child.gameObject, newByPath, propPrefabPaths);
            }
            return;
        }

        instanceCounts[assetPath] = InstanceCount(assetPath) + 1;

        // Prefabs that already have a Prop script are sorted out in FindUnregisteredProps
        if (go.GetComponent<Prop>() != null)
        {
            propPrefabPaths.Add(assetPath);
            return;
        }

        if (!canTick)
        {
            AddSkipped(label, "prefab is inside a package, can't edit it", assetPath, false);
            return;
        }

        if (Choices.IsSkipped(AssetDatabase.AssetPathToGUID(assetPath)))
        {
            AddSkipped(label, "unticked by you", assetPath, true);
            return;
        }

        if (GameObjectUtility.GetStaticEditorFlags(go) != 0 && !included)
        {
            AddSkipped(label, "static", assetPath, true);
            return;
        }

        if (!newByPath.ContainsKey(assetPath))
        {
            newByPath.Add(assetPath, CreateNewProp(assetPath));
        }
    }

    NewProp CreateNewProp(string assetPath)
    {
        return new NewProp
        {
            path = assetPath,
            guid = AssetDatabase.AssetPathToGUID(assetPath),
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath),
            maxHealth = defaultMaxHealth,
            yOffset = defaultYOffset,
        };
    }

    int InstanceCount(string assetPath)
    {
        return instanceCounts.TryGetValue(assetPath, out int count) ? count : 0;
    }

    // Instances of the same prefab skipped for the same reason are grouped into one row
    void AddSkipped(string label, string reason, string assetPath, bool canTick)
    {
        GameObject prefab = assetPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) : null;
        string name = prefab != null ? prefab.name : label;

        SkippedItem existing = skipped.FirstOrDefault(item => item.reason == reason && item.name == name && item.prefab == prefab);
        if (existing != null)
        {
            existing.count++;
            return;
        }

        skipped.Add(new SkippedItem
        {
            name = name,
            reason = reason,
            prefab = prefab,
            guid = canTick ? AssetDatabase.AssetPathToGUID(assetPath) : null,
        });
    }

    // Returns why the object counts as a wall/door/skip word, or null if it doesn't
    string GetSkipWordReason(GameObject go)
    {
        int doorLayer = LayerMask.NameToLayer(DoorLayerName);
        if (doorLayer != -1 && go.layer == doorLayer)
        {
            return "on the Door layer";
        }

        string assetName = "";
        if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
        {
            assetName = Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go));
        }

        foreach (string keyword in excludedKeywords.Split(','))
        {
            string trimmed = keyword.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            if (go.name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"name contains \"{trimmed}\"";
            }
        }
        return null;
    }

    // True if any child (at any depth) is itself an instance of a regular prefab, e.g. a room full of props
    static bool ContainsPrefabInstances(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t == go.transform || !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
            {
                continue;
            }
            if (GetAssetType(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)) != PrefabAssetType.Model)
            {
                return true;
            }
        }
        return false;
    }

    static PrefabAssetType GetAssetType(string assetPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return asset == null ? PrefabAssetType.NotAPrefab : PrefabUtility.GetPrefabAssetType(asset);
    }

    void FindUnregisteredProps(HashSet<string> propPrefabPaths, Dictionary<string, NewProp> newByPath, Dictionary<string, UnregisteredProp> previousUnregistered)
    {
        if (AssetDatabase.IsValidFolder(propsFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { propsFolder }))
            {
                propPrefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        HashSet<int> registeredTypes = new HashSet<int>(GetRegisteredTypes());
        foreach (string path in propPrefabPaths.OrderBy(p => p))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Prop prop = prefab != null ? prefab.GetComponent<Prop>() : null;
            if (prop == null)
            {
                continue;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);

            // Ticking one of these treats it like a new prop, so it gets a name and a new enum value
            if (prop.propType == Prop.PropType.None)
            {
                if (Choices.IsIncluded(guid))
                {
                    if (!newByPath.ContainsKey(path))
                    {
                        newByPath.Add(path, CreateNewProp(path));
                    }
                }
                else
                {
                    AddSkipped(path, "has Prop script but its type is None", path, true);
                }
                continue;
            }
            if (registeredTypes.Contains((int)prop.propType))
            {
                AddSkipped(path, "already registered", path, false);
                continue;
            }
            if (Choices.IsSkipped(guid))
            {
                AddSkipped(path, "unticked by you", path, true);
                continue;
            }
            if (unregisteredProps.Any(p => p.type == prop.propType))
            {
                AddSkipped(path, $"another prefab already uses type {prop.propType}", path, false);
                continue;
            }

            UnregisteredProp entry = new UnregisteredProp
            {
                path = path,
                guid = guid,
                prefab = prefab,
                type = prop.propType,
                instanceCount = InstanceCount(path),
                maxHealth = defaultMaxHealth,
                yOffset = defaultYOffset,
            };
            if (previousUnregistered.TryGetValue(path, out UnregisteredProp previous))
            {
                entry.maxHealth = previous.maxHealth;
                entry.yOffset = previous.yOffset;
            }
            unregisteredProps.Add(entry);
        }
    }

    List<int> GetRegisteredTypes()
    {
        SerializedObject so = new SerializedObject(registryPrefab.GetComponent<PropRegistry>());
        SerializedProperty entries = so.FindProperty("entries");
        List<int> types = new List<int>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            types.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("type").intValue);
        }
        return types;
    }

    static GameObject FindRegistryPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("PropRegistry t:Prefab").Concat(AssetDatabase.FindAssets("t:Prefab")))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<PropRegistry>() != null)
            {
                return prefab;
            }
        }
        return null;
    }

    // ---------- Naming ----------

    // "Prop_Beauty_01" -> "Beauty_01", "Prop_Towel (Old)" -> "Towel_Old"
    static string DeriveTypeName(string prefabName)
    {
        string name = Regex.Replace(prefabName, "^Prop_", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, "[^A-Za-z0-9_]", "_");
        name = Regex.Replace(name, "_+", "_").Trim('_');
        if (name.Length == 0)
        {
            name = "Prop";
        }
        if (char.IsDigit(name[0]))
        {
            name = "Prop_" + name;
        }
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    // If the name is taken, bump its number: "Towel" -> "Towel_02", "Beauty_01" -> "Beauty_02"
    static string MakeUnique(string name, HashSet<string> taken)
    {
        if (!taken.Contains(name))
        {
            return name;
        }

        Match match = Regex.Match(name, @"^(.*?)_?(\d+)$");
        string baseName = match.Success ? match.Groups[1].Value : name;
        int number = match.Success ? int.Parse(match.Groups[2].Value) + 1 : 2;
        int width = match.Success ? Math.Max(2, match.Groups[2].Value.Length) : 2;

        string candidate;
        do
        {
            candidate = $"{baseName}_{number.ToString().PadLeft(width, '0')}";
            number++;
        }
        while (taken.Contains(candidate));
        return candidate;
    }

    List<string> ValidateNames()
    {
        List<string> errors = new List<string>();
        HashSet<string> existing = new HashSet<string>(Enum.GetNames(typeof(Prop.PropType)), StringComparer.OrdinalIgnoreCase);
        HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (NewProp prop in newProps)
        {
            if (string.IsNullOrEmpty(prop.typeName) || !IdentifierRegex.IsMatch(prop.typeName))
            {
                errors.Add($"\"{prop.typeName}\" ({prop.prefab.name}) isn't a valid name. Use letters, digits and _ and don't start with a digit.");
            }
            else if (existing.Contains(prop.typeName))
            {
                errors.Add($"\"{prop.typeName}\" ({prop.prefab.name}) already exists in Prop.PropType.");
            }
            else if (!used.Add(prop.typeName))
            {
                errors.Add($"\"{prop.typeName}\" is used more than once.");
            }
        }
        return errors;
    }

    // ---------- Registering ----------

    void Register()
    {
        List<NewProp> toAdd = newProps.ToList();
        List<UnregisteredProp> toRegister = unregisteredProps.ToList();

        int propLayer = LayerMask.NameToLayer(PropLayerName);
        if (propLayer == -1)
        {
            EditorUtility.DisplayDialog("Prop Registration", $"There's no layer called \"{PropLayerName}\".", "OK");
            return;
        }

        MonoScript propScript = FindPropScript();
        if (propScript == null)
        {
            EditorUtility.DisplayDialog("Prop Registration", "Couldn't find Prop.cs.", "OK");
            return;
        }

        string summary = $"This will register {toAdd.Count} new prop(s) and add {toRegister.Count} existing prop(s) to the registry.\n\n" +
                         "It edits prefab assets, moves them to " + propsFolder + " and edits Prop.cs. " +
                         "Commit your project first so you can undo it if needed.";
        if (!EditorUtility.DisplayDialog("Prop Registration", summary, "Register", "Cancel"))
        {
            return;
        }

        EnsureFolder(propsFolder);

        int nextValue = Enum.GetValues(typeof(Prop.PropType)).Cast<int>().Max() + 1;
        List<(int type, string path, int maxHealth, float yOffset)> registryEntries = new List<(int, string, int, float)>();
        List<string> newEnumNames = new List<string>();
        StringBuilder log = new StringBuilder("Prop Registration Tool:\n");

        try
        {
            for (int i = 0; i < toAdd.Count; i++)
            {
                NewProp prop = toAdd[i];
                EditorUtility.DisplayProgressBar("Registering Props", prop.prefab.name, (float)i / Math.Max(1, toAdd.Count));
                try
                {
                    SetUpPrefab(prop.path, nextValue, propLayer, log);
                    string finalPath = MoveToPropsFolder(prop.path, log);
                    registryEntries.Add((nextValue, finalPath, prop.maxHealth, prop.yOffset));
                    newEnumNames.Add(prop.typeName);
                    log.AppendLine($"  + {prop.typeName} = {nextValue}  ({finalPath})");
                    nextValue++;
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ! Failed on {prop.path}: {e.Message}");
                    Debug.LogException(e);
                }
            }

            foreach (UnregisteredProp prop in toRegister)
            {
                if (addMeshCollider)
                {
                    AddMeshColliderIfMissing(prop.path, log);
                }
                string finalPath = MoveToPropsFolder(prop.path, log);
                registryEntries.Add(((int)prop.type, finalPath, prop.maxHealth, prop.yOffset));
                log.AppendLine($"  + registry entry for existing {prop.type}  ({finalPath})");
            }

            EditorUtility.DisplayProgressBar("Registering Props", "Prop Registry", 1f);
            AddRegistryEntries(registryEntries);
            AssetDatabase.SaveAssets();
            WarnAboutLayerOverrides(registryEntries.Select(e => e.path), propLayer, log);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Done last because it triggers a script recompile
        if (newEnumNames.Count > 0)
        {
            AddEnumValues(propScript, newEnumNames);
        }

        Debug.Log(log.ToString());
        newProps.Clear();
        unregisteredProps.Clear();
        hasScanned = false;
    }

    void SetUpPrefab(string path, int propTypeValue, int propLayer, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (addNetworkObject && root.GetComponent<NetworkObject>() == null)
            {
                root.AddComponent<NetworkObject>();
            }

            Prop prop = root.GetComponent<Prop>();
            if (prop == null)
            {
                prop = root.AddComponent<Prop>();
            }

            // Set as a raw int because the enum value is only added to Prop.cs after this
            SerializedObject so = new SerializedObject(prop);
            so.FindProperty(PropTypeField).intValue = propTypeValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = propLayer;
            }

            if (addMeshCollider)
            {
                AddMeshColliderIfMissing(root, path, log);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // For prefabs that otherwise aren't edited: only opens and saves the prefab if a collider is needed
    static void AddMeshColliderIfMissing(string path, StringBuilder log)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || prefab.GetComponent<Collider>() != null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (AddMeshColliderIfMissing(root, path, log))
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The game reads the collider and the Prop from the same (root) object, so the collider goes there.
    // Returns true if a collider was added.
    static bool AddMeshColliderIfMissing(GameObject root, string path, StringBuilder log)
    {
        if (root.GetComponent<Collider>() != null)
        {
            return false;
        }

        MeshFilter meshFilter = root.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            log.AppendLine($"  ! {path} has no collider and no mesh on its root object, add a collider by hand.");
            return false;
        }

        MeshCollider meshCollider = root.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = true;
        log.AppendLine($"  + added convex MeshCollider to {path}");
        return true;
    }

    // Moving keeps the asset's GUID, so everything that references the prefab keeps working
    string MoveToPropsFolder(string path, StringBuilder log)
    {
        if (Path.GetDirectoryName(path).Replace('\\', '/') == propsFolder.TrimEnd('/'))
        {
            return path;
        }

        string newPath = AssetDatabase.GenerateUniqueAssetPath(propsFolder.TrimEnd('/') + "/" + Path.GetFileName(path));
        string error = AssetDatabase.MoveAsset(path, newPath);
        if (!string.IsNullOrEmpty(error))
        {
            log.AppendLine($"  ! Couldn't move {path}: {error}");
            return path;
        }
        return newPath;
    }

    void AddRegistryEntries(List<(int type, string path, int maxHealth, float yOffset)> registryEntries)
    {
        if (registryEntries.Count == 0)
        {
            return;
        }

        string registryPath = AssetDatabase.GetAssetPath(registryPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(registryPath);
        try
        {
            SerializedObject so = new SerializedObject(root.GetComponent<PropRegistry>());
            SerializedProperty entries = so.FindProperty("entries");
            foreach ((int type, string path, int maxHealth, float yOffset) in registryEntries)
            {
                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("type").intValue = type;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                entry.FindPropertyRelative("maxHealth").intValue = maxHealth;
                entry.FindPropertyRelative("yOffset").floatValue = yOffset;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, registryPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A layer set on an instance in the scene or room prefab wins over the prefab's own layer
    void WarnAboutLayerOverrides(IEnumerable<string> paths, int propLayer, StringBuilder log)
    {
        HashSet<string> pathSet = new HashSet<string>(paths);
        foreach (GameObject root in FindRoots())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (go.layer != propLayer && PrefabUtility.IsPartOfPrefabInstance(go) &&
                    pathSet.Contains(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)))
                {
                    log.AppendLine($"  ! {GetHierarchyPath(go)} still isn't on the Prop layer because of a layer override. Fix it by hand.");
                }
            }
        }
    }

    static void AddEnumValues(MonoScript propScript, List<string> names)
    {
        string path = AssetDatabase.GetAssetPath(propScript);
        string text = File.ReadAllText(path);
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";

        Match enumStart = Regex.Match(text, @"enum\s+PropType\s*\{");
        if (!enumStart.Success)
        {
            Debug.LogError("Prop Registration Tool: couldn't find 'enum PropType' in " + path + ". Add these by hand: " + string.Join(", ", names));
            return;
        }

        int closeIndex = text.IndexOf('}', enumStart.Index + enumStart.Length);
        string body = text.Substring(enumStart.Index + enumStart.Length, closeIndex - enumStart.Index - enumStart.Length);

        // Copy the indentation of the last value already in the enum
        string trimmedBody = body.TrimEnd();
        int lastLineStart = trimmedBody.LastIndexOf('\n') + 1;
        string lastLine = trimmedBody.Substring(lastLineStart);
        string indent = lastLine.Substring(0, lastLine.Length - lastLine.TrimStart().Length);

        StringBuilder newBody = new StringBuilder(trimmedBody);
        if (trimmedBody.Length > 0 && !trimmedBody.EndsWith(","))
        {
            newBody.Append(',');
        }
        foreach (string name in names)
        {
            newBody.Append(newline).Append(indent).Append(name).Append(',');
        }

        // Keep whatever came between the last value and the closing brace (newline + indentation)
        newBody.Append(body.Substring(trimmedBody.Length));

        string newText = text.Substring(0, enumStart.Index + enumStart.Length) + newBody + text.Substring(closeIndex);
        File.WriteAllText(path, newText);
        AssetDatabase.ImportAsset(path);
    }

    static MonoScript FindPropScript()
    {
        foreach (string guid in AssetDatabase.FindAssets("Prop t:MonoScript"))
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (script != null && script.GetClass() == typeof(Prop))
            {
                return script;
            }
        }
        return null;
    }

    static void EnsureFolder(string folder)
    {
        folder = folder.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.transform.parent;
        }
        return path;
    }
}
