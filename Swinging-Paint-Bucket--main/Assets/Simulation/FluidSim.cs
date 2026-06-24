using System;
using UnityEngine;
using Seb.GPUSorting;
using Unity.Mathematics;
using System.Collections.Generic;
using Seb.Helpers;
using static Seb.Helpers.ComputeHelper;

namespace Seb.Fluid.Simulation
{
	public class FluidSim : MonoBehaviour
	{
		public event Action<FluidSim> SimulationInitCompleted;
        [Header("Moving Container References")]
         public Transform bucketTransform;

        // CANVAS
        [Header("Canvas Collision")]
        public CanvasCollisionData canvasCollision;
		// UV
		[Header("Paint Texture")]
		public int paintTextureResolution = 512;
		public Material canvasMaterial;
		public Color paintColour = Color.blue; // colour this bucket's particles paint onto the canvas
		RenderTexture paintTexture;

		[Header("Time Step")] public float normalTimeScale = 1;
		public float slowTimeScale = 0.1f;
		public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
		public int iterationsPerFrame = 3;

		[Header("Simulation Settings")] public float gravity = -10;
		public float smoothingRadius = 0.2f;
		public float targetDensity = 630;
		public float pressureMultiplier = 288;
		public float nearPressureMultiplier = 2.15f;
		public float viscosityStrength = 0;
		public float holeSize = 0.1f;
		public Transform floorTransform; // This will hold the actual Floor object
		[Range(0, 1)] public float collisionDamping = 0.95f;

		[Header("Environmental Settings")]
        [Range(-10f, 60f)] public float temperature = 25f;
        [Range(0f, 1f)]    public float humidity = 0.5f;
        [Range(0f, 0.1f)]  public float evaporationRate = 0.01f;
        [Range(0.0f, 0.3f)]  public float viscosityBase = 0.05f;

		[Header("Foam Settings")] public bool foamActive;
		public int maxFoamParticleCount = 1000;
		public float trappedAirSpawnRate = 70;
		public float spawnRateFadeInTime = 0.5f;
		public float spawnRateFadeStartTime = 0;
		public Vector2 trappedAirVelocityMinMax = new(5, 25);
		public Vector2 foamKineticEnergyMinMax = new(15, 80);
		public float bubbleBuoyancy = 1.5f;
		public int sprayClassifyMaxNeighbours = 5;
		public int bubbleClassifyMinNeighbours = 15;
		public float bubbleScale = 0.5f;
		public float bubbleChangeScaleSpeed = 7;

		

		[Header("Volumetric Render Settings")] public bool renderToTex3D;
		public int densityTextureRes;

		[Header("References")] public ComputeShader compute;
		public Spawner3D spawner;

		[HideInInspector] public RenderTexture DensityMap;
		public Vector3 Scale => transform.localScale;

		// Buffers
		public ComputeBuffer foamBuffer { get; private set; }
		public ComputeBuffer foamSortTargetBuffer { get; private set; }
		public ComputeBuffer foamCountBuffer { get; private set; }
		public ComputeBuffer positionBuffer { get; private set; }
		public ComputeBuffer velocityBuffer { get; private set; }
		public ComputeBuffer densityBuffer { get; private set; }
		public ComputeBuffer predictedPositionsBuffer;
		public ComputeBuffer debugBuffer { get; private set; }


		public ComputeBuffer stateBuffer;
		public ComputeBuffer sortTarget_stateBuffer; 
		ComputeBuffer sortTarget_positionBuffer;
		ComputeBuffer sortTarget_velocityBuffer;
		ComputeBuffer sortTarget_predictedPositionsBuffer;

		// Kernel IDs
		const int externalForcesKernel = 0;
		const int spatialHashKernel = 1;
		const int reorderKernel = 2;
		const int reorderCopybackKernel = 3;
		const int densityKernel = 4;
		const int pressureKernel = 5;
		const int viscosityKernel = 6;
		const int updatePositionsKernel = 7;
		const int renderKernel = 8;
		const int foamUpdateKernel = 9;
		const int foamReorderCopyBackKernel = 10;

		SpatialHash spatialHash;

		// State
		bool isPaused;
		bool pauseNextFrame;
		float smoothRadiusOld;
		float simTimer;
		bool inSlowMode;
		Spawner3D.SpawnData spawnData;
		Dictionary<ComputeBuffer, string> bufferNameLookup;

