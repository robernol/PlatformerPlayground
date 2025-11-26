using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// TODO:
/// - animate menu transitions
/// - documentation, commentation
/// - animation sqush&stretch
/// - jump tuning: gravity up vs down?
/// - foot particles

/// - level select
/// - get the colliders in the scene without allocating garbage... we may need to do a series of overlap test that cover the whole environment
/// - kinematics: more gradual lift up to desired height instead of instant snapping

namespace DevDev.LDP
{
    public static partial class Procs
    {
        public static void
        Main_Initialize(ref Game game, ref GameParams gameParams)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.runInBackground = true;
            Application.targetFrameRate = 0;
            //QualitySettings.vSyncCount = 0;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.autoSyncTransforms = false;

            // Timing
            gameParams.dynamicRate_minTimeStep = 1f / gameParams.frameRateMax;
            gameParams.dynamicRate_maxTimeStep = 1f / gameParams.frameRateMin;
            gameParams.fixedRate_maxTimeAccum = gameParams.dynamicRate_maxTimeStep;
            gameParams.fixedRate_timeStep = 1f / gameParams.fixedTickRate;

#if UNITY_EDITOR
            NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
#endif
            game.resetEvent = new System.Threading.AutoResetEvent(false);

            // Create or find ui main object
            UI.UIMain_mono uiMono = GameObject.FindAnyObjectByType<UI.UIMain_mono>();
            if (uiMono != null)
                game.uiMain = uiMono.data;
            else
                game.uiMain = GameObject.Instantiate(gameParams.assetReferences.mainScreenUI).data;

            #region // Simulation & environment
            game.simulation = new Simulation()
            {
                random = new Unity.Mathematics.Random(seed: 1851936439),
                //pawnInputSamples = new NativeArray<Input.Sample_Pawn>(gameParams.maxPawns, Allocator.Persistent),
            };

            // Look for arena in loaded scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Simulation_Environment arena = SimEnvironment_GetInSceneAndInitialize(SceneManager.GetSceneAt(i), gameParams);
                if (arena != null)
                {
                    game.simulation.environment = arena;
                    game.simulation.metaState = Simulation.MetaState.LOADED;
                    break;
                }
            }

            if (game.simulation.environment == null)
            {
                LoadSimEnvironment(ref game, gameParams);
            }
            #endregion // Create table game

            #region // Input
            {
                game.actionMaps = new Input.InputActions();
                game.actionMaps.Enable();
            }
            #endregion // Input

            game.audioMixer = gameParams.audioMixer;
            game.audioSnapshot_clean = game.audioMixer.FindSnapshot("clean");
            game.audioSnapshot_faded = game.audioMixer.FindSnapshot("faded");

