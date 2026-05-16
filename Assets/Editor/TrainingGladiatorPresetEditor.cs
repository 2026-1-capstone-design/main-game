using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrainingGladiatorPreset))]
public sealed class TrainingGladiatorPresetEditor : Editor
{
    private const string CharacterPreviewPrefabPath = "Assets/Prefabs/Battle/character_model_training.prefab";

    private static readonly SkinPartRoot[] SkinPartRoots =
    {
        new SkinPartRoot(SkinPart.FullHead, "Full Head", "ARMOR PARTS/HEADS", allowNone: true),
        new SkinPartRoot(SkinPart.Nose, "Nose", "FACE DETAILS PARTS/NOSES", allowNone: true),
        new SkinPartRoot(SkinPart.Hair, "Hair", "FACE DETAILS PARTS/HAIRS", allowNone: true),
        new SkinPartRoot(SkinPart.Face, "Face Hair", "FACE DETAILS PARTS/FACE HAIRS", allowNone: true),
        new SkinPartRoot(SkinPart.Eyes, "Eyes", "FACE DETAILS PARTS/EYES", allowNone: true),
        new SkinPartRoot(SkinPart.Eyebrows, "Eyebrows", "FACE DETAILS PARTS/EYEBROWS", allowNone: true),
        new SkinPartRoot(SkinPart.Ears, "Ears", "FACE DETAILS PARTS/EARS", allowNone: true),
        new SkinPartRoot(SkinPart.Chest, "Chest", "ARMOR PARTS/CHESTS", allowNone: false),
        new SkinPartRoot(SkinPart.Arms, "Arms", "ARMOR PARTS/ARMS", allowNone: false),
        new SkinPartRoot(SkinPart.Belt, "Belt", "ARMOR PARTS/BELTS", allowNone: false),
        new SkinPartRoot(SkinPart.Legs, "Legs", "ARMOR PARTS/LEGS", allowNone: false),
        new SkinPartRoot(SkinPart.Feet, "Feet", "ARMOR PARTS/FEET", allowNone: false),
    };

    private SerializedProperty _displayNamePrefix;
    private SerializedProperty _level;
    private SerializedProperty _gladiatorClass;
    private SerializedProperty _weapon;
    private SerializedProperty _overrideWeaponSettings;
    private SerializedProperty _isRanged;
    private SerializedProperty _useProjectile;
    private SerializedProperty _weaponSkillId;
    private SerializedProperty _maxHealth;
    private SerializedProperty _attack;
    private SerializedProperty _attackSpeed;
    private SerializedProperty _moveSpeed;
    private SerializedProperty _attackRange;
    private SerializedProperty _customizeIndicates;

    private GameObject _characterPrefab;
    private readonly Dictionary<SkinPart, string[]> _optionNamesByPart = new Dictionary<SkinPart, string[]>();

    private PreviewRenderUtility _previewUtility;
    private GameObject _previewInstance;
    private readonly Dictionary<SkinPart, Transform> _previewRootsByPart = new Dictionary<SkinPart, Transform>();
    private readonly List<Material> _previewMaterials = new List<Material>();
    private bool _previewNeedsRefresh = true;

