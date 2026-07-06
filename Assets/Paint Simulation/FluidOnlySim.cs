using System;
using UnityEngine;
using PaintSim.GPUSorting;
using Unity.Mathematics;
using System.Collections.Generic;
using PaintSim.Helpers;
using static PaintSim.Helpers.ComputeHelper;

namespace PaintSim.Fluid.Simulation
{
	public class FluidOnlySim : MonoBehaviour
	{
		public event Action<FluidOnlySim> SimulationInitCompleted;

		// UV
		[Header("Paint Texture")]
		public int paintTextureResolution = 512;
		public Material canvasMaterial;
		public Color paintColour = Color.blue; // colour this bucket's particles paint onto the canvas
		RenderTexture paintTexture;

		// MIXING
		RenderTexture paintAccumTexture;
		static RenderTexture sharedPaintAccumTexture; // shared across all buckets

		// STYLE: per-pixel wetness (R) and bump strength (G) baked at deposit time
		RenderTexture paintStyleTexture;
		static RenderTexture sharedPaintStyleTexture;

		// PAINT TYPE
		public enum PaintType { Watercolor, Acrylic, WallPaint }
		public PaintType paintType = PaintType.Watercolor;

		[Header("Time Step")]
		public float normalTimeScale = 1;
		public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
		public int iterationsPerFrame = 3;

		[Header("Main Fluid Settings")] public float gravity = -10;
		public float smoothingRadius = 0.2f;
		public float targetDensity = 630;
		public float pressureMultiplier = 288;
		public float nearPressureMultiplier = 2.15f;
		public float springStiffness = 0.3f;
		public float plasticityRate = 0.3f;
		public float yieldRatio = 0.1f;
		public float viscosityStrength = 0;
		public float weightPerParticle = 0.0001f;
		public struct Spring
		{
			public int neighborIndex;
			public float restLength;
		}


		[Range(0, 1)] public float collisionDamping = 0.95f;
		[Header("Hole Settings")]
		public Vector3 holePosition = Vector3.zero;
		public int holeOrientation = 0;
		[Range(0.0f, 0.5f)]
		public float holeSize = 0.1f;

		public enum SurfaceType
		{
			Glass,
			Plastic,
			Sponge,
			Wood,
			Canvas
		}

		public SurfaceType surfaceType;

		[Header("Environmental Settings")]
		[Range(-10f, 60f)] public float temperature = 25f;
		[Range(0f, 1f)] public float humidity = 0.5f;
		[Range(0f, 0.1f)] public float evaporationRate = 0.01f;


		[Header("Weight Tracking")]

		public int currentParticleCount;
		public float currentBucketWeight;

		private ComputeBuffer bucketCountBuffer;
		private int[] countResultData = new int[1];

		[Header("Flow Tracking")]
		public Vector3 sensorCenter;
		public Vector3 sensorExtents = new Vector3(0.5f, 0.1f, 0.5f);
		public float currentFlowSpeed;
		private ComputeBuffer flowResultBuffer;
		private int[] flowData = new int[4];
		[Header("References")]
		public Transform bucketTransform;
		public ComputeShader compute;
		public Spawner3D spawner;
		public CanvasCollisionData canvasCollision;
		public Vector3 Scale => transform.localScale;


		// Buffers
		public GraphicsBuffer positionBuffer { get; private set; }
		public ComputeBuffer velocityBuffer { get; private set; }
		public ComputeBuffer densityBuffer { get; private set; }
		public ComputeBuffer predictedPositionsBuffer;
		public ComputeBuffer debugBuffer { get; private set; }


		public ComputeBuffer stateBuffer;
		public ComputeBuffer sortTarget_stateBuffer;
		ComputeBuffer sortTarget_positionBuffer;
		ComputeBuffer sortTarget_velocityBuffer;
		ComputeBuffer sortTarget_predictedPositionsBuffer;

		// Spring and Mapping Buffers
		public ComputeBuffer springBuffer;
		ComputeBuffer sortTarget_springBuffer;
		ComputeBuffer originalToNewIndexBuffer;