		void Start()
		{
			Debug.Log("Controls: Space = Play/Pause, Q = SlowMode, R = Reset");
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
			positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			velocityBuffer = CreateStructuredBuffer<float3>(numParticles);
			densityBuffer = CreateStructuredBuffer<float2>(numParticles);
			foamBuffer = CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamSortTargetBuffer = CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamCountBuffer = CreateStructuredBuffer<uint>(4096);
			debugBuffer = CreateStructuredBuffer<float3>(numParticles);
			stateBuffer = CreateStructuredBuffer<int>(numParticles);
			sortTarget_stateBuffer = CreateStructuredBuffer<int>(numParticles);
			sortTarget_positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_velocityBuffer = CreateStructuredBuffer<float3>(numParticles);

			bufferNameLookup = new Dictionary<ComputeBuffer, string>
			{
				{ positionBuffer, "Positions" },
				{ predictedPositionsBuffer, "PredictedPositions" },
				{ velocityBuffer, "Velocities" },
				{ densityBuffer, "Densities" },
				{ spatialHash.SpatialKeys, "SpatialKeys" },
				{ spatialHash.SpatialOffsets, "SpatialOffsets" },
				{ spatialHash.SpatialIndices, "SortedIndices" },
				{ sortTarget_positionBuffer, "SortTarget_Positions" },
				{ sortTarget_predictedPositionsBuffer, "SortTarget_PredictedPositions" },
				{ sortTarget_velocityBuffer, "SortTarget_Velocities" },
				{ foamCountBuffer, "WhiteParticleCounters" },
				{ foamBuffer, "WhiteParticles" },
				{ foamSortTargetBuffer, "WhiteParticlesCompacted" },
				{ debugBuffer, "Debug" },
				{ stateBuffer, "ParticleStates" },
				{ sortTarget_stateBuffer, "SortTarget_ParticleStates" }
			};

			// Set buffer data
			SetInitialBufferData(spawnData);

			// External forces kernel
			SetBuffers(compute, externalForcesKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictedPositionsBuffer,
				velocityBuffer,
				
			});

			// Spatial hash kernel
			SetBuffers(compute, spatialHashKernel, bufferNameLookup, new ComputeBuffer[]
			{
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				predictedPositionsBuffer,
				spatialHash.SpatialIndices
			});

			// Reorder kernel
			SetBuffers(compute, reorderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				spatialHash.SpatialIndices,
				stateBuffer, sortTarget_stateBuffer
			});

