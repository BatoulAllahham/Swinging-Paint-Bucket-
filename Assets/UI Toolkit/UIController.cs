using UnityEngine;
using UnityEngine.UIElements;
using PaintSim.Fluid.Simulation;

public class FluidMasterUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private FluidSim[] allBuckets;
    private FluidSim activeBucket;
    private ScrollView bucketList;
    private VisualElement rootVisual;

    private VisualElement initialConfigPanel;
    private VisualElement mainSimulationPanel;

    private Label lblParticles, lblWeight, lblFlow, lblSensor;
    private VisualElement topBottomControls, sideControls, colorPreview;

    private bool isPlaying = false; 

    private Pendulum GetPendulum()
    {
        if (activeBucket == null) return FindObjectOfType<Pendulum>();
        Pendulum p = activeBucket.GetComponent<Pendulum>();
        if (p == null) p = activeBucket.GetComponentInParent<Pendulum>();
        if (p == null) p = activeBucket.GetComponentInChildren<Pendulum>();
        if (p == null) p = FindObjectOfType<Pendulum>(); 
        return p;
    }

    private Rope GetRope()
    {
        if (activeBucket == null) return FindObjectOfType<Rope>();
        Rope r = activeBucket.GetComponent<Rope>();
        if (r == null) r = activeBucket.GetComponentInParent<Rope>();
        if (r == null) r = activeBucket.GetComponentInChildren<Rope>();
        if (r == null) r = FindObjectOfType<Rope>(); 
        return r;
    }

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        
        rootVisual = uiDocument.rootVisualElement;

        initialConfigPanel = rootVisual.Q<VisualElement>("initial-config-panel");
        mainSimulationPanel = rootVisual.Q<VisualElement>("main-simulation-panel");

        lblParticles = rootVisual.Q<Label>("lbl-particles");
        lblWeight = rootVisual.Q<Label>("lbl-weight");
        lblFlow = rootVisual.Q<Label>("lbl-flow");
        lblSensor = rootVisual.Q<Label>("lbl-sensor");

        topBottomControls = rootVisual.Q<VisualElement>("hole-top-bottom-controls");
        sideControls = rootVisual.Q<VisualElement>("hole-side-controls");
        colorPreview = rootVisual.Q<VisualElement>("color-preview");

        RegisterAllCallbacks(rootVisual);
        InitializeBucketList(rootVisual);
    }
    
    void Update()
    {
        if (mainSimulationPanel == null || mainSimulationPanel.style.display == DisplayStyle.None)
            return;

        if (activeBucket != null)
        {
            if (lblParticles != null) lblParticles.text = $"PARTICLES: {activeBucket.currentParticleCount:N0}";
            if (lblWeight != null) lblWeight.text = $"WEIGHT: {activeBucket.currentBucketWeight:F3} kg";
            if (lblFlow != null) lblFlow.text = $"FLOW SPEED: {activeBucket.currentFlowSpeed:F2}";
            if (lblSensor != null) lblSensor.text = $"SENSOR POS: {activeBucket.sensorCenter.x:F2}, {activeBucket.sensorCenter.y:F2}, {activeBucket.sensorCenter.z:F2}";

            var pendulum = GetPendulum();
            if (pendulum != null && isPlaying)
            {
                UpdateSliderLiveValue("pen-theta-vel", pendulum.ThetaAngularVelocity);
                UpdateSliderLiveValue("pen-phi-vel", pendulum.PhiAngularVelocity);
                UpdateSliderLiveValue("pen-theta-deg", pendulum.ThetaDegree);
                UpdateSliderLiveValue("pen-phi-deg", pendulum.PhiDegree);
                UpdateSliderLiveValue("swinging-rate", pendulum.SwingingRate);
            }
        }
    }

    private void UpdateSliderLiveValue(string sliderName, float liveValue)
    {
        var slider = rootVisual.Q<Slider>(sliderName);
        if (slider != null && rootVisual.panel.focusController.focusedElement != slider)
        {
            slider.SetValueWithoutNotify(liveValue);
        }
    }

    void Start()
    {
        if (activeBucket != null)
        {
            UpdateVisualsToMatchBucket(rootVisual, activeBucket);
        }
    }

    void InitializeBucketList(VisualElement root)
    {
        bucketList = root.Q<ScrollView>("bucket-list");
        if (bucketList == null) return;
        bucketList.Clear();
        allBuckets = FindObjectsOfType<FluidSim>();

        foreach (var bucket in allBuckets)
        {
            Button btn = new Button { text = bucket.name };
            btn.AddToClassList("bucket-btn");
            btn.clicked += () => SelectBucket(bucket, btn, root);
            bucketList.Add(btn);
        }

        if (allBuckets.Length > 0) SelectBucket(allBuckets[0], (Button)bucketList.ElementAt(0), root);
    }

    void SelectBucket(FluidSim bucket, Button activeBtn, VisualElement root)
    {
        activeBucket = bucket;
        foreach (var child in bucketList.Children()) child.RemoveFromClassList("bucket-btn-active");
        activeBtn.AddToClassList("bucket-btn-active");
        UpdateVisualsToMatchBucket(root, bucket);
    }

    void UpdateColorPreview(Color c)
    {
        if (colorPreview != null) colorPreview.style.backgroundColor = c;
    }