    private void OnEnable()
    {
        _displayNamePrefix = serializedObject.FindProperty("displayNamePrefix");
        _level = serializedObject.FindProperty("level");
        _gladiatorClass = serializedObject.FindProperty("gladiatorClass");
        _weapon = serializedObject.FindProperty("weapon");
        _overrideWeaponSettings = serializedObject.FindProperty("overrideWeaponSettings");
        _isRanged = serializedObject.FindProperty("isRanged");
        _useProjectile = serializedObject.FindProperty("useProjectile");
        _weaponSkillId = serializedObject.FindProperty("weaponSkillId");
        _maxHealth = serializedObject.FindProperty("maxHealth");
        _attack = serializedObject.FindProperty("attack");
        _attackSpeed = serializedObject.FindProperty("attackSpeed");
        _moveSpeed = serializedObject.FindProperty("moveSpeed");
        _attackRange = serializedObject.FindProperty("attackRange");
        _customizeIndicates = serializedObject.FindProperty("customizeIndicates");

        _characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPreviewPrefabPath);
        RebuildOptionCache();
    }

    private void OnDisable()
    {
        DestroyPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureCustomizeArray();

        EditorGUILayout.PropertyField(_displayNamePrefix);
        EditorGUILayout.PropertyField(_level);
        EditorGUILayout.PropertyField(_gladiatorClass);

        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(_weapon);
        EditorGUILayout.PropertyField(_overrideWeaponSettings);
        using (new EditorGUI.DisabledScope(!_overrideWeaponSettings.boolValue))
        {
            EditorGUILayout.PropertyField(_isRanged);
            EditorGUILayout.PropertyField(_useProjectile);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_weaponSkillId);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Final Stats", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_maxHealth);
        EditorGUILayout.PropertyField(_attack);
        EditorGUILayout.PropertyField(_attackSpeed);
        EditorGUILayout.PropertyField(_moveSpeed);
        EditorGUILayout.PropertyField(_attackRange);

        EditorGUILayout.Space(8f);
        DrawSkinDropdowns();

        EditorGUILayout.Space(8f);
        DrawValidationBox();

        if (serializedObject.ApplyModifiedProperties())
        {
            _previewNeedsRefresh = true;
        }
    }

    public override bool HasPreviewGUI() => _characterPrefab != null;

    public override void OnPreviewGUI(Rect rect, GUIStyle background)
    {
        if (_characterPrefab == null)
        {
            EditorGUI.LabelField(rect, "character_model_training.prefab not found.");
            return;
        }

        EnsurePreview();
        if (_previewUtility == null || _previewInstance == null)
        {
            EditorGUI.LabelField(rect, "Preview unavailable.");
            return;
        }

        if (_previewNeedsRefresh)
        {
            serializedObject.Update();
            ApplySkinToPreview();
            _previewNeedsRefresh = false;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        Bounds bounds = CalculatePreviewBounds(_previewInstance);
        Vector3 center = bounds.center + Vector3.up * (bounds.extents.y * 0.08f);
        float radius = Mathf.Max(1f, bounds.extents.magnitude);
        Camera camera = _previewUtility.camera;
        camera.transform.position = center + new Vector3(radius * 3f, radius * 0.0f, radius * 2.9f);
        camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, Vector3.up);
        camera.orthographic = true;
        camera.orthographicSize = 1.0f;

        _previewUtility.lights[0].intensity = 1.1f;
        _previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
        _previewUtility.lights[1].intensity = 0.6f;

        _previewUtility.BeginPreview(rect, background);
        _previewUtility.Render();
        Texture previewTexture = _previewUtility.EndPreview();
        GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);
    }

    private void DrawSkinDropdowns()
    {
        EditorGUILayout.LabelField("Skin Parts", EditorStyles.boldLabel);

        if (_characterPrefab == null)
        {
            EditorGUILayout.HelpBox(
                $"Cannot find preview source prefab at {CharacterPreviewPrefabPath}.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.HelpBox(
            "Options are read from character_model_training.prefab. Full Head is treated as a regular part and can be combined with face-detail parts.",
            MessageType.Info
        );

        if (GUILayout.Button("Reload Part Options"))
        {
            _characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPreviewPrefabPath);
            RebuildOptionCache();
            _previewNeedsRefresh = true;
        }

        EditorGUI.BeginChangeCheck();
        foreach (SkinPartRoot partRoot in SkinPartRoots)
        {
            DrawSkinDropdown(partRoot);
        }

        if (EditorGUI.EndChangeCheck())
        {
            _previewNeedsRefresh = true;
        }
    }

    private void DrawSkinDropdown(SkinPartRoot partRoot)
    {
        string[] options = GetOptions(partRoot);
        SerializedProperty element = _customizeIndicates.GetArrayElementAtIndex((int)partRoot.Part);
        int currentValue = element.intValue;
        int popupIndex = partRoot.AllowNone ? currentValue + 1 : currentValue;
        popupIndex = Mathf.Clamp(popupIndex, 0, Mathf.Max(0, options.Length - 1));

        int nextPopupIndex = EditorGUILayout.Popup(partRoot.Label, popupIndex, options);
        int nextValue = partRoot.AllowNone ? nextPopupIndex - 1 : nextPopupIndex;
        element.intValue = nextValue;
    }

    private void DrawValidationBox()
    {
        TrainingGladiatorPreset preset = (TrainingGladiatorPreset)target;
        string error = preset.GetValidationError();
        if (error == null)
        {
            EditorGUILayout.HelpBox("Preset is valid.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    private void EnsureCustomizeArray()
    {
        int expectedSize = (int)SkinPart.TotalCount;
        if (_customizeIndicates.arraySize != expectedSize)
        {
            _customizeIndicates.arraySize = expectedSize;
        }
    }

    private string[] GetOptions(SkinPartRoot partRoot)
    {
        if (!_optionNamesByPart.TryGetValue(partRoot.Part, out string[] options) || options == null || options.Length == 0)
        {
            return partRoot.AllowNone ? new[] { "None" } : new[] { "Missing Root" };
        }

        return options;
    }

    private void RebuildOptionCache()
    {
        _optionNamesByPart.Clear();
        if (_characterPrefab == null)
            return;

        Transform root = _characterPrefab.transform;
        foreach (SkinPartRoot partRoot in SkinPartRoots)
        {
            Transform partParent = root.Find(partRoot.Path);
            if (partParent == null)
            {
                _optionNamesByPart[partRoot.Part] = partRoot.AllowNone ? new[] { "None" } : new[] { "Missing Root" };
                continue;
            }

            int offset = partRoot.AllowNone ? 1 : 0;
            string[] options = new string[partParent.childCount + offset];
            if (partRoot.AllowNone)
            {
                options[0] = "None";
            }

            for (int i = 0; i < partParent.childCount; i++)
            {
                options[i + offset] = $"{i}: {partParent.GetChild(i).name}";
            }

            _optionNamesByPart[partRoot.Part] = options;
        }
    }

    private void EnsurePreview()
    {
        if (_previewUtility != null && _previewInstance != null)
            return;

        DestroyPreview();

        _previewUtility = new PreviewRenderUtility();
        _previewUtility.cameraFieldOfView = 30f;
        _previewInstance = Instantiate(_characterPrefab);
        _previewInstance.hideFlags = HideFlags.HideAndDontSave;
        _previewInstance.transform.position = Vector3.zero;
        _previewInstance.transform.rotation = Quaternion.identity;

        foreach (Canvas canvas in _previewInstance.GetComponentsInChildren<Canvas>(true))
        {
            canvas.gameObject.SetActive(false);
        }

        ReplacePreviewMaterials();
        _previewUtility.AddSingleGO(_previewInstance);
        RebuildPreviewRootCache();
        ApplySkinToPreview();
    }

    private void DestroyPreview()
    {
        if (_previewInstance != null)
        {
            DestroyImmediate(_previewInstance);
            _previewInstance = null;
        }

        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }

        for (int i = 0; i < _previewMaterials.Count; i++)
        {
            if (_previewMaterials[i] != null)
            {
                DestroyImmediate(_previewMaterials[i]);
            }
        }

        _previewMaterials.Clear();
        _previewRootsByPart.Clear();
    }

    private void ReplacePreviewMaterials()
    {
        if (_previewInstance == null)
            return;

        Shader previewShader = Shader.Find("Unlit/Texture");
        if (previewShader == null)
        {
            previewShader = Shader.Find("Unlit/Color");
        }

        if (previewShader == null)
        {
            previewShader = Shader.Find("Standard");
        }

        if (previewShader == null)
            return;

        Renderer[] renderers = _previewInstance.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] previewMaterials = new Material[sourceMaterials.Length];

            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = sourceMaterials[materialIndex];
                Material previewMaterial = CreatePreviewMaterial(sourceMaterial, previewShader);
                previewMaterials[materialIndex] = previewMaterial;
                _previewMaterials.Add(previewMaterial);
            }

            renderer.sharedMaterials = previewMaterials;
        }
    }

    private static Material CreatePreviewMaterial(Material sourceMaterial, Shader previewShader)
    {
        Material previewMaterial = new Material(previewShader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = sourceMaterial != null ? $"{sourceMaterial.name} Preview" : "Training Preview Material",
        };

        Texture mainTexture = TryGetTexture(sourceMaterial, "_BaseMap") ?? TryGetTexture(sourceMaterial, "_MainTex");
        Color baseColor = TryGetColor(sourceMaterial, "_BaseColor", TryGetColor(sourceMaterial, "_Color", Color.white));

        if (mainTexture != null && previewMaterial.HasProperty("_MainTex"))
        {
            previewMaterial.SetTexture("_MainTex", mainTexture);
        }

        if (previewMaterial.HasProperty("_Color"))
        {
            previewMaterial.SetColor("_Color", baseColor);
        }

        return previewMaterial;
    }

    private static Texture TryGetTexture(Material material, string propertyName)
    {
        if (material == null || !material.HasProperty(propertyName))
            return null;

        return material.GetTexture(propertyName);
    }

    private static Color TryGetColor(Material material, string propertyName, Color fallback)
    {
        if (material == null || !material.HasProperty(propertyName))
            return fallback;

        return material.GetColor(propertyName);
    }

    private void RebuildPreviewRootCache()
    {
        _previewRootsByPart.Clear();
        if (_previewInstance == null)
            return;

        Transform root = _previewInstance.transform;
        foreach (SkinPartRoot partRoot in SkinPartRoots)
        {
            _previewRootsByPart[partRoot.Part] = root.Find(partRoot.Path);
        }
    }

    private void ApplySkinToPreview()
    {
        if (_previewInstance == null)
            return;

        EnsureCustomizeArray();
        foreach (SkinPartRoot partRoot in SkinPartRoots)
        {
            int index = _customizeIndicates.GetArrayElementAtIndex((int)partRoot.Part).intValue;
            if (_previewRootsByPart.TryGetValue(partRoot.Part, out Transform partParent))
            {
                ActivateSpecificSkinPart(partParent, index);
            }
        }
    }

    private static void ActivateSpecificSkinPart(Transform parentRoot, int targetIndex)
    {
        if (parentRoot == null)
            return;

        for (int i = 0; i < parentRoot.childCount; i++)
        {
            parentRoot.GetChild(i).gameObject.SetActive(i == targetIndex);
        }
    }

    private static Bounds CalculatePreviewBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private readonly struct SkinPartRoot
    {
        public readonly SkinPart Part;
        public readonly string Label;
        public readonly string Path;
        public readonly bool AllowNone;

        public SkinPartRoot(SkinPart part, string label, string path, bool allowNone)
        {
            Part = part;
            Label = label;
            Path = path;
            AllowNone = allowNone;
        }
    }
}