            game.initialized = true;
        }

        static void
        InitializeUI(ref UI.UIMain ui, Game game, GameParams gameParams)
        {
            ui.inputActions = new Queue<UI.Action>();

            // performance graph
            {
                UI.BarGraph graph = ui.screen_debug.data.frameTimeGraph.data;
                graph.samples = new float[graph.nSamples];
                graph.propID_samples = Shader.PropertyToID("GraphValues");
                graph.propID_highValue = Shader.PropertyToID("_HighValue");
                graph.propID_cautionThreshold = Shader.PropertyToID("_CautionThreshold");
                graph.propID_goodThreshold = Shader.PropertyToID("_GoodThreshold");
                graph.image.material = UnityEngine.Object.Instantiate<UnityEngine.Material>(graph.image.material);
                graph.image.material.SetInt("GraphValues_Length", graph.samples.Length);
                graph.image.material.SetFloat(graph.propID_cautionThreshold, lerp(gameParams.dynamicRate_maxTimeStep, gameParams.dynamicRate_minTimeStep, 0.75f));
                graph.image.material.SetFloat(graph.propID_goodThreshold, gameParams.dynamicRate_minTimeStep);
                graph.image.material.SetFloat(graph.propID_highValue, gameParams.dynamicRate_maxTimeStep * 0.5f);


            }

            // Start and pause screen
            {
                // Initialize volume sliders to reflect mixer settings
                game.audioMixer.GetFloat("vol_sfx", out float vol);
                ui.screen_pause.data.sfxVolumeSlider.SetValueWithoutNotify(DbToNormalized(vol));
                ui.screen_levelStart.data.sfxVolumeSlider.SetValueWithoutNotify(DbToNormalized(vol));
                game.audioMixer.GetFloat("vol_music", out vol);
                ui.screen_pause.data.musicVolumeSlider.SetValueWithoutNotify(DbToNormalized(vol));
                ui.screen_levelStart.data.musicVolumeSlider.SetValueWithoutNotify(DbToNormalized(vol));
            }

            ui.screen_levelStart.data.rootGameObject.SetActive(true);
            ui.eventSystem.SetSelectedGameObject(ui.screen_levelStart.data.defaultSelection);
            ui.screen_pause.data.rootGameObject.SetActive(false);
            ui.screen_levelComplete.data.rootGameObject.SetActive(false);
            ui.screen_hud.data.rootGameObject.SetActive(true);
            ui.initialized = true;
        }

        static void
        LoadSimEnvironment(ref Game game, GameParams gameParams)
        {
            if (game.simulation.environment == null)
            {
                // Load first environment in list.
                int buildIndex = gameParams.sceneSet.data.level_buildIndex;
                game.environmentLoadJob = new SceneLoadJob
                {
                    sceneBuildIndex = buildIndex,
                    asyncOp = SceneManager.LoadSceneAsync(buildIndex, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.None }),
                };
                game.environmentLoadJob.asyncOp.allowSceneActivation = true;
                game.simulation.metaState = Simulation.MetaState.LOADING;
            }
        }

        public static void
        Main_Update(ref Game game, ref GameParams gameParams)
        {
            // Determine time delta for this frame
            #region // Time
            /// TODO(stef) :: We are still getting minor frametime inconsistencies (as reported by msi afterburner) when we move the character. 
            ///                 Maybe we need some wiggle room somewhere in here?
            ///                 In most cases we appear to be getting a fast frame
            float deltaTime;
            {
                // Wait until time since last frame has been at least one sim tick. This creates a framerate limit
                Int64 minTicks = (int)(gameParams.dynamicRate_minTimeStep * Stopwatch.Frequency);
                Int64 desiredStartTick = game.ticks_startOfPreviousFrame + minTicks;
                Int64 nTicksToWait = desiredStartTick - Stopwatch.GetTimestamp();
                Int64 startOfThisFrameTick;
                if (nTicksToWait > 0)
                {
                    /// TODO(stef) :: we may need to set system timer granularity, or at least query it, and round our ms down so it does not surpass that
                    ///                 I'm not sure if this method actually gives us 1ms granularity or not
                    // sleep for as many milliseconds as we can to get close to our desired start time, but not to go past it                   
                    game.resetEvent.WaitOne((int)floor(nTicksToWait / Stopwatch.Frequency));

                    // burn cycles for remainder of the wait time
                    do
                    {
                        startOfThisFrameTick = Stopwatch.GetTimestamp();
                    }
                    while (startOfThisFrameTick < desiredStartTick);
                }
                else
                {
                    startOfThisFrameTick = Stopwatch.GetTimestamp();
                    //Debug.Log($"Missed frametime by: {(startOfThisFrameTick - desiredStartTick) * (float)(1000.0 / Stopwatch.Frequency)}ms");
                }

                //Debug.Log($"delta: {ticks_startOfFrame - desiredStartTick}, wanted: {desiredStartTick}, got: {ticks_startOfFrame}");
                deltaTime = (startOfThisFrameTick - game.ticks_startOfPreviousFrame) * (float)(1.0 / Stopwatch.Frequency);
                game.ticks_startOfPreviousFrame = startOfThisFrameTick;


                // Perf graph                
                if (game.initialized && game.uiMain.screen_debug.data.frameTimeGraph.data.rootGameObject.activeInHierarchy)
                {
                    UI.BarGraph graph = game.uiMain.screen_debug.data.frameTimeGraph.data;
                    graph.samples_lastWritten = (graph.samples_lastWritten + 1) % graph.samples.Length;
                    graph.samples[graph.samples_lastWritten] = deltaTime;
                }
                deltaTime = math.min(deltaTime, gameParams.dynamicRate_maxTimeStep);
            }
            #endregion

            // Initialization happens once upon the first update call. UI initialization happens on the following frame
            #region // Initialization
            if (!game.initialized)
            {
                Main_Initialize(ref game, ref gameParams);
                deltaTime = 0;
            }
            else if (!game.uiMain.initialized)
            {
                // We wait for one frame after instantiation before initializing UI to avoid some unity-side bug(s)
                InitializeUI(ref game.uiMain, game, gameParams);
            }
            #endregion

            // Detect completion of an environment loading job
            #region // Environment load completion
            if (game.environmentLoadJob.asyncOp != null && game.environmentLoadJob.asyncOp.isDone)
            {
                Scene newScene = SceneManager.GetSceneByBuildIndex(game.environmentLoadJob.sceneBuildIndex);
                game.simulation.environment = SimEnvironment_GetInSceneAndInitialize(newScene, gameParams);
                game.environmentLoadJob = default(SceneLoadJob);
                SceneManager.SetActiveScene(newScene);
                if (game.simulation.metaState == Simulation.MetaState.LOADING)
                    game.simulation.metaState = Simulation.MetaState.LOADED;
            }
            #endregion // Arena load completion

            Input.Sample_General userInput_general = default;
            
            // Act on inputs recieved from the UI. These events are enqueued by scripts on the UI buttons/sliders
            #region // Consume UI actions
            if (game.uiMain.initialized)
            {
                while (game.uiMain.inputActions.Count > 0)
                {
                    UI.Action action = game.uiMain.inputActions.Dequeue();
                    switch (action.type)
                    {
                        case UI.ActionType.START:
                            // Start simulating 
                            if (game.initialized && game.simulation.metaState == Simulation.MetaState.LOADED)
                            {
                                game.simulation.metaState = Simulation.MetaState.SIMULATING;
                                game.uiMain.screen_levelStart.data.rootGameObject.SetActive(false);
                            }
                            break;
                        case UI.ActionType.RESUME:
                            userInput_general.pause = true;
                            break;
                        case UI.ActionType.RESET:
                            if (game.simulation.metaState == Simulation.MetaState.SIMULATING && game.simulation.simmedOnce)
                            {

                                // NOTE :: We're assuming we dont really need to wait for the scene to unload before proceeding
                                AsyncOperation op = SceneManager.UnloadSceneAsync(game.simulation.environment.rootGameObject.scene);
                                game.simulation = new Simulation()
                                {
                                    random = new Unity.Mathematics.Random(seed: 1851936439),
                                };
                                LoadSimEnvironment(ref game, gameParams);
                                InitializeUI(ref game.uiMain, game, gameParams);
                                game.audioSnapshot_clean.TransitionTo(.3f);
                                game.uiMain.screen_levelStart.data.rootGameObject.SetActive(true);
                                game.uiMain.eventSystem.SetSelectedGameObject(game.uiMain.screen_levelStart.data.defaultSelection);
                                //RecallLevelState(ref game.simulation, game.simulation.levelSaveState_initial);
                            }
                            break;
                        case UI.ActionType.QUIT:
#if UNITY_EDITOR
                            EditorApplication.isPlaying = false;
#else
                        // TODO(stef): Add confirmation screen before actually quitting
                        Application.Quit();
#endif
                            break;
                        case UI.ActionType.SETVOL_MUSIC:
                            gameParams.audioMixer.SetFloat("vol_music", normalizedToDb(action.slider.value));
                            break;
                        case UI.ActionType.SETVOL_SFX:
                            gameParams.audioMixer.SetFloat("vol_sfx", normalizedToDb(action.slider.value));
                            gameParams.audioMixer.SetFloat("vol_ui", normalizedToDb(action.slider.value));
                            break;
                        case UI.ActionType.TOGGLE_FULLSCREEN:
                            userInput_general.fullScreen = true;
                            break;
                        default:
                            break;
                    }
                }
            }
            #endregion


            if (game.simulation.metaState == Simulation.MetaState.SIMULATING)
            {
                #region // fixed rate sim
                // Add time to sim budget for delta time
                game.fixedRate_timeAvailable = math.min(game.fixedRate_timeAvailable + deltaTime, gameParams.fixedRate_maxTimeAccum);

                // determine number of iterations and consume available time
                int fixedRateIterations;
                if (!game.simulation.paused)
                {
                    fixedRateIterations = (int)math.floor(game.fixedRate_timeAvailable / gameParams.fixedRate_timeStep);
                    game.fixedRate_timeAvailable -= fixedRateIterations * gameParams.fixedRate_timeStep;
                }
                else
                {
                    fixedRateIterations = 1;
                    game.fixedRate_timeAvailable = 0;
                }

                for (int i = 0; i < fixedRateIterations; i++)
                {
                    InputSystem.Update();
                    
                    // Input - NonGameplay
                    userInput_general.pause |= game.actionMaps.General.pause.triggered;
                    userInput_general.fullScreen |= game.actionMaps.General.fullScreen.triggered;
                    userInput_general.devMode |= game.actionMaps.General.devMode.triggered;

                    if (userInput_general.pause)
                        break;

                    if (!game.simulation.paused)
                    {                                            
                        // user input
                        Input.Sample_Pawn userInput_sim = default;
                        userInput_sim.moveDir = game.actionMaps.PawnControl.move.ReadValue<Vector2>();
                        userInput_sim.jumpTriggered = game.actionMaps.PawnControl.jump.triggered;
                        userInput_sim.jumpValue = game.actionMaps.PawnControl.jump.ReadValue<float>() > 0;
                        userInput_sim.fallthrough = game.actionMaps.PawnControl.move.ReadValue<Vector2>().y < -.5f;

                        TickGameplaySim(
                              ref game.simulation
                            , deltaTime: gameParams.fixedRate_timeStep
                            , userInput_sim
                            , gameParams.assetReferences
                            );

                        // TODO(stef): we would be able to sync transforms only once per render frame, but currently we use transform data in gameplay sim
                        Physics2D.SyncTransforms();
                    }
                }
                #endregion

                #region // Hud
                {
                    game.uiMain.screen_hud.data.elapsedTime.text = TimeString(game.simulation.elapsedSeconds, monospace: true);
                    game.uiMain.screen_hud.data.collectedCount.text = CountString(game.simulation.pickupCount, minDigits: 3, monoSpace: true);
                }
                #endregion

                #region // Consume non-sim input
                if (userInput_general.pause)
                {
                    TogglePauseScreen(ref game);
                }

                if (userInput_general.fullScreen)
                {
                    if (Screen.fullScreen)
                        Screen.SetResolution(1280, 720, false);
                    else
                        Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
                }
                if (userInput_general.devMode) {
                    game.simulation.devMode = !game.simulation.devMode;
                    // game.uiMain.screen_debug.data.rootGameObject.SetActive(game.simulation.devMode);
                }
                #endregion
            }
            else
            {
                // For UI event system(s) to work, update input once per frame when we arent running the gameplay simulation.
                // If we are simulating, InputSystem.Update() will be called once per fixed update
                InputSystem.Update();
            }

            // Enable level complete screen if level finished
            if (game.simulation.levelComplete && !game.uiMain.screen_levelComplete.data.rootGameObject.activeInHierarchy)
            {
                UI.Screen_LevelComplete screen = game.uiMain.screen_levelComplete.data;
                screen.rootGameObject.SetActive(true);
                screen.collectableCount.text = CountString(game.simulation.pickupCount, minDigits: 3, monoSpace: false);
                screen.elapsedTime.text = TimeString(game.simulation.elapsedSeconds, monospace: false);
                game.uiMain.eventSystem.SetSelectedGameObject(screen.defaultSelection);
            }

            #region // Performance graphs
            if (game.uiMain.initialized && game.uiMain.screen_debug.data.rootGameObject.activeInHierarchy)
            {
                UI.BarGraph graph = game.uiMain.screen_debug.data.frameTimeGraph.data;

                float highestValue = 1f / 480;
                for (int i = 0; i < graph.samples.Length; i++)
                {
                    if (graph.samples[i] > highestValue)
                        highestValue = graph.samples[i];
                }
                //graph.image.material.SetFloat(graph.propID_highValue, highestValue);
                graph.image.material.SetFloatArray(graph.propID_samples, graph.samples);
            }
            #endregion // Performance graph
        }

        public static void
        SetSimPaused(ref Simulation simulation, bool value)
        {
            simulation.paused = value;
            // Pause/resume all animators in the sim environment
            if (simulation.environment != null)
            {
                if (simulation.paused)
                {
                    if (simulation.environment.pausedAnimators == null)
                        simulation.environment.pausedAnimators = new List<Animator>(64);
                    PauseAllAnimationsInEnvironment(simulation.environment, ref simulation.environment.pausedAnimators);
                }
                else if (simulation.environment.pausedAnimators != null)
                {

                    ResumeAnimators(simulation.environment.pausedAnimators);
                }
            }
        }

        public static void
        TogglePauseScreen(ref Game game)
        {
            bool pause = !game.simulation.paused;
            SetSimPaused(ref game.simulation, pause);

            AudioMixerSnapshot audioSnapshot = pause ? game.audioSnapshot_faded : game.audioSnapshot_clean;
            audioSnapshot.TransitionTo(.3f);

            game.uiMain.screen_pause.data.rootGameObject.SetActive(game.simulation.paused);
            if (game.simulation.paused)
            {
                game.uiMain.screen_pause.data.elapsedTime.text = TimeString(game.simulation.elapsedSeconds, monospace: false);
                game.uiMain.screen_pause.data.collectableCount.text = CountString(game.simulation.pickupCount, minDigits: 3, monoSpace: false);
                game.uiMain.eventSystem.SetSelectedGameObject(game.uiMain.screen_pause.data.defaultSelection);
            }
        }

        public static void
        PauseAllAnimationsInEnvironment(Simulation_Environment env, ref List<Animator> pausedAnimators)
        {
            pausedAnimators.Clear();
            env.rootTransform.GetComponentsInChildren<Animator>(pausedAnimators);
            Animator anim;
            for (int i = pausedAnimators.Count - 1; i >= 0; i--)
            {
                anim = pausedAnimators[i];
                if (anim.playableGraph.IsPlaying())
                    anim.speed = 0;
                //anim.playableGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                else
                    pausedAnimators.RemoveAt(i);
            }
        }

        public static void
        ResumeAnimators(List<Animator> anims)
        {
            foreach (Animator anim in anims)
            {
                anim.speed = 1;
                //anim.playableGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.GameTime);
            }
            anims.Clear();
        }