private void ClearCanvasTextures()
{
    var prevActive = RenderTexture.active;

    // 1. Wipe the shared rendering textures clean
    ClearSharedTexture("sharedPaintAccumTexture");
    ClearSharedTexture("sharedPaintStyleTexture");

    // 2. Wipe the individual canvas/paint textures for ALL available buckets in the scene
    if (allBuckets != null)
    {
        foreach (var bucket in allBuckets)
        {
            ClearInstanceTexture(bucket, "paintTexture");
        }
    }

    RenderTexture.active = prevActive;
}

private void ClearSharedTexture(string fieldName)
{
    var field = typeof(FluidSim).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    var rt = field?.GetValue(null) as RenderTexture;
    if (rt == null) return;

    RenderTexture.active = rt;
    GL.Clear(false, true, Color.clear);
}

private void ClearInstanceTexture(FluidSim sim, string fieldName)
{
    if (sim == null) return;
    var field = typeof(FluidSim).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var rt = field?.GetValue(sim) as RenderTexture;
    if (rt == null) return;

    RenderTexture.active = rt;
    GL.Clear(false, true, Color.clear);
}

    void RegisterAllCallbacks(VisualElement root)
    {
        var confirmBtn = root.Q<Button>("confirm-setup-btn");
        if (confirmBtn != null)
        {
            confirmBtn.clicked += () => {
                if (activeBucket != null)
                {
                    activeBucket.ApplySurfacePreset();
                    activeBucket.ApplyPaintTypeSettings();
                    activeBucket.ResetParticles(); 
                }

                if (initialConfigPanel != null) initialConfigPanel.style.display = DisplayStyle.None;
                if (mainSimulationPanel != null) mainSimulationPanel.style.display = DisplayStyle.Flex;

                UpdateVisualsToMatchBucket(root, activeBucket);
            };
        }

        var playBtn = root.Q<Button>("play-pause-btn");
        var resetBtn = root.Q<Button>("reset-btn");

        if (playBtn != null)
        {
            playBtn.clicked += () => {
                isPlaying = !isPlaying;
                playBtn.text = isPlaying ? "PAUSE PENDULUM" : "START PENDULUM";
                ColorUtility.TryParseHtmlString(isPlaying ? "#ca8a04" : "#059669", out Color btnColor);
                playBtn.style.backgroundColor = btnColor;

                var p = GetPendulum();
                if (p != null) p.isSimulating = isPlaying;
                
                var r = GetRope();
                if (r != null) r.isSimulating = isPlaying;
            };
        }

if (resetBtn != null)
{
    resetBtn.clicked += () => {
        if (isPlaying && playBtn != null) {
            isPlaying = false;
            playBtn.text = "START PENDULUM";
            ColorUtility.TryParseHtmlString("#059669", out Color defaultGreen);
            playBtn.style.backgroundColor = defaultGreen;
            
            var pend = GetPendulum();
            if (pend != null) pend.isSimulating = false;
            
            var rop = GetRope();
            if (rop != null) rop.isSimulating = false;
        }
        
        ClearCanvasTextures();

        // Reset particles for all buckets to clean up old frames completely
        if (allBuckets != null)
        {
            foreach (var bucket in allBuckets)
            {
                bucket.ResetParticles();
                bucket.holeSize = 0f; // Seal up the holes on reset
            }
        }
        else if (activeBucket != null)
        {
            activeBucket.ResetParticles();
        }
        
        var p = GetPendulum();
        if (p != null){ p.ResetToInitial();
        p.ThetaAngularVelocity = 0f;
            p.PhiAngularVelocity = 0f;
            p.ThetaDegree = 45f;    // Set to your actual default starting angle
            p.PhiDegree = 0f;       // Set to your actual default starting angle
            p.Gravity = 9.81f;      // Standard gravity
            p.AirDensity = 1.2f;    // Standard air density
            p.DragCoefficient = 0.5f; 
            p.SwingingRate = 1f;}

        
        var r = GetRope();
        if (r != null) r.ResetRope();

        // Go back to the configuration screen to allow changes to baseline parameters
        if (mainSimulationPanel != null) mainSimulationPanel.style.display = DisplayStyle.None;
        if (initialConfigPanel != null) initialConfigPanel.style.display = DisplayStyle.Flex;
        
        UpdateVisualsToMatchBucket(root, activeBucket);
    };
}

        root.Q<Slider>("color-r")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) { Color c = activeBucket.paintColour; c.r = evt.newValue; activeBucket.paintColour = c; UpdateColorPreview(c); } });
        root.Q<Slider>("color-g")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) { Color c = activeBucket.paintColour; c.g = evt.newValue; activeBucket.paintColour = c; UpdateColorPreview(c); } });
        root.Q<Slider>("color-b")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) { Color c = activeBucket.paintColour; c.b = evt.newValue; activeBucket.paintColour = c; UpdateColorPreview(c); } });
        
        root.Q<EnumField>("paint-type")?.RegisterValueChangedCallback(evt => {
             if (activeBucket != null) { 
                activeBucket.paintType = (FluidSim.PaintType)evt.newValue;
                activeBucket.ApplyPaintTypeSettings();
                 } });
        root.Q<EnumField>("canvas-type")?.RegisterValueChangedCallback(evt => 
        { if (activeBucket != null) 
    { 
        activeBucket.surfaceType = (FluidSim.SurfaceType)evt.newValue; 
        activeBucket.ApplySurfacePreset(); 
    } 
});
        root.Q<Slider>("canvas-rot-x")?.RegisterValueChangedCallback(evt => { if (activeBucket != null && activeBucket.canvasCollision != null) { Vector3 rot = activeBucket.canvasCollision.transform.localEulerAngles; rot.x = evt.newValue; activeBucket.canvasCollision.transform.localEulerAngles = rot; } });
        root.Q<Slider>("canvas-rot-y")?.RegisterValueChangedCallback(evt => { if (activeBucket != null && activeBucket.canvasCollision != null) { Vector3 rot = activeBucket.canvasCollision.transform.localEulerAngles; rot.y = evt.newValue; activeBucket.canvasCollision.transform.localEulerAngles = rot; } });
        root.Q<Slider>("canvas-rot-z")?.RegisterValueChangedCallback(evt => { if (activeBucket != null && activeBucket.canvasCollision != null) { Vector3 rot = activeBucket.canvasCollision.transform.localEulerAngles; rot.z = evt.newValue; activeBucket.canvasCollision.transform.localEulerAngles = rot; } });

        root.Q<Slider>("pen-theta-vel")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.ThetaAngularVelocity = evt.newValue; });
        root.Q<Slider>("pen-phi-vel")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.PhiAngularVelocity = evt.newValue; });
        root.Q<Slider>("pen-theta-deg")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.ThetaDegree = evt.newValue; });
        root.Q<Slider>("pen-phi-deg")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.PhiDegree = evt.newValue; });
        root.Q<Slider>("pen-gravity")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.Gravity = evt.newValue; });
        root.Q<Slider>("pen-air-density")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.AirDensity = evt.newValue; });
        root.Q<Slider>("pen-drag")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.DragCoefficient = evt.newValue; });
        root.Q<Slider>("swinging-rate")?.RegisterValueChangedCallback(evt => { var p = GetPendulum(); if (p != null) p.SwingingRate = evt.newValue; });
        root.Q<EnumField>("rope-type")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.Type = (Rope.RopeType)evt.newValue; });
        root.Q<Slider>("rope-length")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.RopeLengthProperty = evt.newValue; });
        root.Q<Slider>("rope-mass")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.TotalRopeMass = evt.newValue; });
        root.Q<SliderInt>("rope-segments")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.NumSegments = evt.newValue; });
        root.Q<Slider>("rope-radius")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.RopeRadius = evt.newValue; });
        root.Q<SliderInt>("rope-radial-seg")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.RadialSegments = evt.newValue; });
        root.Q<Slider>("rope-gravity")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.Gravity = evt.newValue; });
        root.Q<SliderInt>("rope-iterations")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.ConstraintIterations = evt.newValue; });
        root.Q<SliderInt>("rope-substeps")?.RegisterValueChangedCallback(evt => { var r = GetRope(); if (r != null) r.Substeps = evt.newValue; });

        var sliderY = root.Q<Slider>("hole-y");
        root.Q<DropdownField>("hole-placement")?.RegisterValueChangedCallback(evt => {
            if (activeBucket == null) return;
            if (evt.newValue == "Bottom") {
                activeBucket.holeOrientation = 0;
                activeBucket.holePosition = new Vector3(0f, -1f, 0f);
                topBottomControls.style.display = DisplayStyle.Flex;
                sideControls.style.display = DisplayStyle.None;
                root.Q<Slider>("hole-x")?.SetValueWithoutNotify(0f);
                root.Q<Slider>("hole-z")?.SetValueWithoutNotify(0f);
            } else if (evt.newValue == "Side") {
                activeBucket.holeOrientation = 1;
                activeBucket.holePosition = new Vector3(0.5f, 0f, 0f);
                topBottomControls.style.display = DisplayStyle.None;
                sideControls.style.display = DisplayStyle.Flex;
                if (sliderY != null) {
                    sliderY.lowValue = -1f;
                    sliderY.highValue = 1f;
                    sliderY.SetValueWithoutNotify(0f);
                }
                root.Q<DropdownField>("hole-side-x")?.SetValueWithoutNotify("Right (0.5)");
            }
        });

        root.Q<Slider>("hole-x")?.RegisterValueChangedCallback(evt => { if(activeBucket!=null) activeBucket.holePosition.x = evt.newValue; });
        root.Q<Slider>("hole-z")?.RegisterValueChangedCallback(evt => { if(activeBucket!=null) activeBucket.holePosition.z = evt.newValue; });
        root.Q<Slider>("hole-y")?.RegisterValueChangedCallback(evt => { if(activeBucket!=null) activeBucket.holePosition.y = evt.newValue; });
        root.Q<DropdownField>("hole-side-x")?.RegisterValueChangedCallback(evt => { if(activeBucket!=null) activeBucket.holePosition.x = evt.newValue.StartsWith("Left") ? -0.5f : 0.5f; });

        root.Q<Slider>("hole-size")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.holeSize = evt.newValue; });
        root.Q<Slider>("time-scale")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.normalTimeScale = evt.newValue; });
        root.Q<Slider>("max-fps")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.maxTimestepFPS = evt.newValue; });
        root.Q<SliderInt>("iterations")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.iterationsPerFrame = evt.newValue; });
        root.Q<Slider>("gravity")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.gravity = evt.newValue; });
        root.Q<Slider>("radius")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.smoothingRadius = evt.newValue; });
        root.Q<Slider>("density")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.targetDensity = evt.newValue; });
        root.Q<Slider>("pressure")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.pressureMultiplier = evt.newValue; });
        root.Q<Slider>("near-pressure")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.nearPressureMultiplier = evt.newValue; });
        root.Q<Slider>("stiffness")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.springStiffness = evt.newValue; });
        root.Q<Slider>("plasticity")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.plasticityRate = evt.newValue; });
        root.Q<Slider>("yield")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.yieldRatio = evt.newValue; });
        root.Q<Slider>("viscosity")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.viscosityStrength = evt.newValue; });
        root.Q<Slider>("damping")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.collisionDamping = evt.newValue; });
        root.Q<Slider>("weight")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.weightPerParticle = evt.newValue; });
        root.Q<Slider>("temp-slider")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.temperature = evt.newValue; });
        root.Q<Slider>("humidity-slider")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.humidity = evt.newValue; });
        root.Q<Slider>("evap-slider")?.RegisterValueChangedCallback(evt => { if (activeBucket != null) activeBucket.evaporationRate = evt.newValue; });
        root.Q<Button>("back-to-menu-btn")?.RegisterCallback<ClickEvent>(evt => {
    UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
});
    }

    void UpdateVisualsToMatchBucket(VisualElement root, FluidSim bucket)
    {
        if (bucket == null) return;
        
        root.Q<Slider>("color-r")?.SetValueWithoutNotify(bucket.paintColour.r);
        root.Q<Slider>("color-g")?.SetValueWithoutNotify(bucket.paintColour.g);
        root.Q<Slider>("color-b")?.SetValueWithoutNotify(bucket.paintColour.b);
        UpdateColorPreview(bucket.paintColour);
        
        var paintEnum = root.Q<EnumField>("paint-type");
        if(paintEnum != null) { paintEnum.Init(bucket.paintType); paintEnum.SetValueWithoutNotify(bucket.paintType); }
        
        var canvasEnum = root.Q<EnumField>("canvas-type");
        if(canvasEnum != null) { canvasEnum.Init(bucket.surfaceType); canvasEnum.SetValueWithoutNotify(bucket.surfaceType); }

        if (bucket.canvasCollision != null)
        {
            Vector3 canvasRot = bucket.canvasCollision.transform.localEulerAngles;
            canvasRot.x = -30f; 
            bucket.canvasCollision.transform.localEulerAngles = canvasRot;
            
            root.Q<Slider>("canvas-rot-x")?.SetValueWithoutNotify(canvasRot.x);
            root.Q<Slider>("canvas-rot-y")?.SetValueWithoutNotify(canvasRot.y);
            root.Q<Slider>("canvas-rot-z")?.SetValueWithoutNotify(canvasRot.z);
        }

        var pendulum = GetPendulum();
        if (pendulum != null)
        {
            root.Q<Slider>("pen-theta-vel")?.SetValueWithoutNotify(pendulum.ThetaAngularVelocity);
            root.Q<Slider>("pen-phi-vel")?.SetValueWithoutNotify(pendulum.PhiAngularVelocity);
            root.Q<Slider>("pen-theta-deg")?.SetValueWithoutNotify(pendulum.ThetaDegree);
            root.Q<Slider>("pen-phi-deg")?.SetValueWithoutNotify(pendulum.PhiDegree);
            root.Q<Slider>("pen-gravity")?.SetValueWithoutNotify(pendulum.Gravity);
            root.Q<Slider>("pen-air-density")?.SetValueWithoutNotify(pendulum.AirDensity);
            root.Q<Slider>("swinging-rate")?.SetValueWithoutNotify(pendulum.SwingingRate);
            root.Q<Slider>("pen-drag")?.SetValueWithoutNotify(pendulum.DragCoefficient);
        }

        var rope = GetRope();
        if (rope != null)
        {
            var ropeTypeField = root.Q<EnumField>("rope-type");
            if (ropeTypeField != null) { ropeTypeField.Init(rope.Type); ropeTypeField.SetValueWithoutNotify(rope.Type); }

            root.Q<Slider>("rope-length")?.SetValueWithoutNotify(rope.RopeLengthProperty);
            root.Q<Slider>("rope-mass")?.SetValueWithoutNotify(rope.TotalRopeMass);
            root.Q<SliderInt>("rope-segments")?.SetValueWithoutNotify(rope.NumSegments);
            root.Q<Slider>("rope-radius")?.SetValueWithoutNotify(rope.RopeRadius);
            root.Q<SliderInt>("rope-radial-seg")?.SetValueWithoutNotify(rope.RadialSegments);
            root.Q<Slider>("rope-gravity")?.SetValueWithoutNotify(rope.Gravity);
            root.Q<SliderInt>("rope-iterations")?.SetValueWithoutNotify(rope.ConstraintIterations);
            root.Q<SliderInt>("rope-substeps")?.SetValueWithoutNotify(rope.Substeps);
        }

        root.Q<Slider>("time-scale")?.SetValueWithoutNotify(bucket.normalTimeScale);
        root.Q<Slider>("max-fps")?.SetValueWithoutNotify(bucket.maxTimestepFPS);
        root.Q<SliderInt>("iterations")?.SetValueWithoutNotify(bucket.iterationsPerFrame);
        root.Q<Slider>("gravity")?.SetValueWithoutNotify(bucket.gravity);
        root.Q<Slider>("radius")?.SetValueWithoutNotify(bucket.smoothingRadius);
        root.Q<Slider>("density")?.SetValueWithoutNotify(bucket.targetDensity);
        root.Q<Slider>("pressure")?.SetValueWithoutNotify(bucket.pressureMultiplier);
        root.Q<Slider>("near-pressure")?.SetValueWithoutNotify(bucket.nearPressureMultiplier);
        root.Q<Slider>("stiffness")?.SetValueWithoutNotify(bucket.springStiffness);
        root.Q<Slider>("plasticity")?.SetValueWithoutNotify(bucket.plasticityRate);
        root.Q<Slider>("yield")?.SetValueWithoutNotify(bucket.yieldRatio);
        root.Q<Slider>("viscosity")?.SetValueWithoutNotify(bucket.viscosityStrength);
        root.Q<Slider>("damping")?.SetValueWithoutNotify(bucket.collisionDamping);
        root.Q<Slider>("weight")?.SetValueWithoutNotify(bucket.weightPerParticle);
        root.Q<Slider>("temp-slider")?.SetValueWithoutNotify(bucket.temperature);
        root.Q<Slider>("humidity-slider")?.SetValueWithoutNotify(bucket.humidity);
        root.Q<Slider>("evap-slider")?.SetValueWithoutNotify(bucket.evaporationRate);
        root.Q<Slider>("hole-size")?.SetValueWithoutNotify(bucket.holeSize);
        
        var holePlacement = root.Q<DropdownField>("hole-placement");
        if (bucket.holeOrientation == 1) 
        {
            holePlacement?.SetValueWithoutNotify("Side");
            topBottomControls.style.display = DisplayStyle.None;
            sideControls.style.display = DisplayStyle.Flex;
            
            var sliderY = root.Q<Slider>("hole-y");
            if (sliderY != null)
            {
                sliderY.lowValue = -1f;
                sliderY.highValue = 1f;
                sliderY.SetValueWithoutNotify(bucket.holePosition.y);
            }
            root.Q<DropdownField>("hole-side-x")?.SetValueWithoutNotify(bucket.holePosition.x < 0 ? "Left (-0.5)" : "Right (0.5)");
        } 
        else 
        {
            holePlacement?.SetValueWithoutNotify("Bottom");
            topBottomControls.style.display = DisplayStyle.Flex;
            sideControls.style.display = DisplayStyle.None;
            root.Q<Slider>("hole-x")?.SetValueWithoutNotify(bucket.holePosition.x);
            root.Q<Slider>("hole-z")?.SetValueWithoutNotify(bucket.holePosition.z);
        }
    }
}