			// Reorder copyback kernel
			SetBuffers(compute, reorderCopybackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				spatialHash.SpatialIndices,
				stateBuffer, sortTarget_stateBuffer
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
				foamBuffer,
				foamCountBuffer,
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
				spatialHash.SpatialOffsets
			});

			// Update positions kernel
			SetBuffers(compute, updatePositionsKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				velocityBuffer,
				stateBuffer
			});

			// Render to 3d tex kernel
			SetBuffers(compute, renderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
			});

			// Foam update kernel
			SetBuffers(compute, foamUpdateKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamCountBuffer,
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				foamSortTargetBuffer,
				//debugBuffer
			});


			// Foam reorder copyback kernel
			SetBuffers(compute, foamReorderCopyBackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamSortTargetBuffer,
				foamCountBuffer,
			});

			compute.SetInt("numParticles", positionBuffer.count);
			compute.SetInt("MaxWhiteParticleCount", maxFoamParticleCount);

			UpdateSmoothingConstants();

            if (renderToTex3D)
			{
				RunSimulationFrame(0);
			}
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

                if (canvasMaterial != null)
                    canvasMaterial.mainTexture = paintTexture;
            }

            compute.SetTexture(updatePositionsKernel, "PaintTexture", paintTexture);
            compute.SetInt("paintTextureSize", paintTextureResolution);
        }

		void Update()
		{
			// Run simulation
			if (!isPaused)
			{
				float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; // If framerate dips too low, run the simulation slower than real-time
				float dt = Mathf.Min(Time.deltaTime * ActiveTimeScale, maxDeltaTime);
				RunSimulationFrame(dt);
			}

			if (pauseNextFrame)
			{
				isPaused = true;
				pauseNextFrame = false;
			}

			HandleInput();
		}

		void RunSimulationFrame(float frameDeltaTime)
		{
			float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
			UpdateSettings(subStepDeltaTime, frameDeltaTime);

			// Simulation sub-steps
			for (int i = 0; i < iterationsPerFrame; i++)
			{
				simTimer += subStepDeltaTime;
				RunSimulationStep();
			}

			// Foam and spray particles
			if (foamActive)
			{
				Dispatch(compute, maxFoamParticleCount, kernelIndex: foamUpdateKernel);
				Dispatch(compute, maxFoamParticleCount, kernelIndex: foamReorderCopyBackKernel);
			}

			// 3D density map
			if (renderToTex3D)
			{
				UpdateDensityMap();
			}
		}

		void UpdateDensityMap()
		{
			float maxAxis = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			int w = Mathf.RoundToInt(transform.localScale.x / maxAxis * densityTextureRes);
			int h = Mathf.RoundToInt(transform.localScale.y / maxAxis * densityTextureRes);
			int d = Mathf.RoundToInt(transform.localScale.z / maxAxis * densityTextureRes);
			CreateRenderTexture3D(ref DensityMap, w, h, d, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat, TextureWrapMode.Clamp);
			//Debug.Log(w + " " + h + "  " + d);
			compute.SetTexture(renderKernel, "DensityMap", DensityMap);
			compute.SetInts("densityMapSize", DensityMap.width, DensityMap.height, DensityMap.volumeDepth);
		
			Dispatch(compute, DensityMap.width, DensityMap.height, DensityMap.volumeDepth, renderKernel);
		}

		void RunSimulationStep()
		{
			Dispatch(compute, positionBuffer.count, kernelIndex: externalForcesKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);
			spatialHash.Run();
			
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderCopybackKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
			if (viscosityBase != 0) Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
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
        public enum SurfaceType
        {
            Glass,
            Plastic,
            Sponge,
            Wood,
            Canvas
        }

        public SurfaceType surfaceType;

        Vector3 GetSurfaceParams()
        {
            switch (surfaceType)
            {
                case SurfaceType.Glass:
                    return new Vector3(
                        1.4f,   // bounce عالي
                        0.02f,  // rough قليل
                        0.0f    // لا امتصاص
                    );

                case SurfaceType.Wood:
                    return new Vector3(
                        0.55f,
                        0.45f,
                        0.1f
                    );

                case SurfaceType.Sponge:
                    return new Vector3(
                        0.15f,
                        0.9f,
                        1.0f
                    );

                case SurfaceType.Plastic:
                    return new Vector3(
                        0.95f,
                        0.15f,
                        0.0f
                    );

                case SurfaceType.Canvas:
                    return new Vector3(0.35f, 0.65f, 0.45f);
                    

            }

            return Vector3.zero;
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

            compute.SetVector("boundsSize", simBoundsSize);
            compute.SetVector("centre", simBoundsCentre);
            compute.SetFloat("holeSize", holeSize);

			//environmental settings
			compute.SetFloat("temperature",              temperature);
            compute.SetFloat("humidity",                 humidity);
            compute.SetFloat("airDensity",               1.225f);
            compute.SetFloat("airDragCoefficient",       0.47f);
            compute.SetFloat("evaporationRate",          evaporationRate);
            compute.SetFloat("viscosityBase",            viscosityBase);
            compute.SetFloat("viscosityTempCoeff",       0.05f);
            compute.SetFloat("surfaceTensionBase",       0.072f);
            compute.SetFloat("surfaceTensionTempCoeff",  0.0001f);

            // =========================
            // CANVAS (IMPORTANT FIX)
            // =========================
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

            // =========================
            // FOAM SETTINGS
            // =========================
            float fadeInT = (spawnRateFadeInTime <= 0)
                ? 1
                : Mathf.Clamp01((simTimer - spawnRateFadeStartTime) / spawnRateFadeInTime);

            compute.SetVector("trappedAirParams",
                new Vector3(trappedAirSpawnRate * fadeInT * fadeInT,
                trappedAirVelocityMinMax.x,
                trappedAirVelocityMinMax.y));

            compute.SetVector("kineticEnergyParams", foamKineticEnergyMinMax);
            compute.SetFloat("bubbleBuoyancy", bubbleBuoyancy);
            compute.SetInt("sprayClassifyMaxNeighbours", sprayClassifyMaxNeighbours);
            compute.SetInt("bubbleClassifyMinNeighbours", bubbleClassifyMinNeighbours);
            compute.SetFloat("bubbleScaleChangeSpeed", bubbleChangeScaleSpeed);
            compute.SetFloat("bubbleScale", bubbleScale);
        }

        void SetInitialBufferData(Spawner3D.SpawnData spawnData)
		{
			positionBuffer.SetData(spawnData.points);
			predictedPositionsBuffer.SetData(spawnData.points);
			velocityBuffer.SetData(spawnData.velocities);

			foamBuffer.SetData(new FoamParticle[foamBuffer.count]);

			debugBuffer.SetData(new float3[debugBuffer.count]);
			foamCountBuffer.SetData(new uint[foamCountBuffer.count]);
			stateBuffer.SetData(new int[positionBuffer.count]);
			simTimer = 0;
		}

		void HandleInput()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				isPaused = !isPaused;
			}

			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				isPaused = false;
				pauseNextFrame = true;
			}

			if (Input.GetKeyDown(KeyCode.R))
			{
				pauseNextFrame = true;
				SetInitialBufferData(spawnData);
				// Run single frame of sim with deltaTime = 0 to initialize density texture
				// (so that display can work even if paused at start)
				if (renderToTex3D)
				{
					RunSimulationFrame(0);
				}
			}

			if (Input.GetKeyDown(KeyCode.Q))
			{
				inSlowMode = !inSlowMode;
			}
		}

		private float ActiveTimeScale => inSlowMode ? slowTimeScale : normalTimeScale;

		void OnDestroy()
		{
			foreach (var kvp in bufferNameLookup)
			{
				Release(kvp.Key);
			}

			spatialHash.Release();
		}


		public struct FoamParticle
		{
			public float3 position;
			public float3 velocity;
			public float lifetime;
			public float scale;
		}



		
	}
}