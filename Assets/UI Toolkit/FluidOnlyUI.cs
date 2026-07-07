using UnityEngine;
using UnityEngine.UIElements;
using PaintSim.Fluid.Simulation;
using UnityEngine.EventSystems; 

public class FluidOnlyMasterUI : MonoBehaviour
{
    [Header("Simulation Reference")]
    public FluidOnlySim fluidSim;
    
    [Tooltip("The transform to rotate when using the fluid rotation sliders.")]
    public Transform targetTransform;

    private UIDocument uiDocument;
    private VisualElement rootVisual;
    private VisualElement initialConfigPanel;
    private VisualElement mainSimulationPanel;

    private Label lblParticles, lblWeight, lblFlow, lblSensor;
    private VisualElement topBottomControls, sideControls, colorPreview;

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        uiDocument.rootVisualElement.pickingMode = PickingMode.Position;
        
        rootVisual = uiDocument.rootVisualElement;

        // Auto-find the simulation if not assigned
        if (fluidSim == null) fluidSim = FindObjectOfType<FluidOnlySim>();

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
    }
    
    void Start()
    {
        if (fluidSim != null)
        {
            UpdateVisualsToMatchSim(rootVisual, fluidSim);
        }
    }

    void Update()
    {
        // Prevent UI interactions from clicking through to the scene
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) 
        {
            return; 
        }

        if (mainSimulationPanel == null || mainSimulationPanel.style.display == DisplayStyle.None)
            return;

        // Update Live Telemetry
        if (fluidSim != null)
        {
            if (lblParticles != null) lblParticles.text = $"PARTICLES: {fluidSim.currentParticleCount:N0}";
            if (lblWeight != null) lblWeight.text = $"WEIGHT: {fluidSim.currentBucketWeight:F3} kg";
            if (lblFlow != null) lblFlow.text = $"FLOW SPEED: {fluidSim.currentFlowSpeed:F2}";
            if (lblSensor != null) lblSensor.text = $"SENSOR POS: {fluidSim.sensorCenter.x:F2}, {fluidSim.sensorCenter.y:F2}, {fluidSim.sensorCenter.z:F2}";
        }
    }

    void UpdateRotation(string axis, float value)
    {
        if (targetTransform == null) return;
        
        Vector3 rot = targetTransform.localEulerAngles;
        if (axis == "x") rot.x = value;
        if (axis == "y") rot.y = value;
        if (axis == "z") rot.z = value;
        
        targetTransform.localEulerAngles = rot;
    }

    void UpdateColorPreview(Color c)
    {
        if (colorPreview != null) colorPreview.style.backgroundColor = c;
    }

    private void ClearCanvasTextures()
    {
        var prevActive = RenderTexture.active;

        ClearSharedTexture("sharedPaintAccumTexture");
        ClearSharedTexture("sharedPaintStyleTexture");

        if (fluidSim != null)
        {
            ClearInstanceTexture(fluidSim, "paintTexture");
        }

        RenderTexture.active = prevActive;
    }

    private void ClearSharedTexture(string fieldName)
    {
        var field = typeof(FluidOnlySim).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var rt = field?.GetValue(null) as RenderTexture;
        if (rt == null) return;

        RenderTexture.active = rt;
        GL.Clear(false, true, Color.clear);
    }

    private void ClearInstanceTexture(FluidOnlySim sim, string fieldName)
    {
        if (sim == null) return;
        var field = typeof(FluidOnlySim).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rt = field?.GetValue(sim) as RenderTexture;
        if (rt == null) return;

        RenderTexture.active = rt;
        GL.Clear(false, true, Color.clear);
    }

    void RegisterAllCallbacks(VisualElement root)
    {
        // 1. Panel Navigation & Resets
        var confirmBtn = root.Q<Button>("confirm-setup-btn");
        if (confirmBtn != null)
        {
            confirmBtn.clicked += () => {
                if (fluidSim != null)
                {
                    fluidSim.ApplySurfacePreset();
                    fluidSim.ApplyPaintTypeSettings();
                    fluidSim.ResetParticles(); 
                }

                if (initialConfigPanel != null) initialConfigPanel.style.display = DisplayStyle.None;
                if (mainSimulationPanel != null) mainSimulationPanel.style.display = DisplayStyle.Flex;

                UpdateVisualsToMatchSim(root, fluidSim);
            };
        }

        var resetBtn = root.Q<Button>("reset-btn");
        if (resetBtn != null)
        {
            resetBtn.clicked += () => {
                ClearCanvasTextures();

                if (fluidSim != null)
                {
                    fluidSim.ResetParticles();
                    fluidSim.holeSize = 0f; // Seal up the holes on reset
                }

                if (mainSimulationPanel != null) mainSimulationPanel.style.display = DisplayStyle.None;
                if (initialConfigPanel != null) initialConfigPanel.style.display = DisplayStyle.Flex;
                
                UpdateVisualsToMatchSim(root, fluidSim);
            };
        }

        root.Q<Button>("back-to-menu-btn")?.RegisterCallback<ClickEvent>(evt => {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
        });

        // 2. Paint Colors
        root.Q<Slider>("color-r")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) { Color c = fluidSim.paintColour; c.r = evt.newValue; fluidSim.paintColour = c; UpdateColorPreview(c); } });
        root.Q<Slider>("color-g")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) { Color c = fluidSim.paintColour; c.g = evt.newValue; fluidSim.paintColour = c; UpdateColorPreview(c); } });
        root.Q<Slider>("color-b")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) { Color c = fluidSim.paintColour; c.b = evt.newValue; fluidSim.paintColour = c; UpdateColorPreview(c); } });
        
        // 3. Types and Presets
        root.Q<EnumField>("paint-type")?.RegisterValueChangedCallback(evt => {
            if (fluidSim != null) { 
                fluidSim.paintType = (FluidOnlySim.PaintType)evt.newValue;
                fluidSim.ApplyPaintTypeSettings();
            } 
        });

        root.Q<EnumField>("canvas-type")?.RegisterValueChangedCallback(evt => { 
            if (fluidSim != null) { 
                fluidSim.surfaceType = (FluidOnlySim.SurfaceType)evt.newValue; 
                fluidSim.ApplySurfacePreset(); 
            } 
        });

        // 4. Target/Fluid Container Rotation
        root.Q<Slider>("rot-x")?.RegisterValueChangedCallback(e => UpdateRotation("x", e.newValue));
        root.Q<Slider>("rot-y")?.RegisterValueChangedCallback(e => UpdateRotation("y", e.newValue));
        root.Q<Slider>("rot-z")?.RegisterValueChangedCallback(e => UpdateRotation("z", e.newValue));

        // 5. Canvas Collision Rotation
        root.Q<Slider>("canvas-rot-x")?.RegisterValueChangedCallback(evt => { if (fluidSim != null && fluidSim.canvasCollision != null && fluidSim.canvasCollision.transform != null) { Vector3 rot = fluidSim.canvasCollision.transform.localEulerAngles; rot.x = evt.newValue; fluidSim.canvasCollision.transform.localEulerAngles = rot; } });
        root.Q<Slider>("canvas-rot-y")?.RegisterValueChangedCallback(evt => { if (fluidSim != null && fluidSim.canvasCollision != null && fluidSim.canvasCollision.transform != null) { Vector3 rot = fluidSim.canvasCollision.transform.localEulerAngles; rot.y = evt.newValue; fluidSim.canvasCollision.transform.localEulerAngles = rot; } });
        root.Q<Slider>("canvas-rot-z")?.RegisterValueChangedCallback(evt => { if (fluidSim != null && fluidSim.canvasCollision != null && fluidSim.canvasCollision.transform != null) { Vector3 rot = fluidSim.canvasCollision.transform.localEulerAngles; rot.z = evt.newValue; fluidSim.canvasCollision.transform.localEulerAngles = rot; } });

        // 6. Hole Placement Logic
        var sliderY = root.Q<Slider>("hole-y");
        root.Q<DropdownField>("hole-placement")?.RegisterValueChangedCallback(evt => {
            if (fluidSim == null) return;
            if (evt.newValue == "Bottom") {
                fluidSim.holeOrientation = 0;
                fluidSim.holePosition = new Vector3(0f, -1f, 0f);
                if(topBottomControls != null) topBottomControls.style.display = DisplayStyle.Flex;
                if(sideControls != null) sideControls.style.display = DisplayStyle.None;
                root.Q<Slider>("hole-x")?.SetValueWithoutNotify(0f);
                root.Q<Slider>("hole-z")?.SetValueWithoutNotify(0f);
            } else if (evt.newValue == "Side") {
                fluidSim.holeOrientation = 1;
                fluidSim.holePosition = new Vector3(0.5f, 0f, 0f);
                if(topBottomControls != null) topBottomControls.style.display = DisplayStyle.None;
                if(sideControls != null) sideControls.style.display = DisplayStyle.Flex;
                if (sliderY != null) {
                    sliderY.lowValue = -1f;
                    sliderY.highValue = 1f;
                    sliderY.SetValueWithoutNotify(0f);
                }
                root.Q<DropdownField>("hole-side-x")?.SetValueWithoutNotify("Right (0.5)");
            }
        });

        root.Q<Slider>("hole-x")?.RegisterValueChangedCallback(evt => { if(fluidSim!=null) fluidSim.holePosition.x = evt.newValue; });
        root.Q<Slider>("hole-z")?.RegisterValueChangedCallback(evt => { if(fluidSim!=null) fluidSim.holePosition.z = evt.newValue; });
        root.Q<Slider>("hole-y")?.RegisterValueChangedCallback(evt => { if(fluidSim!=null) fluidSim.holePosition.y = evt.newValue; });
        root.Q<DropdownField>("hole-side-x")?.RegisterValueChangedCallback(evt => { if(fluidSim!=null) fluidSim.holePosition.x = evt.newValue.StartsWith("Left") ? -0.5f : 0.5f; });
        root.Q<Slider>("hole-size")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.holeSize = evt.newValue; });

        // 7. SPH and Environment Variables
        root.Q<Slider>("time-scale")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.normalTimeScale = evt.newValue; });
        root.Q<Slider>("max-fps")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.maxTimestepFPS = evt.newValue; });
        root.Q<SliderInt>("iterations")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.iterationsPerFrame = evt.newValue; });
        root.Q<Slider>("gravity")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.gravity = evt.newValue; });
        root.Q<Slider>("radius")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.smoothingRadius = evt.newValue; });
        root.Q<Slider>("density")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.targetDensity = evt.newValue; });
        root.Q<Slider>("pressure")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.pressureMultiplier = evt.newValue; });
        root.Q<Slider>("near-pressure")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.nearPressureMultiplier = evt.newValue; });
        root.Q<Slider>("stiffness")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.springStiffness = evt.newValue; });
        root.Q<Slider>("plasticity")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.plasticityRate = evt.newValue; });
        root.Q<Slider>("yield")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.yieldRatio = evt.newValue; });
        root.Q<Slider>("viscosity")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.viscosityStrength = evt.newValue; });
        root.Q<Slider>("damping")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.collisionDamping = evt.newValue; });
        root.Q<Slider>("weight")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.weightPerParticle = evt.newValue; });
        root.Q<Slider>("temp-slider")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.temperature = evt.newValue; });
        root.Q<Slider>("humidity-slider")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.humidity = evt.newValue; });
        root.Q<Slider>("evap-slider")?.RegisterValueChangedCallback(evt => { if (fluidSim != null) fluidSim.evaporationRate = evt.newValue; });
    }

    void UpdateVisualsToMatchSim(VisualElement root, FluidOnlySim sim)
    {
        if (sim == null) return;
        
        // Setup Colors
        root.Q<Slider>("color-r")?.SetValueWithoutNotify(sim.paintColour.r);
        root.Q<Slider>("color-g")?.SetValueWithoutNotify(sim.paintColour.g);
        root.Q<Slider>("color-b")?.SetValueWithoutNotify(sim.paintColour.b);
        UpdateColorPreview(sim.paintColour);
        
        // Setup Enums
        var paintEnum = root.Q<EnumField>("paint-type");
        if(paintEnum != null) { paintEnum.Init(sim.paintType); paintEnum.SetValueWithoutNotify(sim.paintType); }
        
        var canvasEnum = root.Q<EnumField>("canvas-type");
        if(canvasEnum != null) { canvasEnum.Init(sim.surfaceType); canvasEnum.SetValueWithoutNotify(sim.surfaceType); }

        // Container/Canvas rotations
        if (targetTransform != null)
        {
            Vector3 targetRot = targetTransform.localEulerAngles;
            root.Q<Slider>("rot-x")?.SetValueWithoutNotify(targetRot.x);
            root.Q<Slider>("rot-y")?.SetValueWithoutNotify(targetRot.y);
            root.Q<Slider>("rot-z")?.SetValueWithoutNotify(targetRot.z);
        }

        if (sim.canvasCollision != null && sim.canvasCollision.transform != null)
        {
            Vector3 canvasRot = sim.canvasCollision.transform.localEulerAngles;
            root.Q<Slider>("canvas-rot-x")?.SetValueWithoutNotify(canvasRot.x);
            root.Q<Slider>("canvas-rot-y")?.SetValueWithoutNotify(canvasRot.y);
            root.Q<Slider>("canvas-rot-z")?.SetValueWithoutNotify(canvasRot.z);
        }

        // SPH and Env settings
        root.Q<Slider>("time-scale")?.SetValueWithoutNotify(sim.normalTimeScale);
        root.Q<Slider>("max-fps")?.SetValueWithoutNotify(sim.maxTimestepFPS);
        root.Q<SliderInt>("iterations")?.SetValueWithoutNotify(sim.iterationsPerFrame);
        root.Q<Slider>("gravity")?.SetValueWithoutNotify(sim.gravity);
        root.Q<Slider>("radius")?.SetValueWithoutNotify(sim.smoothingRadius);
        root.Q<Slider>("density")?.SetValueWithoutNotify(sim.targetDensity);
        root.Q<Slider>("pressure")?.SetValueWithoutNotify(sim.pressureMultiplier);
        root.Q<Slider>("near-pressure")?.SetValueWithoutNotify(sim.nearPressureMultiplier);
        root.Q<Slider>("stiffness")?.SetValueWithoutNotify(sim.springStiffness);
        root.Q<Slider>("plasticity")?.SetValueWithoutNotify(sim.plasticityRate);
        root.Q<Slider>("yield")?.SetValueWithoutNotify(sim.yieldRatio);
        root.Q<Slider>("viscosity")?.SetValueWithoutNotify(sim.viscosityStrength);
        root.Q<Slider>("damping")?.SetValueWithoutNotify(sim.collisionDamping);
        root.Q<Slider>("weight")?.SetValueWithoutNotify(sim.weightPerParticle);
        root.Q<Slider>("temp-slider")?.SetValueWithoutNotify(sim.temperature);
        root.Q<Slider>("humidity-slider")?.SetValueWithoutNotify(sim.humidity);
        root.Q<Slider>("evap-slider")?.SetValueWithoutNotify(sim.evaporationRate);
        root.Q<Slider>("hole-size")?.SetValueWithoutNotify(sim.holeSize);
        
        // Hole Placements
        var holePlacement = root.Q<DropdownField>("hole-placement");
        if (sim.holeOrientation == 1) 
        {
            holePlacement?.SetValueWithoutNotify("Side");
            if (topBottomControls != null) topBottomControls.style.display = DisplayStyle.None;
            if (sideControls != null) sideControls.style.display = DisplayStyle.Flex;
            
            var sliderY = root.Q<Slider>("hole-y");
            if (sliderY != null)
            {
                sliderY.lowValue = -1f;
                sliderY.highValue = 1f;
                sliderY.SetValueWithoutNotify(sim.holePosition.y);
            }
            root.Q<DropdownField>("hole-side-x")?.SetValueWithoutNotify(sim.holePosition.x < 0 ? "Left (-0.5)" : "Right (0.5)");
        } 
        else 
        {
            holePlacement?.SetValueWithoutNotify("Bottom");
            
            if (topBottomControls != null) topBottomControls.style.display = DisplayStyle.Flex;
            if (sideControls != null) sideControls.style.display = DisplayStyle.None;
            
            root.Q<Slider>("hole-x")?.SetValueWithoutNotify(sim.holePosition.x);
            root.Q<Slider>("hole-z")?.SetValueWithoutNotify(sim.holePosition.z);
        }
    }
}