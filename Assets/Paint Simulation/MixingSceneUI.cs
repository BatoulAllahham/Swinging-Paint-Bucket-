using UnityEngine;
using PaintSim.Fluid.Simulation;

// Attach to any empty GameObject. Wire bucketA, bucketB, canvasTransform in Inspector.
// Press H to show/hide the panel so you can move the camera freely.
//
// Two-screen flow:
//   Setup screen (before Start): choose paint type per bucket, canvas surface, and tilt.
//   Running screen (after Start): only a viscosity slider per bucket + Reset.
//   Reset clears the canvas, reseals both buckets, and returns to the Setup screen.
public class MixingSceneUI : MonoBehaviour
{
    [Header("Wire in Inspector")]
    public FluidSim bucketA; // Red bucket
    public FluidSim bucketB; // Yellow bucket
    public Transform canvasTransform;

    [Header("UI")]
    [Range(0.5f, 4f)] public float uiScale = 2f;

    // How far the hole opens when Start is pressed. Sealed (0) during setup and after Reset.
    [Range(0.01f, 0.5f)] public float pourHoleSize = 0.1f;

    // Live, shared hole size while running — drives both buckets. Initialized from
    // pourHoleSize when Start is pressed; adjustable in the running screen from then on.
    float _holeSize;

    // Canvas rotation per-axis
    float _tiltX, _tiltY, _tiltZ;

    Vector2 _scroll;
    bool _showUI = true;
    bool _hasStarted;

    static readonly FluidSim.PaintType[] PaintTypes =
        { FluidSim.PaintType.Watercolor, FluidSim.PaintType.Acrylic, FluidSim.PaintType.WallPaint };
    static readonly string[] PaintNames = { "Watercolor", "Acrylic", "Wall" };

    static readonly FluidSim.SurfaceType[] SurfaceTypes = {
        FluidSim.SurfaceType.Canvas, FluidSim.SurfaceType.Wood,
        FluidSim.SurfaceType.Sponge, FluidSim.SurfaceType.Glass, FluidSim.SurfaceType.Plastic
    };
    static readonly string[] SurfaceNames = { "Canvas", "Wood", "Sponge", "Glass", "Plastic" };

    // SPH presets — mirrors OnValidate in FluidSim
    static readonly float[] ViscPresets = { 0.0f, 0.0002f, 0.0004f };
    static readonly float[] RadiusPresets = { 0.2f, 0.2f, 0.2f };
    static readonly float[] DensPresets = { 800f, 800f, 800f };
    static readonly float[] PressPresets = { 200f, 200f, 200f };
    static readonly float[] NearPresets = { 2f, 2f, 2f };

    GUIStyle _box, _lbl, _hdr, _btn, _btnActive;
    bool _stylesReady;

    // OrbitCam reads this to skip input when the mouse is over the UI panel
    public static bool MouseOverUI { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (bucketA != null) bucketA.holeSize = 0f;
        if (bucketB != null) bucketB.holeSize = 0f;
        if (canvasTransform != null)
        {
            _tiltX = canvasTransform.localEulerAngles.x;
            _tiltY = canvasTransform.localEulerAngles.y;
            _tiltZ = canvasTransform.localEulerAngles.z;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) _showUI = !_showUI;

        // Compute mouse position in logical GUI space (Y-down, divided by uiScale).
        // Done in Update() — Event.current.mousePosition in OnGUI is unreliable
        // during non-mouse events (Layout/Repaint) and can leave MouseOverUI stuck true.
        float mx = Input.mousePosition.x / uiScale;
        float my = (Screen.height - Input.mousePosition.y) / uiScale;
        var mouseGUI = new Vector2(mx, my);
        var toggleRect = new Rect(4, 4, 28, 20);
        var panelRect = new Rect(10, 10, 270f, Screen.height / uiScale - 20f);
        MouseOverUI = toggleRect.Contains(mouseGUI) || (_showUI && panelRect.Contains(mouseGUI));
    }

    // ── GUI ───────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        BuildStyles();
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        float panelW = 270f;
        float panelH = Screen.height / uiScale - 20f;
        var panelRect = new Rect(10, 10, panelW, panelH);

        // Small always-visible toggle button so you can show the panel without keyboard
        if (GUI.Button(new Rect(4, 4, 28, 20), _showUI ? "◀" : "▶", _btn))
            _showUI = !_showUI;

        if (!_showUI) return;

