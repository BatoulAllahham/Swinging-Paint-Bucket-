using UnityEngine;
using Seb.Fluid.Simulation;

// Attach to any empty GameObject. Wire bucketA, bucketB, canvasTransform in Inspector.
// Press H to show/hide the panel so you can move the camera freely.
public class MixingSceneUI : MonoBehaviour
{
    [Header("Wire in Inspector")]
    public FluidSim bucketA;
    public FluidSim bucketB;
    public Transform canvasTransform;

    [Header("UI")]
    [Range(0.5f, 4f)] public float uiScale = 2f;

    // Per-bucket color — initialized from FluidSim in Start()
    Color _colA = Color.red;
    Color _colB = Color.yellow;

    // Canvas rotation per-axis
    float _tiltX, _tiltY, _tiltZ;

    Vector2 _scroll;
    bool _showUI = true;

    static readonly FluidSim.PaintType[] PaintTypes =
        { FluidSim.PaintType.Watercolor, FluidSim.PaintType.Acrylic, FluidSim.PaintType.WallPaint };
    static readonly string[] PaintNames = { "Watercolor", "Acrylic", "Wall" };

    static readonly FluidSim.SurfaceType[] SurfaceTypes = {
        FluidSim.SurfaceType.Canvas, FluidSim.SurfaceType.Wood,
        FluidSim.SurfaceType.Sponge, FluidSim.SurfaceType.Glass, FluidSim.SurfaceType.Plastic
    };
    static readonly string[] SurfaceNames = { "Canvas", "Wood", "Sponge", "Glass", "Plastic" };

    // SPH presets — mirrors OnValidate in FluidSim
    // Hole starts at 0 so the bucket is closed on start (issue 5)
    static readonly float[] ViscPresets = { 0.0f, 0.0002f, 0.0004f };
    static readonly float[] RadiusPresets = { 0.2f, 0.2f, 0.2f };
    static readonly float[] DensPresets = { 800f, 800f, 800f };
    static readonly float[] PressPresets = { 200f, 200f, 200f };
    static readonly float[] NearPresets = { 2f, 2f, 2f };
    static readonly float[] HolePresets = { 0f, 0f, 0f };

    GUIStyle _box, _lbl, _hdr, _btn, _btnActive;
    bool _stylesReady;

    // OrbitCam reads this to skip input when the mouse is over the UI panel
    public static bool MouseOverUI { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (bucketA != null) { _colA = bucketA.paintColour; bucketA.holeSize = 0f; }
        if (bucketB != null) { _colB = bucketB.paintColour; bucketB.holeSize = 0f; }
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

        y = DrawBucket(x, y, w, " Bucket A ", bucketA, ref _colA);
        y += 12f;
        y = DrawBucket(x, y, w, " Bucket B ", bucketB, ref _colB);
        y += 12f;
        y = DrawCanvasSection(x, y, w);

        GUI.EndScrollView();
    }

    // ── Bucket panel ──────────────────────────────────────────────────────────

    float DrawBucket(float x, float y, float w, string title, FluidSim sim, ref Color col)
    {
        GUI.Label(new Rect(x, y, w, 18), title, _hdr); y += 22f;

        if (sim == null)
        {
            GUI.Label(new Rect(x, y, w, 18), "(not assigned)", _lbl);
            return y + 20f;
        }

        // Color picker — show current value in label (fix issue 1)
        GUI.Label(new Rect(x, y, w, 16), "Color", _lbl); y += 18f;
        col.r = LabelSlider(x, y, w, $"R  {col.r:F2}", col.r, 0f, 1f); y += 20f;
        col.g = LabelSlider(x, y, w, $"G  {col.g:F2}", col.g, 0f, 1f); y += 20f;
        col.b = LabelSlider(x, y, w, $"B  {col.b:F2}", col.b, 0f, 1f); y += 20f;
        sim.paintColour = col;

        // Color swatch
        var prev = GUI.color;
        GUI.color = col;
        GUI.DrawTexture(new Rect(x, y, w, 16), Texture2D.whiteTexture);
        GUI.color = prev;
        y += 22f;

        // Paint type presets
        GUI.Label(new Rect(x, y, w, 16), "Paint Type (preset)", _lbl); y += 18f;
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
                sim.holeSize = HolePresets[i];
            }
        }
        y += 26f;

        // SPH sliders
        GUI.Label(new Rect(x, y, w, 16), "SPH Parameters", _lbl); y += 18f;
        sim.viscosityStrength = LabelSlider(x, y, w, $"Viscosity    {sim.viscosityStrength:F4}", sim.viscosityStrength, 0f, 50f); y += 20f;
        sim.smoothingRadius = LabelSlider(x, y, w, $"Smooth R     {sim.smoothingRadius:F3}", sim.smoothingRadius, 0.05f, 1f); y += 20f;
        sim.targetDensity = LabelSlider(x, y, w, $"Density      {sim.targetDensity:F0}", sim.targetDensity, 50f, 5000f); y += 20f;
        sim.pressureMultiplier = LabelSlider(x, y, w, $"Pressure     {sim.pressureMultiplier:F0}", sim.pressureMultiplier, 10f, 1000f); y += 20f;
        sim.nearPressureMultiplier = LabelSlider(x, y, w, $"Near Press   {sim.nearPressureMultiplier:F2}", sim.nearPressureMultiplier, 0f, 20f); y += 20f;
        sim.holeSize = LabelSlider(x, y, w, $"Hole Size    {sim.holeSize:F3}", sim.holeSize, 0f, 0.5f); y += 20f;

        // Spawner target density (spawn-time setting — takes effect after reset)
        if (sim.spawner != null)
        {
            sim.spawner.simulationTargetDensity = Mathf.RoundToInt(
                LabelSlider(x, y, w, $"Spawn Density {sim.spawner.simulationTargetDensity}", sim.spawner.simulationTargetDensity, 1f, 2000f));
            y += 20f;
        }
        int total = sim.positionBuffer != null ? sim.positionBuffer.count : 0;
        GUI.Label(new Rect(x, y, w, 18), $"Total: {total}  |  In bucket: {sim.currentParticleCount}", _lbl); y += 18f;
        GUI.Label(new Rect(x, y, w, 16), "(Spawn Density takes effect after Reset)", _lbl); y += 20f;

        return y;
    }

    // ── Canvas panel ──────────────────────────────────────────────────────────

    float DrawCanvasSection(float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 18), " Canvas", _hdr); y += 22f;

        // Tilt on all three axes (fix issue 3)
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
                if (bucketA != null) bucketA.surfaceType = SurfaceTypes[i];
                if (bucketB != null) bucketB.surfaceType = SurfaceTypes[i];
            }
        }
        y += Mathf.CeilToInt(SurfaceTypes.Length / 3f) * 26f + 4f;

        // Clear canvas + reset particles (fix issue 2)
        if (GUI.Button(new Rect(x, y, w, 26), "Clear Canvas + Reset Particles", _btn))
            ClearAll();
        y += 30f;

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