		// Kernel IDs
		const int externalForcesKernel = 0;
		const int spatialHashKernel = 1;
		const int reorderKernel = 2;
		const int reorderSpringsKernel = 3;
		const int reorderCopybackKernel = 4;
		const int reorderSpringsCopybackKernel = 5;
		const int densityKernel = 6;
		const int pressureKernel = 7;
		const int viscosityKernel = 8;
		const int updatePositionsKernel = 9;
		const int updateSpringsKernel = 10;
		const int applySpringForcesKernel = 11;
		const int mapOriginalToNewKernel = 12;

		SpatialHash spatialHash;

		// State
		bool isPaused;
		bool pauseNextFrame;
		float smoothRadiusOld;
		float simTimer;
		bool inSlowMode;
		Spawner3D.SpawnData spawnData;
		Dictionary<ComputeBuffer, string> bufferNameLookup;

		// Tracks the last applied paint type / surface type so OnValidate only re-applies presets
		// when that specific field actually changed, not whenever any other Inspector field is edited.
		[NonSerialized] PaintType _lastAppliedPaintType = (PaintType)(-1);
		[NonSerialized] SurfaceType _lastAppliedSurfaceType = (SurfaceType)(-1);

		void OnValidate()
		{
			if (paintType != _lastAppliedPaintType)
			{
				_lastAppliedPaintType = paintType;
				ApplyPaintTypePreset();
			}
			if (surfaceType != _lastAppliedSurfaceType)
			{
				_lastAppliedSurfaceType = surfaceType;
				ApplySurfacePreset();
			}
		}

		void ApplyPaintTypePreset()
		{
			ApplyPaintTypeSettings();
		}


		public void ApplyPaintTypeSettings()
		{
			switch (paintType)
			{
				case PaintType.Watercolor:
					smoothingRadius = 0.2f;
					targetDensity = 2000f;
					pressureMultiplier = 90f;
					nearPressureMultiplier = 90f;
					viscosityStrength = 0.0f;
					break;
				case PaintType.WallPaint:
					smoothingRadius = 0.2f;
					targetDensity = 2000f;
					pressureMultiplier = 90f;
					nearPressureMultiplier = 90f;
					viscosityStrength = 0.0004f;
					break;
				case PaintType.Acrylic:
					smoothingRadius = 0.2f;
					targetDensity = 2000f;
					pressureMultiplier = 90f;
					nearPressureMultiplier = 90f;
					viscosityStrength = 0.0002f;
					break;
			}
		}

		// Public so runtime UI (which sets surfaceType via script and therefore never triggers
		// OnValidate) can apply the preset immediately instead of only on the next Editor edit.
		public void ApplySurfacePreset()
		{
			switch (surfaceType)
			{
				case SurfaceType.Glass:
					bounceOffset = 1.0f;
					roughnessOffset = 0.02f;
					absorptionOffset = 0.0f;
					break;

				case SurfaceType.Wood:
					bounceOffset = 0.55f;
					roughnessOffset = 0.45f;
					absorptionOffset = 0.1f;
					break;

				case SurfaceType.Sponge:
					bounceOffset = 0.15f;
					roughnessOffset = 0.9f;
					absorptionOffset = 1.0f;
					break;

				case SurfaceType.Plastic:
					bounceOffset = 0.95f;
					roughnessOffset = 0.15f;
					absorptionOffset = 0.0f;
					break;

				case SurfaceType.Canvas:
					bounceOffset = 0.35f;
					roughnessOffset = 0.65f;
					absorptionOffset = 0.45f;
					break;
			}
			_lastAppliedSurfaceType = surfaceType;
		}

		void Start()
		{
			isPaused = false;

			Initialize();
		}

