using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders a crisp, configurable outline contour around a 3D object and its children.
/// Designed for Unity Universal Render Pipeline (URP) and compatible with Built-in pipeline.
/// Supports both static MeshRenderers and animated SkinnedMeshRenderers with multiple submeshes.
/// </summary>
[DisallowMultipleComponent]
public class InteractableOutline : MonoBehaviour
{
    public enum OutlineMode
    {
        VisibleOnly = 4,   // LessEqual: Outline hidden behind occluding geometry/walls
        AlwaysVisible = 8  // Always: Outline visible through walls/obstacles
    }

    public enum ExtrusionMode
    {
        ScreenSpace = 0, // Uniform pixel thickness regardless of camera distance
        WorldSpace = 1   // Physical thickness in 3D world space
    }

    [Header("Outline Appearance")]
    [Tooltip("Color of the outline contour.")]
    [SerializeField] private Color _outlineColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Thickness/width of the outline.")]
    [Range(0.1f, 10f)]
    [SerializeField] private float _outlineWidth = 2.5f;

    [Tooltip("VisibleOnly: hidden behind walls. AlwaysVisible: visible through walls.")]
    [SerializeField] private OutlineMode _outlineMode = OutlineMode.VisibleOnly;

    [Tooltip("ScreenSpace: constant pixel thickness. WorldSpace: scales with distance.")]
    [SerializeField] private ExtrusionMode _extrusionMode = ExtrusionMode.ScreenSpace;

    [Header("Target Renderers")]
    [Tooltip("Include all child MeshRenderers and SkinnedMeshRenderers automatically.")]
    [SerializeField] private bool _includeChildren = true;

    [Tooltip("Optional: manually specify which renderers to outline. If empty, auto-detects.")]
    [SerializeField] private Renderer[] _customRenderers;

    [Header("Shader Reference (Optional)")]
    [Tooltip("Custom/InteractableOutline shader. Loaded automatically from Resources if null.")]
    [SerializeField] private Shader _customOutlineShader;

    // Internal state
    private Material _outlineMaterial;
    private readonly List<Renderer> _outlineRenderers = new List<Renderer>();
    private readonly List<GameObject> _helperObjects = new List<GameObject>();
    private bool _isOutlineActive = false;
    private bool _isInitialized = false;

    // Shader property IDs (cached for performance)
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
    private static readonly int ExtrusionModeId = Shader.PropertyToID("_ExtrusionMode");

    #region Properties

    public Color OutlineColor
    {
        get => _outlineColor;
        set
        {
            _outlineColor = value;
            UpdateMaterialProperties();
        }
    }

    public float OutlineWidth
    {
        get => _outlineWidth;
        set
        {
            _outlineWidth = Mathf.Max(0f, value);
            UpdateMaterialProperties();
        }
    }

    public OutlineMode Mode
    {
        get => _outlineMode;
        set
        {
            _outlineMode = value;
            UpdateMaterialProperties();
        }
    }

    public ExtrusionMode Extrusion
    {
        get => _extrusionMode;
        set
        {
            _extrusionMode = value;
            UpdateMaterialProperties();
        }
    }

    public bool IsOutlineActive => _isOutlineActive;