        GUI.Box(panelRect, GUIContent.none, _box);

        _scroll = GUI.BeginScrollView(
            panelRect,
            _scroll,
            new Rect(0, 0, panelW - 20, 1400f),
            false, true);

        float x = 8f, y = 8f, w = panelW - 30f;

        y = _hasStarted ? DrawRunningUI(x, y, w) : DrawSetupUI(x, y, w);

        GUI.EndScrollView();
    }

    // ── Setup screen (UI1) ────────────────────────────────────────────────────

    float DrawSetupUI(float x, float y, float w)
    {
        y = DrawBucketSetup(x, y, w, "Yellow Bucket", bucketA);
        y += 12f;
        y = DrawBucketSetup(x, y, w, "Red Bucket", bucketB);
        y += 12f;
        y = DrawCanvasSetup(x, y, w);
        y += 8f;

        if (GUI.Button(new Rect(x, y, w, 30), "Start Simulation", _btn))
        {
            _hasStarted = true;
            _holeSize = pourHoleSize;
            if (bucketA != null) bucketA.holeSize = _holeSize;
            if (bucketB != null) bucketB.holeSize = _holeSize;
        }
        y += 34f;

        return y;
    }

    float DrawBucketSetup(float x, float y, float w, string title, FluidSim sim)
    {
        GUI.Label(new Rect(x, y, w, 18), title, _hdr); y += 22f;

        if (sim == null)
        {
            GUI.Label(new Rect(x, y, w, 18), "(not assigned)", _lbl);
            return y + 20f;
        }

        // Color swatch — informational only, colour is fixed per bucket (not user-editable)
        var prev = GUI.color;
        GUI.color = sim.paintColour;
        GUI.DrawTexture(new Rect(x, y, w, 14), Texture2D.whiteTexture);
        GUI.color = prev;
        y += 20f;

        // Paint type presets
        GUI.Label(new Rect(x, y, w, 16), "Paint Type", _lbl); y += 18f;
        float bw = (w - 4f) / 3f;
        for (int i = 0; i < PaintTypes.Length; i++)
        {
            var style = sim.paintType == PaintTypes[i] ? _btnActive : _btn;
            if (GUI.Button(new Rect(x + i * (bw + 2f), y, bw, 22), PaintNames[i], style))
            {
                sim.paintType = PaintTypes[i];
                sim.viscosityStrength = ViscPresets[i];
                sim.smoothingRadius = RadiusPresets[i];
                sim.targetDensity = DensPresets[i];
                sim.pressureMultiplier = PressPresets[i];
                sim.nearPressureMultiplier = NearPresets[i];
            }
        }
        y += 26f;

        return y;
    }

    float DrawCanvasSetup(float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 18), " Canvas", _hdr); y += 22f;

        // Tilt on all three axes
        GUI.Label(new Rect(x, y, w, 16), "Rotation", _lbl); y += 18f;
        _tiltX = LabelSlider(x, y, w, $"X  {_tiltX:F0}°", _tiltX, -90f, 0f); y += 20f;
        _tiltY = LabelSlider(x, y, w, $"Y  {_tiltY:F0}°", _tiltY, -180f, 180f); y += 20f;
        _tiltZ = LabelSlider(x, y, w, $"Z  {_tiltZ:F0}°", _tiltZ, -90f, 90f); y += 20f;
        if (canvasTransform != null)
            canvasTransform.localEulerAngles = new Vector3(_tiltX, _tiltY, _tiltZ);

        // Surface type
        GUI.Label(new Rect(x, y, w, 16), "Surface Type", _lbl); y += 18f;
        float bw = (w - 8f) / 3f;
        for (int i = 0; i < SurfaceTypes.Length; i++)
        {
            float bx = x + (i % 3) * (bw + 4f);
            float by = y + (i / 3) * 26f;
            bool active = bucketA != null && bucketA.surfaceType == SurfaceTypes[i];
            if (GUI.Button(new Rect(bx, by, bw, 22), SurfaceNames[i], active ? _btnActive : _btn))
            {
                // ApplySurfacePreset() is called explicitly because setting surfaceType from script
                // does not trigger FluidSim's OnValidate (that's an Editor-only Inspector callback) --
                // without this, the bounce/roughness/absorption params actually used by the compute
                // shader would never update.
                if (bucketA != null) { bucketA.surfaceType = SurfaceTypes[i]; bucketA.ApplySurfacePreset(); }
                if (bucketB != null) { bucketB.surfaceType = SurfaceTypes[i]; bucketB.ApplySurfacePreset(); }
            }
        }
        y += Mathf.CeilToInt(SurfaceTypes.Length / 3f) * 26f + 4f;

        return y;
    }

    // ── Running screen ────────────────────────────────────────────────────────

    float DrawRunningUI(float x, float y, float w)
    {
        y = DrawViscositySlider(x, y, w, "Yellow Bucket", bucketA);
        y += 10f;
        y = DrawViscositySlider(x, y, w, "Red Bucket", bucketB);
        y += 12f;

        // Shared hole size — drives both buckets so they pour together.
        _holeSize = LabelSlider(x, y, w, $"Hole Size    {_holeSize:F3}", _holeSize, 0f, 0.5f); y += 20f;
        if (bucketA != null) bucketA.holeSize = _holeSize;
        if (bucketB != null) bucketB.holeSize = _holeSize;
        y += 8f;

        if (GUI.Button(new Rect(x, y, w, 30), "Reset", _btn))
        {
            ClearAll();
            _hasStarted = false;
        }
        y += 34f;

        return y;
    }

    float DrawViscositySlider(float x, float y, float w, string title, FluidSim sim)
    {
        GUI.Label(new Rect(x, y, w, 18), title, _hdr); y += 22f;

        if (sim == null)
        {
            GUI.Label(new Rect(x, y, w, 18), "(not assigned)", _lbl);
            return y + 20f;
        }

        sim.viscosityStrength = LabelSlider(x, y, w, $"Viscosity    {sim.viscosityStrength:F4}", sim.viscosityStrength, 0f, 50f); y += 20f;

        return y;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    float LabelSlider(float x, float y, float w, string label, float val, float min, float max)
    {
        float lw = w * 0.52f;
        GUI.Label(new Rect(x, y, lw, 18), label, _lbl);
        return GUI.HorizontalSlider(new Rect(x + lw + 4f, y + 3f, w - lw - 4f, 14), val, min, max);
    }

    void BuildStyles()
    {
        if (_stylesReady) return;

        _box = new GUIStyle(GUI.skin.box);
        _box.normal.background = MakeTex(new Color(0.12f, 0.12f, 0.15f, 0.95f));

        _lbl = new GUIStyle(GUI.skin.label);
        _lbl.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        _lbl.fontSize = 11;

        _hdr = new GUIStyle(GUI.skin.label);
        _hdr.normal.textColor = Color.white;
        _hdr.fontStyle = FontStyle.Bold;
        _hdr.fontSize = 12;

        _btn = new GUIStyle(GUI.skin.button);
        _btn.normal.background = MakeTex(new Color(0.25f, 0.25f, 0.32f, 1f));
        _btn.hover.background = MakeTex(new Color(0.35f, 0.35f, 0.42f, 1f));
        _btn.normal.textColor = Color.white;
        _btn.fontSize = 10;

        _btnActive = new GUIStyle(_btn);
        _btnActive.normal.background = MakeTex(new Color(0.20f, 0.50f, 1.00f, 1f));
        _btnActive.hover.background = MakeTex(new Color(0.30f, 0.60f, 1.00f, 1f));

        _stylesReady = true;
    }

    static Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    void ClearAll()
    {
        // Clear canvas textures
        var prev = RenderTexture.active;
        ClearShared("sharedPaintAccumTexture");
        ClearShared("sharedPaintStyleTexture");
        ClearInstance(bucketA, "paintTexture");
        ClearInstance(bucketB, "paintTexture");
        RenderTexture.active = prev;

        // Reset all particles back to spawn positions and seal buckets
        bucketA?.ResetParticles();
        bucketB?.ResetParticles();
        if (bucketA != null) bucketA.holeSize = 0f;
        if (bucketB != null) bucketB.holeSize = 0f;
    }

    static void ClearShared(string field)
    {
        var rt = typeof(FluidSim)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as RenderTexture;
        if (rt == null) return;
        RenderTexture.active = rt;
        GL.Clear(false, true, Color.clear);
    }

    static void ClearInstance(FluidSim sim, string field)
    {
        if (sim == null) return;
        var rt = typeof(FluidSim)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(sim) as RenderTexture;
        if (rt == null) return;
        RenderTexture.active = rt;
        GL.Clear(false, true, Color.clear);
    }
}