		void Initialize()
		{
			compute = Instantiate(compute);

			spawnData = spawner.GetSpawnData();
			int numParticles = spawnData.points.Length;

			spatialHash = new SpatialHash(numParticles);

			// Create buffers
			positionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numParticles, sizeof(float) * 3);
			predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			velocityBuffer = CreateStructuredBuffer<float3>(numParticles);
			densityBuffer = CreateStructuredBuffer<float2>(numParticles);
			debugBuffer = CreateStructuredBuffer<float3>(numParticles);
			stateBuffer = CreateStructuredBuffer<int>(numParticles);
			sortTarget_stateBuffer = CreateStructuredBuffer<int>(numParticles);
			sortTarget_positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_velocityBuffer = CreateStructuredBuffer<float3>(numParticles);
			bucketCountBuffer = new ComputeBuffer(1, sizeof(int));
			flowResultBuffer = new ComputeBuffer(4, sizeof(int));
			// Spring Allocations (8 slots per particle)
			springBuffer = new ComputeBuffer(numParticles * 8, 8);
			sortTarget_springBuffer = new ComputeBuffer(numParticles * 8, 8);
			originalToNewIndexBuffer = CreateStructuredBuffer<uint>(numParticles);

			Spring[] initialSprings = new Spring[numParticles * 8];
			for (int i = 0; i < initialSprings.Length; i++)
			{
				initialSprings[i] = new Spring { neighborIndex = -1, restLength = 0 };
			}
			springBuffer.SetData(initialSprings);

			bufferNameLookup = new Dictionary<ComputeBuffer, string>
			{

				{ predictedPositionsBuffer, "PredictedPositions" },
				{ velocityBuffer, "Velocities" },
				{ densityBuffer, "Densities" },
				{ spatialHash.SpatialKeys, "SpatialKeys" },
				{ spatialHash.SpatialOffsets, "SpatialOffsets" },
				{ spatialHash.SpatialIndices, "SortedIndices" },
				{ sortTarget_positionBuffer, "SortTarget_Positions" },
				{ sortTarget_predictedPositionsBuffer, "SortTarget_PredictedPositions" },
				{ sortTarget_velocityBuffer, "SortTarget_Velocities" },
				{ debugBuffer, "Debug" },
				{ stateBuffer, "ParticleStates" },
				{ sortTarget_stateBuffer, "SortTarget_ParticleStates" },
				{ flowResultBuffer, "FlowResultBuffer" },
				{ springBuffer, "Springs" },
				{ sortTarget_springBuffer, "SortTarget_Springs" },
				{ originalToNewIndexBuffer, "OriginalToNewIndices" }
			};

			// Set buffer data
			SetInitialBufferData(spawnData);

			// External forces kernel
			SetBuffers(compute, externalForcesKernel, bufferNameLookup, new ComputeBuffer[]
	{
			predictedPositionsBuffer,
			velocityBuffer,
	});
			// Manually bind GraphicsBuffer for the shader graph
			compute.SetBuffer(externalForcesKernel, "Positions", positionBuffer);

			// Spatial hash kernel
			SetBuffers(compute, spatialHashKernel, bufferNameLookup, new ComputeBuffer[]
				{
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				predictedPositionsBuffer,
				spatialHash.SpatialIndices
				});
			// Map Kernel
			SetBuffers(compute, mapOriginalToNewKernel, bufferNameLookup, new ComputeBuffer[] { spatialHash.SpatialIndices, originalToNewIndexBuffer });

			// Reorder kernel
			SetBuffers(compute, reorderKernel, bufferNameLookup, new ComputeBuffer[]
			{
			sortTarget_positionBuffer,
			predictedPositionsBuffer,
			sortTarget_predictedPositionsBuffer,
			velocityBuffer,
			sortTarget_velocityBuffer,
			spatialHash.SpatialIndices,
			stateBuffer, sortTarget_stateBuffer
			});
			// Manually bind GraphicsBuffer for the shader graph
			compute.SetBuffer(reorderKernel, "Positions", positionBuffer);

			SetBuffers(compute, reorderSpringsKernel, bufferNameLookup, new ComputeBuffer[] {
				spatialHash.SpatialIndices, springBuffer, sortTarget_springBuffer, originalToNewIndexBuffer
			});

			// Reorder copyback kernel
			SetBuffers(compute, reorderCopybackKernel, bufferNameLookup, new ComputeBuffer[]
			{
			sortTarget_positionBuffer,
			predictedPositionsBuffer,
			sortTarget_predictedPositionsBuffer,
			velocityBuffer,
			sortTarget_velocityBuffer,
			spatialHash.SpatialIndices,
			stateBuffer, sortTarget_stateBuffer
			});
			// Manually bind GraphicsBuffer for the shader graph
			compute.SetBuffer(reorderCopybackKernel, "Positions", positionBuffer);