#if UNITY_EDITOR
        public static void
        ValidateSceneSet(ref SceneSet set)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(2);
            // Add main scene
            scenes.Add(new EditorBuildSettingsScene()
            {
                enabled = true,
                path = AssetDatabase.GetAssetOrScenePath(set.mainScene),
            });

            int nextSceneIndex = 1;

            // Add level
            if (set.level != null)
            {
                scenes.Add(new EditorBuildSettingsScene()
                {
                    enabled = true,
                    path = AssetDatabase.GetAssetOrScenePath(set.level),
                });
                set.level_buildIndex = nextSceneIndex;
            }
            else
            {
                set.level_buildIndex = -1;
            }
#if false
            // Add arenas
            set.levels_buildIndicies = new int[set.levels.Length];
            for (int i = 0; i < set.levels.Length; i++)
            {
                if (set.levels[i] != null)
                {
                    scenes.Add(new EditorBuildSettingsScene()
                    {
                        enabled = true,
                        path = AssetDatabase.GetAssetOrScenePath(set.levels[i]),
                    });
                    set.levels_buildIndicies[i] = nextSceneIndex++;
                }
                else
                {
                    set.levels_buildIndicies[i] = -1;
                }
            }
#endif

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void
        GizmosDrawXYCircle(float3 center, float radius, float resolution)
        {
            int nSegments = (int)ceil(2 * PI * radius * resolution);
            float theta = 0;
            float3 offsetDir = 0;
            offsetDir.x = cos(theta);
            offsetDir.y = sin(theta);
            float3 pos_prev = center + offsetDir * radius;
            float3 pos;
            for (int s = 1; s < nSegments; s++)
            {
                theta = ((float)s / (nSegments - 1)) * PI * 2;
                offsetDir.x = cos(theta);
                offsetDir.y = sin(theta);
                pos = center + offsetDir * radius;
                Gizmos.DrawLine(pos_prev, pos);
                pos_prev = pos;
            }
        }