    #endregion

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
        ApplyActiveState();
    }

    private void OnDisable()
    {
        // Hide outline when this component or object is disabled
        SetRenderersEnabled(false);
    }

    private void OnDestroy()
    {
        CleanupHelpers();

        if (_outlineMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_outlineMaterial);
            }
            else
            {
                DestroyImmediate(_outlineMaterial);
            }
            _outlineMaterial = null;
        }
    }

    private void OnValidate()
    {
        _outlineWidth = Mathf.Max(0f, _outlineWidth);
        UpdateMaterialProperties();
    }

    /// <summary>
    /// Initializes materials and spawns outline helper renderers.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        CreateOutlineMaterial();
        BuildHelperRenderers();
        UpdateMaterialProperties();
        SetRenderersEnabled(_isOutlineActive);

        _isInitialized = true;
    }

    private void CreateOutlineMaterial()
    {
        if (_outlineMaterial != null) return;

        Shader shader = _customOutlineShader;
        if (shader == null)
        {
            shader = Shader.Find("Custom/InteractableOutline");
        }
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Shaders/InteractableOutline");
        }

        if (shader == null)
        {
            Debug.LogError($"[InteractableOutline] Could not find shader 'Custom/InteractableOutline' on '{gameObject.name}'.", this);
            return;
        }

        _outlineMaterial = new Material(shader)
        {
            name = $"{gameObject.name}_InteractableOutlineMat"
        };
    }

    /// <summary>
    /// Builds dedicated helper renderers matching each target mesh/skinned mesh.
    /// </summary>
    public void BuildHelperRenderers()
    {
        CleanupHelpers();

        if (_outlineMaterial == null)
        {
            CreateOutlineMaterial();
            if (_outlineMaterial == null) return;
        }

        List<Renderer> sourceRenderers = new List<Renderer>();

        if (_customRenderers != null && _customRenderers.Length > 0)
        {
            sourceRenderers.AddRange(_customRenderers);
        }
        else if (_includeChildren)
        {
            sourceRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }
        else
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null) sourceRenderers.Add(r);
        }

        foreach (Renderer source in sourceRenderers)
        {
            if (source == null) continue;

            // Skip particle systems, trail renderers, UI, or previous helper objects
            if (source is ParticleSystemRenderer || source is TrailRenderer || source is LineRenderer) continue;
            if (source.gameObject.name == "[OutlineHelper]") continue;

            if (source is MeshRenderer mr)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                GameObject helperGO = new GameObject("[OutlineHelper]");
                helperGO.hideFlags = HideFlags.DontSave;
                helperGO.layer = source.gameObject.layer;

                Transform t = helperGO.transform;
                t.SetParent(source.transform, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;

                MeshFilter helperMF = helperGO.AddComponent<MeshFilter>();
                helperMF.sharedMesh = mf.sharedMesh;

                MeshRenderer helperMR = helperGO.AddComponent<MeshRenderer>();
                ConfigureHelperRenderer(helperMR, mf.sharedMesh.subMeshCount);

                _helperObjects.Add(helperGO);
                _outlineRenderers.Add(helperMR);
            }
            else if (source is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;

                GameObject helperGO = new GameObject("[OutlineHelper]");
                helperGO.hideFlags = HideFlags.DontSave;
                helperGO.layer = source.gameObject.layer;

                Transform t = helperGO.transform;
                t.SetParent(source.transform, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;

                SkinnedMeshRenderer helperSMR = helperGO.AddComponent<SkinnedMeshRenderer>();
                helperSMR.sharedMesh = smr.sharedMesh;
                helperSMR.bones = smr.bones;
                helperSMR.rootBone = smr.rootBone;
                ConfigureHelperRenderer(helperSMR, smr.sharedMesh.subMeshCount);

                _helperObjects.Add(helperGO);
                _outlineRenderers.Add(helperSMR);
            }
        }
    }

    private void ConfigureHelperRenderer(Renderer helper, int subMeshCount)
    {
        // Assign outline material to all submeshes so the entire mesh is outlined
        Material[] mats = new Material[Mathf.Max(1, subMeshCount)];
        for (int i = 0; i < mats.Length; i++)
        {
            mats[i] = _outlineMaterial;
        }
        helper.sharedMaterials = mats;

        helper.shadowCastingMode = ShadowCastingMode.Off;
        helper.receiveShadows = false;
        helper.lightProbeUsage = LightProbeUsage.Off;
        helper.reflectionProbeUsage = ReflectionProbeUsage.Off;
        helper.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        helper.enabled = _isOutlineActive;
    }

    private void CleanupHelpers()
    {
        for (int i = 0; i < _helperObjects.Count; i++)
        {
            if (_helperObjects[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_helperObjects[i]);
                }
                else
                {
                    DestroyImmediate(_helperObjects[i]);
                }
            }
        }
        _helperObjects.Clear();
        _outlineRenderers.Clear();
    }

    /// <summary>
    /// Enables or disables the outline contour highlight.
    /// </summary>
    public void SetOutlineActive(bool active)
    {
        _isOutlineActive = active;

        if (!_isInitialized)
        {
            Initialize();
        }

        ApplyActiveState();
    }

    [ContextMenu("Test Show Outline")]
    public void ShowOutline() => SetOutlineActive(true);

    [ContextMenu("Test Hide Outline")]
    public void HideOutline() => SetOutlineActive(false);

    private void ApplyActiveState()
    {
        SetRenderersEnabled(_isOutlineActive && enabled && gameObject.activeInHierarchy);
    }

    private void SetRenderersEnabled(bool enabledState)
    {
        for (int i = 0; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] != null)
            {
                _outlineRenderers[i].enabled = enabledState;
            }
        }
    }

    /// <summary>
    /// Re-evaluates target renderers in case meshes or hierarchy changed.
    /// </summary>
    public void RefreshRenderers()
    {
        BuildHelperRenderers();
        UpdateMaterialProperties();
        ApplyActiveState();
    }

    /// <summary>
    /// Synchronizes material properties (Color, Width, ZTest, ExtrusionMode).
    /// </summary>
    public void UpdateMaterialProperties()
    {
        if (_outlineMaterial == null) return;

        _outlineMaterial.SetColor(OutlineColorId, _outlineColor);
        _outlineMaterial.SetFloat(OutlineWidthId, _outlineWidth);
        _outlineMaterial.SetFloat(ZTestId, (float)_outlineMode);
        _outlineMaterial.SetFloat(ExtrusionModeId, (float)_extrusionMode);
    }
}