			SetBuffers(compute, reorderSpringsCopybackKernel, bufferNameLookup, new ComputeBuffer[] {
				springBuffer, sortTarget_springBuffer
			});

			// Density kernel
			SetBuffers(compute, densityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets
			});

			// Pressure kernel
			SetBuffers(compute, pressureKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				debugBuffer,
				stateBuffer
			});

			// Viscosity kernel
			SetBuffers(compute, viscosityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				stateBuffer
			});

			// Update positions kernel
			SetBuffers(compute, updatePositionsKernel, bufferNameLookup, new ComputeBuffer[]
			{
			velocityBuffer,
			stateBuffer
			});
			// Manually bind GraphicsBuffer for the shader grapgh
			compute.SetBuffer(updatePositionsKernel, "Positions", positionBuffer);


			int updatePosKernel = compute.FindKernel("UpdatePositions");
			compute.SetBuffer(updatePosKernel, "BucketParticleCount", bucketCountBuffer);

			SetBuffers(compute, updateSpringsKernel, bufferNameLookup, new ComputeBuffer[] { predictedPositionsBuffer, spatialHash.SpatialKeys, spatialHash.SpatialOffsets, springBuffer });
			SetBuffers(compute, applySpringForcesKernel, bufferNameLookup, new ComputeBuffer[] { predictedPositionsBuffer, velocityBuffer, springBuffer });


			compute.SetInt("numParticles", positionBuffer.count);

			UpdateSmoothingConstants();

			// UV
			SimulationInitCompleted?.Invoke(this);

			// CANVAS IS ALWAYS SHARED: if another bucket has already created a paint
			// texture on this canvasMaterial, reuse it instead of making a new one.
			// This is what makes multiple buckets splatter onto the same canvas
			// (different colours can overlap/mix). All buckets that should share a
			// canvas must be assigned the same canvasMaterial asset in the Inspector.
			if (canvasMaterial != null && canvasMaterial.mainTexture is RenderTexture existingPaintTexture)
			{
				paintTexture = existingPaintTexture;
			}
			else
			{
				paintTexture = new RenderTexture(paintTextureResolution, paintTextureResolution, 0, RenderTextureFormat.ARGB32);
				paintTexture.enableRandomWrite = true;
				paintTexture.Create();

				// Clear to white canvas (alpha=0 means no paint thickness yet)
				var prevRT = RenderTexture.active;
				RenderTexture.active = paintTexture;
				GL.Clear(false, true, new Color(1f, 1f, 1f, 0f));
				RenderTexture.active = prevRT;

				if (canvasMaterial != null)
					canvasMaterial.mainTexture = paintTexture;
			}

			compute.SetTexture(updatePositionsKernel, "PaintTexture", paintTexture);
			compute.SetInt("paintTextureSize", paintTextureResolution);

			// MIXING
			// Share accum texture across all buckets (same logic as paintTexture)
			if (sharedPaintAccumTexture != null && sharedPaintAccumTexture.IsCreated())
			{
				paintAccumTexture = sharedPaintAccumTexture;
			}
			else
			{
				paintAccumTexture = new RenderTexture(paintTextureResolution, paintTextureResolution, 0, RenderTextureFormat.ARGBFloat);
				paintAccumTexture.enableRandomWrite = true;
				paintAccumTexture.Create();
				sharedPaintAccumTexture = paintAccumTexture;
			}

			compute.SetTexture(updatePositionsKernel, "PaintAccumTexture", paintAccumTexture);

			// STYLE texture: per-pixel wetness/bump baked at deposit time
			if (sharedPaintStyleTexture != null && sharedPaintStyleTexture.IsCreated())
			{
				paintStyleTexture = sharedPaintStyleTexture;
			}
			else
			{
				paintStyleTexture = new RenderTexture(paintTextureResolution, paintTextureResolution, 0, RenderTextureFormat.RGFloat);
				paintStyleTexture.enableRandomWrite = true;
				paintStyleTexture.Create();
				sharedPaintStyleTexture = paintStyleTexture;
			}
			compute.SetTexture(updatePositionsKernel, "PaintStyleTexture", paintStyleTexture);
			if (canvasMaterial != null)
				canvasMaterial.SetTexture("_StyleTex", paintStyleTexture);

		}

		void Update()
		{
			// Run simulation
			if (!isPaused)
			{
				float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; // If framerate dips too low, run the simulation slower than real-time
				float dt = Mathf.Min(Time.deltaTime * normalTimeScale, maxDeltaTime);
				RunSimulationFrame(dt);
			}

			if (pauseNextFrame)
			{
				isPaused = true;
				pauseNextFrame = false;
			}


			Debug.Log($"Canvas normal: {canvasCollision.canvasTransform.up}, flatness: {Mathf.Abs(Vector3.Dot(canvasCollision.canvasTransform.up, Vector3.up))}");
		}
		// BANA
		// Resets all particles to fluid state at their original spawn positions.
		// Called by MixingSceneUI when clearing the canvas so in-air particles are also removed.
		public void ResetParticles()
		{
			if (spawnData.points == null || positionBuffer == null) return;
			var positions = System.Array.ConvertAll(spawnData.points, p => (Vector3)p);
			var velocities = new Vector3[positions.Length];
			var states = new int[positions.Length]; // all 0 = fluid
			positionBuffer.SetData(positions);
			velocityBuffer.SetData(velocities);
			stateBuffer.SetData(states);
		}

		void RunSimulationFrame(float frameDeltaTime)
		{
			float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
			UpdateSettings(subStepDeltaTime, frameDeltaTime);

			flowData[0] = 0; flowData[1] = 0; flowData[2] = 0; flowData[3] = 0;
			flowResultBuffer.SetData(flowData);
			// Simulation sub-steps
			for (int i = 0; i < iterationsPerFrame; i++)
			{
				simTimer += subStepDeltaTime;
				RunSimulationStep();


			}
			bucketCountBuffer.GetData(countResultData);
			currentParticleCount = countResultData[0];

			currentBucketWeight = currentParticleCount * weightPerParticle;

			flowResultBuffer.GetData(flowData);
			if (flowData[0] > 0) // count > 0
			{
				Vector3 sumVel = new Vector3(flowData[1], flowData[2], flowData[3]) / 1000f;
				currentFlowSpeed = (sumVel / flowData[0]).magnitude;
			}
			else
			{
				currentFlowSpeed = 0f;
			}


		}



		void RunSimulationStep()
		{
			Dispatch(compute, positionBuffer.count, kernelIndex: externalForcesKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);
			spatialHash.Run();

			Dispatch(compute, positionBuffer.count, kernelIndex: mapOriginalToNewKernel);


			Dispatch(compute, positionBuffer.count, kernelIndex: reorderKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderSpringsKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderCopybackKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderSpringsCopybackKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: updateSpringsKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
			if (viscosityStrength != 0) Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
			countResultData[0] = 0;
			bucketCountBuffer.SetData(countResultData);
			Dispatch(compute, positionBuffer.count, kernelIndex: applySpringForcesKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: updatePositionsKernel);
		}

		void UpdateSmoothingConstants()
		{
			float r = smoothingRadius;
			float spikyPow2 = 15 / (2 * Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3 = 15 / (Mathf.PI * Mathf.Pow(r, 6));
			float spikyPow2Grad = 15 / (Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3Grad = 45 / (Mathf.PI * Mathf.Pow(r, 6));

			compute.SetFloat("K_SpikyPow2", spikyPow2);
			compute.SetFloat("K_SpikyPow3", spikyPow3);
			compute.SetFloat("K_SpikyPow2Grad", spikyPow2Grad);
			compute.SetFloat("K_SpikyPow3Grad", spikyPow3Grad);
		}


		[Header("Surface Tuning (Overrides Preset)")]
		[Range(0f, 1f)] public float bounceOffset = 0.5f;
		[Range(0f, 1f)] public float roughnessOffset = 0.5f;
		[Range(0f, 1f)] public float absorptionOffset = 0.5f;

		Vector3 GetSurfaceParams()
		{
			return new Vector3(
				bounceOffset,
				roughnessOffset,
				absorptionOffset
			);
		}



		void SetPaintTypeParams()
		{
			// Single source of truth: everything derives from viscosityStrength.
			// viscNorm: 0=watercolor (visc=0), 0.333=acrylic (visc=0.0002), 0.5=wallpaint (visc=0.0004).
			// Asymptotic — approaches 1.0 as viscosity → ∞, no hard ceiling.
			float viscNorm = viscosityStrength / (viscosityStrength + 0.0004f);

			// OLD: hardcoded switch — watercolor=0/0, acrylic=0.4/0.5, wallpaint=1/1
			float paintViscosity = Mathf.Lerp(0.0f, 2.0f, viscNorm); // wallpaint→1.0, beyond→up to 2
			float paintDensityFactor = Mathf.Lerp(0.0f, 2.0f, viscNorm);
			compute.SetFloat("paintViscosity", paintViscosity);
			compute.SetFloat("paintDensityFactor", paintDensityFactor);

			// mixRate and wetnessGloss are strictly binary: watercolor (viscosity==0) vs everything else.
			// Any positive viscosity means a non-mixing paint — no partial rates.
			bool isWatercolor = viscosityStrength <= 0f;
			compute.SetFloat("paintMixRate", isWatercolor ? 1.0f : 0.0f);
			compute.SetFloat("paintWetnessGloss", isWatercolor ? 0.4f : 0.0f);

			// flowRate: exponential decay — watercolor(0)→1.0, wallpaint(0.5)→0.05, beyond→<0.05
			// OLD: hardcoded watercolor=1.0, acrylic=0.3, wallpaint=0.05
			float flowRate = Mathf.Pow(0.2f, viscNorm * 2.0f);
			compute.SetFloat("paintFlowRate", flowRate);

			// Bump baked per-pixel into PaintStyleTexture at deposit time (wetness set above, binary).
			compute.SetFloat("paintBumpStrength", Mathf.Lerp(1.5f, 5.5f, viscNorm));

			// absorptionResistance: watercolor(0)→0.0, wallpaint(0.5)→0.9, beyond→up to 1.8
			// OLD: hardcoded watercolor=0.0, acrylic=0.5, wallpaint=0.9
			float paintAbsorptionResistance = Mathf.Lerp(0.0f, 1.8f, viscNorm);
			compute.SetFloat("paintAbsorptionResistance", paintAbsorptionResistance);

			// spread: exponential decay — watercolor(0)→1.0, wallpaint(0.5)→0.3, beyond→<0.3
			// OLD: hardcoded watercolor=1.0, acrylic=0.6, wallpaint=0.3
			float paintSpread = Mathf.Pow(0.3f, viscNorm * 2.0f);
			compute.SetFloat("paintSpread", paintSpread);

			// thickness: watercolor(0)→0.012, wallpaint(0.5)→0.110, beyond→up to 0.208
			// OLD: hardcoded watercolor=0.012, acrylic=0.050, wallpaint=0.110
			float paintLayerThickness = Mathf.Lerp(0.012f, 0.208f, viscNorm);
			compute.SetFloat("paintLayerThickness", paintLayerThickness);
		}

		void UpdateSettings(float stepDeltaTime, float frameDeltaTime)
		{
			if (smoothingRadius != smoothRadiusOld)
			{
				smoothRadiusOld = smoothingRadius;
				UpdateSmoothingConstants();
			}

			Vector3 simBoundsSize = bucketTransform.localScale;
			Vector3 simBoundsCentre = bucketTransform.position;

			// =========================
			// CORE SIM SETTINGS
			// =========================
			compute.SetFloat("deltaTime", stepDeltaTime);
			compute.SetFloat("whiteParticleDeltaTime", frameDeltaTime);
			compute.SetFloat("simTime", simTimer);

			compute.SetFloat("gravity", gravity);
			compute.SetFloat("collisionDamping", collisionDamping);

			compute.SetFloat("smoothingRadius", smoothingRadius);
			compute.SetFloat("targetDensity", targetDensity);
			compute.SetFloat("pressureMultiplier", pressureMultiplier);
			compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
			compute.SetFloat("viscosityStrength", viscosityStrength);
			compute.SetFloat("springStiffness", springStiffness);
			compute.SetFloat("plasticityRate", plasticityRate);
			compute.SetFloat("yieldRatio", yieldRatio);

			compute.SetVector("boundsSize", simBoundsSize);
			compute.SetVector("centre", simBoundsCentre);
			compute.SetFloat("holeSize", holeSize);

			// Environmental settings
			compute.SetFloat("temperature", temperature);
			compute.SetFloat("humidity", humidity);
			compute.SetFloat("evaporationRate", evaporationRate);


			compute.SetVector("holePosition", holePosition);
			compute.SetInt("holeOrientation", holeOrientation);

			compute.SetVector("sensorCenter", bucketTransform.TransformPoint(holePosition)); // Follows the hole
			compute.SetVector("sensorExtents", sensorExtents);
			compute.SetBuffer(updatePositionsKernel, "FlowResultBuffer", flowResultBuffer);

			// CANVAS
			if (canvasCollision != null)
			{
				canvasCollision.SetShaderParams(compute);
			}

			// =========================
			// SURFACE RESPONSE (IMPORTANT PART)
			// =========================
			Vector3 surface = GetSurfaceParams();
			// x = bounce
			// y = rough
			// z = absorb

			compute.SetVector("surfaceParams", surface);
			Debug.Log(surfaceType + " -> " + surface);
			// =========================
			// MATRIX TRANSFORMS
			// =========================
			compute.SetMatrix("localToWorld", bucketTransform.localToWorldMatrix);
			compute.SetMatrix("worldToLocal", bucketTransform.worldToLocalMatrix);

			// =========================
			// PAINT TEXTURE
			// =========================
			if (paintTexture != null)
				compute.SetTexture(updatePositionsKernel, "PaintTexture", paintTexture);

			compute.SetVector("paintColour", paintColour);

			// PAINT TYPE
			SetPaintTypeParams();

			// MIXING
			if (paintAccumTexture != null)
				compute.SetTexture(updatePositionsKernel, "PaintAccumTexture", paintAccumTexture);

			// STYLE: rebind every frame so the compute shader always has it
			if (paintStyleTexture != null)
				compute.SetTexture(updatePositionsKernel, "PaintStyleTexture", paintStyleTexture);
		}

		void SetInitialBufferData(Spawner3D.SpawnData spawnData)
		{
			positionBuffer.SetData(spawnData.points);
			predictedPositionsBuffer.SetData(spawnData.points);
			debugBuffer.SetData(new float3[debugBuffer.count]);
			stateBuffer.SetData(new int[positionBuffer.count]);
			simTimer = 0;
			bucketCountBuffer.SetData(countResultData);
		}

		void OnDestroy()
		{
			if (bufferNameLookup != null)
			{
				foreach (var kvp in bufferNameLookup)
				{
					Release(kvp.Key);
				}
			}

			if (positionBuffer != null) positionBuffer.Release();

			if (bucketCountBuffer != null) bucketCountBuffer.Release();

			if (flowResultBuffer != null) flowResultBuffer.Release();

			if (spatialHash != null) spatialHash.Release();
		}

		void OnDrawGizmos()
		{
			if (bucketTransform == null) return;

			// Local-space matrix so the sphere reflects the bucket's own rotation/scale,
			// matching how the compute shader tests holePosition against posLocal.
			Gizmos.matrix = bucketTransform.localToWorldMatrix;
			Gizmos.color = new Color(1f, 0.3f, 0f, 0.8f);
			Gizmos.DrawWireSphere(holePosition, Mathf.Max(holeSize, 0.01f));

			Vector3 drainDir = holeOrientation == 0
				? Vector3.down
				: new Vector3(holePosition.x, 0f, holePosition.z).normalized;
			Gizmos.DrawLine(holePosition, holePosition + drainDir * 0.3f);

			// World-space marker: line up this dot between two buckets to make their holes drain to the same spot.
			Gizmos.matrix = Matrix4x4.identity;
			Gizmos.color = Color.yellow;
			Gizmos.DrawSphere(bucketTransform.TransformPoint(holePosition), 0.02f);
		}


	}
}