#endif

        #region // Utilities
        static string
        TimeString(float seconds, bool monospace)
        {
            if (monospace)
                return $"<mspace=.35em>{((int)math.floor(seconds / 60f)).ToString("D2")}:{((int)math.floor(seconds % 60f)).ToString("D2")}</mspace>";
            else
                return $"{((int)math.floor(seconds / 60f)).ToString("D2")}:{((int)math.floor(seconds % 60f)).ToString("D2")}";
        }

        static string
        CountString(int count, int minDigits, bool monoSpace)
        {
            if (monoSpace)
                return $"<mspace=.35em>{count.ToString($"D{minDigits}")}</mspace>";
            else
                return count.ToString($"D{minDigits}");
        }

        static float DbToNormalized(float db)
        {
            return 1f - (math.clamp(db, -40f, 0f) / -40f);
        }

        static float normalizedToDb(float normalized)
        {
            if (normalized <= 0)
                return -80f;
            return (1f - math.clamp(normalized, 0f, 1f)) * -40f;

        }

        static float2
        ClampMagnitude(
              float2 v
            , float min
            , float max
            )
        {
            /// TODO :: I'm certain this can be optimized
            return math.normalize(v) * math.clamp(math.length(v), min, max);
        }

        static float PI2 = math.PI * 2;
        static float
        PhiDelta(float phi0, float phi1)
        {
            /// TODO :: do this without branching??
            float delta = phi0 - phi1;
            if (delta < -math.PI)
                delta += PI2;
            return delta;
        }

        static bool
        BoundsRayIntersection(
            float2 boundsMin
            , float2 boundsMax
            , float2 rayOrigin
            , float2 rayInverseDelta
            )
        {
            // https://tavianator.com/fast-branchless-raybounding-box-intersections/
            // NOTE :: We're assuming some good behaviour in math.min and math.max which stops NaNs where possible. NaNs can appear in "edge cases", but min/max seems to deal with them.

            int axis = 0;
            float t1 = (boundsMin[axis] - rayOrigin[axis]) * rayInverseDelta[axis];
            float t2 = (boundsMax[axis] - rayOrigin[axis]) * rayInverseDelta[axis];
            float tmin = math.min(t1, t2);
            float tmax = math.max(t1, t2);

            axis = 1;

            t1 = (boundsMin[axis] - rayOrigin[axis]) * rayInverseDelta[axis];
            t2 = (boundsMax[axis] - rayOrigin[axis]) * rayInverseDelta[axis];
            tmin = math.max(tmin, math.min(t1, t2));
            tmax = math.min(tmax, math.max(t1, t2));

            return tmax > 0 && tmax > tmin && tmin < 1;
        }

        public static bool
        SegSegIntersection(
            float2x2 seg0
            , float2x2 seg1
            , out float intersection_distanceNormalized
            )
        {
            return SegSegIntersection(seg0.c0, seg0.c1, seg1.c0, seg1.c1, out intersection_distanceNormalized);
        }

        public static bool
        SegSegIntersection(
              float2 seg0_0
            , float2 seg0_1
            , float2 seg1_0
            , float2 seg1_1
            , out float intersection_distanceNormalized
            )
        {
            float2 delta0 = seg0_1 - seg0_0;
            float2 delta1 = seg1_1 - seg1_0;

            float s = (-delta0.y * (seg0_0.x - seg1_0.x) + delta0.x * (seg0_0.y - seg1_0.y)) / (-delta1.x * delta0.y + delta0.x * delta1.y);
            intersection_distanceNormalized = (delta1.x * (seg0_0.y - seg1_0.y) - delta1.y * (seg0_0.x - seg1_0.x)) / (-delta1.x * delta0.y + delta0.x * delta1.y);

            return s >= 0 && s <= 1 && intersection_distanceNormalized > 0 && intersection_distanceNormalized <= 1;
        }

        static bool
        PointInPoly(float2 point, float2[] poly)
        {
            int wn = 0;    // the  winding number counter

            // loop through all edges of the polygon
            float2x2 seg;
            seg.c0 = poly[0];
            for (int i = 0; i < poly.Length; i++)
            {
                seg.c1 = poly[(i + 1) % poly.Length];
                // edge from V[i] to  V[i+1]

                // start y <= P.y
                if (seg.c0.y <= point.y)
                {
                    // an upward crossing
                    // P left of  edge, meaning we have a valid up intersect
                    if (seg.c1.y > point.y && PointIsLeftOfSegment(seg, point) > 0)
                        ++wn;
                }
                // start y > P.y
                // a downward crossing
                // P right of  edge, meaning we have  a valid down intersect
                else if (seg.c1.y <= point.y && PointIsLeftOfSegment(seg, point) < 0)
                {
                    --wn;
                }
                seg.c0 = seg.c1;
            }
            return wn != 0;
        }

        // 0 == on line, >0 == left, <0 == right
        static float
        PointIsLeftOfSegment(float2x2 segment, float2 point)
        {
            return ((segment[1].x - segment[0].x) * (point.y - segment[0].y) - (point.x - segment[0].x) * (segment[1].y - segment[0].y));
        }

        static
        int Repeat(int val, int length)
        {
            /// TODO :: Optimize, remove branch at least
            if (val < 0)
                return length - (((-val - 1) % length) + 1);
            else
                return val % length;
        }
        #endregion // Utilities

        #region // Gizmos buffers
        public static void DrawGizmosBuffer(GizmosCommandBuffer buffer)
        {
            if (buffer != null)
            {
                GizmosCommand cmd;
                GizmosCommand.JobID currentID = GizmosCommand.JobID.invalid;
                for (int i = 0; i < buffer.nCommands; i++)
                {
                    cmd = buffer.commands[i];
                    switch (cmd.type)
                    {
                        case GizmosCommand.CommandType.DRAW_LINE:
                            Gizmos.DrawLine(cmd.v0, cmd.v1);
                            break;
                        case GizmosCommand.CommandType.DRAW_RAY:
                            Gizmos.DrawRay(cmd.v0, cmd.v1);
                            break;
                        case GizmosCommand.CommandType.DRAW_WIRE_CUBE:
                            Gizmos.DrawWireCube(cmd.v0, cmd.v1);
                            break;
                        case GizmosCommand.CommandType.DRAW_CUBE:
                            Gizmos.DrawCube(cmd.v0, cmd.v1);
                            break;
                        case GizmosCommand.CommandType.SET_MATRIX:
                            Gizmos.matrix = cmd.matrix;
                            break;
                        case GizmosCommand.CommandType.SET_COLOR:
                            Gizmos.color = cmd.color;
                            break;
                        case GizmosCommand.CommandType.SET_ID:
                            GizmosCommand.JobID newID = cmd.id;
                            if (newID != currentID)
                            {
                                ///Gizmos.color = ;
                                currentID = newID;
                            }
                            break;
                    }
                }
            }
        }

        public static void GizmosSetJobID(ref GizmosCommandBuffer buffer, GizmosCommand.JobID id)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.SET_ID,
                id = id,
            };
        }

        public static void GizmosSetMatrix(ref GizmosCommandBuffer buffer, Matrix4x4 matrix)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.SET_MATRIX,
                matrix = matrix,
            };
        }

        public static void GizmosSetColor(ref GizmosCommandBuffer buffer, Color color)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.SET_COLOR,
                color = color,
            };
        }

        public static void GizmosDrawPoints(ref GizmosCommandBuffer buffer, Vector2[] points, int nPoints, float cubeSize = .05f)
        {
            for (int i = 0; i < nPoints; i++)
            {
                GizmosDrawCube(ref buffer, points[i], Vector3.one * cubeSize);
            }
        }

        public static void GizmosDrawSegments_Clown(ref GizmosCommandBuffer buffer, Vector2[] verts, int[] segments, Vector3[] normals, int[] meshIDs, bool[] invalidity, int nSegments, bool drawHeads, bool drawNormals)
        {
            if (nSegments <= 0) return;
            for (int i = 0; i < nSegments; i++)
            {
                //GizmosSetColor(ref buffer, invalidity[i] ? Colors.grey : Colors.clown[meshIDs[i] % Colors.clown.Length]);
                GizmosSetColor(ref buffer, invalidity[i] ? Colors.grey : Colors.green);
                GizmosDrawSegment(ref buffer, verts[segments[i * 2]], verts[segments[i * 2 + 1]], normals[i], drawHeads, drawNormals);
            }
        }

        public static void GizmosDrawSegments(ref GizmosCommandBuffer buffer, Vector2[] verts, int[] segments, Vector3[] normals, int nSegments, bool drawHeads, bool drawNormals, bool cullInvalidated, bool[] invalidated)
        {
            if (nSegments <= 0) return;
            for (int i = 0; i < nSegments; i++)
            {
                if (!cullInvalidated || !invalidated[i])
                    GizmosDrawSegment(ref buffer, verts[segments[i * 2]], verts[segments[i * 2 + 1]], normals[i], drawHeads, drawNormals);
            }
        }

        public static void GizmosDrawSegment(ref GizmosCommandBuffer buffer, Vector2 v0, Vector2 v1, Vector3 normal, bool drawHead, bool drawNormal)
        {
            GizmosDrawLine(ref buffer, (Vector3)v0, (Vector3)v1);
            if (drawHead)
            {
                buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
                {
                    type = GizmosCommand.CommandType.DRAW_LINE,
                    v0 = v1,
                    v1 = (Vector3)v1 + (((Vector3)(v0 - v1).normalized + normal) * .2f),
                };
            }
            if (drawNormal)
            {
                buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
                {
                    type = GizmosCommand.CommandType.DRAW_RAY,
                    v0 = v0 + ((v1 - v0) * .5f),
                    v1 = normal * .2f,
                };
            }
        }

        public static void GizmosDrawSegment(ref GizmosCommandBuffer buffer, float2 v0, float2 v1, float2 normal, bool drawHead, bool drawNormal)
        {
            GizmosDrawLine(ref buffer, v0, v1);
            if (drawHead)
            {
                buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
                {
                    type = GizmosCommand.CommandType.DRAW_LINE,
                    v0 = (Vector2)v1,
                    v1 = (Vector2)v1 + (((Vector2)math.normalize(v0 - v1) + (Vector2)normal) * .2f),
                };
            }
            if (drawNormal)
            {
                buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
                {
                    type = GizmosCommand.CommandType.DRAW_RAY,
                    v0 = (Vector2)v0 + ((Vector2)(v1 - v0) * .5f),
                    v1 = (Vector2)normal * .2f,
                };
            }
        }

        public static void GizmosDrawArrowHead(ref GizmosCommandBuffer buffer, Vector3 position, Vector3 direction, float scale)
        {
            // Left and back
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_RAY,
                v0 = position,
                v1 = Quaternion.AngleAxis(180 - 30, Vector3.forward) * direction.normalized * scale,
            };

            // Right and back
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_RAY,
                v0 = position,
                v1 = Quaternion.AngleAxis(180 + 30, Vector3.forward) * direction.normalized * scale,
            };
        }

        public static void GizmosDrawLine(ref GizmosCommandBuffer buffer, Vector3 position0, Vector3 position1)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_LINE,
                v0 = position0,
                v1 = position1,
            };
        }

        public static void GizmosDrawLine(ref GizmosCommandBuffer buffer, float2 position0, float2 position1)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_LINE,
                v0 = new float3(position0, 0),
                v1 = new float3(position1, 0),
            };
        }

        public static void GizmosDrawBounds2D(ref GizmosCommandBuffer buffer, float2 min, float2 max)
        {
            float width = max.x - min.x;
            float height = max.y - min.y;
            float2x2 seg;
            seg.c0 = min;
            seg.c1 = seg.c0;
            seg.c1.y += height;
            GizmosDrawLine(ref buffer, seg.c0, seg.c1);
            seg.c0 = seg.c1;
            seg.c1.x += width;
            GizmosDrawLine(ref buffer, seg.c0, seg.c1);
            seg.c0 = seg.c1;
            seg.c1.y += -height;
            GizmosDrawLine(ref buffer, seg.c0, seg.c1);
            seg.c0 = seg.c1;
            seg.c1.x += -width;
            GizmosDrawLine(ref buffer, seg.c0, seg.c1);
        }

        public static void GizmosDrawRay(ref GizmosCommandBuffer buffer, Vector3 position, Vector3 direction)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_RAY,
                v0 = position,
                v1 = direction,
            };
        }

        public static void GizmosDrawWireCube(ref GizmosCommandBuffer buffer, Vector3 position, Vector3 scale)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_WIRE_CUBE,
                v0 = position,
                v1 = scale,
            };
        }

        public static void GizmosDrawWireCube(ref GizmosCommandBuffer buffer, float2 position, float2 scale)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_WIRE_CUBE,
                v0 = new float3(position, 0),
                v1 = new float3(scale, 0),
            };
        }

        public static void GizmosDrawCube(ref GizmosCommandBuffer buffer, Vector3 position, Vector3 scale)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_CUBE,
                v0 = position,
                v1 = scale,
            };
        }

        public static void GizmosDrawCube(ref GizmosCommandBuffer buffer, float2 position, float2 scale)
        {
            buffer.commands[buffer.incrementedCommandIndex] = new GizmosCommand
            {
                type = GizmosCommand.CommandType.DRAW_CUBE,
                v0 = new float3(position, 0),
                v1 = new float3(scale, 0),
            };
        }

        public static void GizmosDrawFace(ref GizmosCommandBuffer buffer, NativeArray<float3> edgeVerts, int nEdges)
        {
            for (int edge = 0; edge < nEdges; edge++)
                GizmosDrawLine(ref buffer, edgeVerts[edge * 2], edgeVerts[edge * 2 + 1]);
        }

        public static void GizmosDrawTriangle(ref GizmosCommandBuffer buffer, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 normal, bool drawNormal = true)
        {
            GizmosDrawLine(ref buffer, v0, v1);
            GizmosDrawLine(ref buffer, v0, v2);
            GizmosDrawLine(ref buffer, v1, v2);

            if (drawNormal)
            {
                Vector3 center =
                      Vector3.right * ((v0.x + v1.x + v2.x) / 3)
                    + Vector3.up * ((v0.y + v1.y + v2.y) / 3)
                    + Vector3.forward * ((v0.z + v1.z + v2.z) / 3);
                GizmosDrawRay(ref buffer, center, normal * ((Vector3.Distance(v0, v1) + Vector3.Distance(v0, v2) + Vector3.Distance(v1, v2)) / 24));
            }
        }

        public static void GizmosDrawPlane(ref GizmosCommandBuffer buffer, float normalScale, bool outer = true, bool cross = true, bool x = true, bool normal = true)
        {
            if (normal)
                GizmosDrawRay(ref buffer, Vector3.zero, Vector3.forward * normalScale);

            if (outer)
            {
                Vector3 currentPoint;
                Vector3 nextPoint;
                currentPoint = -Vector2.one * .5f;
                nextPoint = currentPoint + Vector3.up;
                GizmosDrawLine(ref buffer, currentPoint, nextPoint);

                currentPoint = nextPoint;
                nextPoint += Vector3.right;
                GizmosDrawLine(ref buffer, currentPoint, nextPoint);

                currentPoint = nextPoint;
                nextPoint += Vector3.down;
                GizmosDrawLine(ref buffer, currentPoint, nextPoint);

                currentPoint = nextPoint;
                nextPoint += Vector3.left;
                GizmosDrawLine(ref buffer, currentPoint, nextPoint);
            }

            if (cross)
            {
                GizmosDrawLine(ref buffer, (Vector3)Vector2.down * .5f, (Vector3)Vector2.up * .5f);
                GizmosDrawLine(ref buffer, (Vector3)Vector2.left * .5f, (Vector3)Vector2.right * .5f);
            }
            if (x)
            {
                GizmosDrawLine(ref buffer, -(Vector3)Vector2.one * .5f, (Vector3)Vector2.one * .5f);
                GizmosDrawLine(ref buffer, -(Vector3)Vector2.one * .5f + (Vector3)Vector2.up, Vector2.one * .5f + Vector2.down);
            }
        }

#if false
        /// TODO :: repurpose this for use with gizmos command buffers
        static void GizmosDrawMesh(Mesh mesh, Vector3 pos)
        {
            int[] indices = mesh.GetIndices(0);
            Vector3[] verts = mesh.vertices;
            int nTris = indices.Length / 3;
            for (int t = 0; t < nTris; t++)
            {
                for (int v = 0; v < 3; v++)
                {
                    int vi_0 = (t * 3) + (v);
                    int vi_1 = (t * 3) + ((v + 1) % 3);
                    Gizmos.DrawLine(pos + mesh.vertices[indices[vi_0]], pos + mesh.vertices[indices[vi_1]]);
                }
            }
        }
#endif
        #endregion // Gizmos buffer drawing
    }
}
