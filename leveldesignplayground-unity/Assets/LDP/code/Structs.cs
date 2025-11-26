using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System;
using System.Threading;
using UnityEngine.SceneManagement;
using Cinemachine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DevDev.LDP
{
    public static class GlobalDebug
    {
        public static GizmosCommandBuffer gizmos_navLineBuild = new GizmosCommandBuffer();
        public static GizmosCommandBuffer gizmos_footholds = new GizmosCommandBuffer();
    }

    public static class Defines
    {
        public const string assetMenu_Root = "LDP Scriptable Object/";
        public const string assetMenu_GameplayParams = "Gameplay/";
        public const string assetMenu_Input = "Input/";
        public const string assetMenu_UI = "UI/";
        public const float epsilon_distance = 0.001f;
        public const float epsilon_time = 0.001f;
        public static LayerMask layer_oneWayPlatforms = LayerMask.NameToLayer("oneWayPlatforms");
    }    

    [Serializable]
    public class GameParams
    {
        public UInt16 frameRateMin = 30;
        public UInt16 frameRateMax = 144;
        public UInt16 fixedTickRate = 240;
        internal float fixedRate_timeStep;
        internal float fixedRate_maxTimeAccum;
        internal float dynamicRate_minTimeStep;
        internal float dynamicRate_maxTimeStep;

        public AudioMixer audioMixer;
        public AssetReferences_so assetReferences_so;
        internal AssetReferences assetReferences { get { return assetReferences_so.data; } }
        public SceneSet_so sceneSet;
    }

    [Serializable]
    public class AssetReferences
    {
        public UI.UIMain_mono mainScreenUI;
        public PlayerAvatar_mono playerFab;       
        public Enemy_mono enemyFab;
    }

    public class Game
    {
        internal UI.UIMain uiMain;
        internal float fixedRate_timeAvailable;
        internal Int64 ticks_startOfPreviousFrame;
        internal bool initialized;
        internal SceneLoadJob environmentLoadJob;
        internal Simulation simulation;
        //internal Input.InputSystemState input;
        internal AutoResetEvent resetEvent;
        internal Input.InputActions actionMaps;
        internal AudioMixer audioMixer;
        internal AudioMixerSnapshot audioSnapshot_clean;
        internal AudioMixerSnapshot audioSnapshot_faded;
    }

    [Serializable]
    public class Simulation
    {
        internal enum MetaState
        {
            INVALID,
            LOADING,
            LOADED,
            SIMULATING
        }

        internal bool simmedOnce;
        internal bool paused;
        internal MetaState metaState;
        internal Simulation_Environment environment;
        internal Unity.Mathematics.Random random;
        internal PlayerAvatar playerAvatar;
        internal Vector3 lastKnownPlayerPos;
        internal LevelSaveState levelSaveState;
        internal RaycastHit2D[] cache_raycastHits = new RaycastHit2D[30];
        internal float elapsedSeconds;
        internal int pickupCount;
        internal bool levelComplete;
        internal bool devMode;
    }

    [Serializable]
    public struct Pawn
    {
        public Transform rootTransform;
        public Transform top;
        public Transform hips;
        public Transform bottom;
        public Rigidbody2D rigidbody;
        public Collider2D hitbox;
        public Collider2D physicsCollider;
        public PlayerKinematicsParams_so kinematicsParams;
        public SpriteRenderer spriteRenderer;
        public bool spriteFacingLeft;
        public Animator animator;
        public AnimParams_Pawn animParams;
        public AudioSource as_jump;
        public AudioSource as_footstep;
        public AudioSource as_footstep_platform;
        public AudioSource as_spawn;
        public AudioSource as_death;
        public AudioSource as_land;
        public AudioSource as_land_platform;
        public AnimationEventReciever_mono footstepEvent;
        internal AnimStateEntrySensor animSensor_deathFinished;
        internal AnimStateEntrySensor animSensor_spawnFinished;

        internal bool valid;
        internal bool killed;
        internal int colliderID;
        internal float characterHeight;
        internal float hipsHeight;
        internal bool isJumping;
        internal float timeSinceGrounded;
        internal float timeSinceJumpTriggered;
        internal bool onGround_prev;
    }

    [Serializable]
    public struct PlayerAvatar
    {
        public Pawn pawn;
        public AnimParams_Player animParams;        
        internal bool finishedSpawning;
    }

    [Serializable]
    public class AnimParams_Player
    {
    }

    [Serializable]
    public class AnimParams_Pawn
    {
        /// TODO(stef): move these out to the player anim params. Enemies don't need these params... Kinematics proc needs to be updated to accomodate the change.
        public AnimNameHash animParam_jumpStart;
        public AnimNameHash animParam_isJumping;
        public AnimNameHash animParam_grounded;

        public AnimNameHash animParam_moveSpeed;
        public AnimNameHash animParam_killed;
        public AnimNameHash animSensor_appearFinished;
        public AnimNameHash animSensor_disappearFinished;
    }

    [Serializable]
    public class AnimParams_AngryPig
    {
        public AnimNameHash animParam_angry;
    }

    [Serializable]
    public struct Enemy
    {
        public Enemy_Params_so aiParams;
        public bool patrolLeft;
        public Pawn pawn;
        public AnimParams_AngryPig animParams;
        public AudioSource as_hitPlayer;
        internal bool valid;
        internal bool movingRight;
        internal bool isAggro;
        internal float timeSinceSeenPlayer;
    }

    [Serializable]
    public struct Enemy_Params
    {
        [Range(0,1)]
        public float speedScale_calm;
        [Range(0,1)]
        public float speedScale_aggro;
        public float vision_maxDist_calm;
        public float vision_maxDist_aggro;
        public float aggroLossDelay;
        //public float searchTime;
        //public float directionChangeMinDelay;
    }

    [Serializable]
    public struct AnimNameHash
    { 
        public string name;
        internal int nameHash;
        public void InitializeID()
        {
            nameHash = Animator.StringToHash(name);
        }
    }

    [Serializable]
    public struct PawnKinematicsParams
    {
        public float jumpHeight;
        public float bounceHeight;
        public float legReach_pawnWidth;
        [Range(0,1)]
        public float legReach_extraWhenRunning;
        [Range(0,1)]
        public float jumpForgiveness_early;
        [Range(0,1)]
        public float jumpForgiveness_late;
        [Range(5,60)]
        public float footholdRaycastResolution;
        [Range(10,90)]
        public float maxSlope;
        public float maxRunSpeed;
        public float maxRunAccel;
        public float airControl_maxSpeed;
        public float airControl_maxAccel;
        public float drag_air;
        public float drag_braking_passive;
        public float drag_braking_reversalHelp;
    }

    public enum GroundMaterial
    {
        DEFAULT, WOOD
    }

    public struct Foothold
    {
        internal bool valid;
        public float2 position;
        public float2 normal;
        public int colliderID;
        public float forwardDistance;
        public float forwardDistanceNormalized;
        public float rayHitDistance;
        public GroundMaterial groundMaterial;
    }

    public struct TransformEnergy
    {
        public float2 force;
        public float2 acceleration;
        public float2 acceleration_impulse;
    }

    [Serializable]
    public struct Checkpoint
    {
        public Collider2D collider;
        public Transform spawnPoint;
        public Animator animator;
        public AnimNameHash animID_isActive;
        public AudioSource as_activate;
        internal bool valid;
    }

    [Serializable]
    public struct LevelExit
    {
        public Collider2D collider;
        public AudioSource as_activate;
    }

    internal struct LevelSaveState
    {
        internal bool valid;
        internal int checkpointIndex;
        internal int playerCollectableCount;
        internal float elapsedSeconds;
        internal bool[] pickups_collected;
        internal Enemy[] enemies;
        internal MovingPlatform[] platforms;
        internal float2[] enemies_position;
        internal float2[] enemies_velocity;
    }

    [Serializable]
    public class Simulation_Environment
    {
        public Transform playerSpawn;
        public Transform boundary_killFloor;
        public CinemachineVirtualCameraBase cam_followCam;

        internal string name;
        internal Scene scene;
        internal Transform rootTransform;
        internal GameObject rootGameObject;
        internal Collectable[] collectables;
        internal Checkpoint[] checkpoints;
        internal LevelExit[] exits;
        internal Hazard[] hazards;
        internal MovingPlatform[] movingPlatforms;
        internal Enemy[] enemies;
        internal List<Animator> pausedAnimators;
    }

    [Serializable]
    public struct Hazard
    {
        public Collider2D collider;
        public AudioSource hitSound;
    }

    [Serializable]
    public struct Collectable
    {
        public GameObject rootGameObject;
        public Animator animator;
        public Collider2D collider;
        public AnimNameHash animID_collected;
        public AudioSource as_collected;
        internal AnimStateEntrySensor animationCompletionSensor;
        internal bool collected;
    }

    public enum PlatformTrackType
    {
        CIRCULAR,
        PINGPONG,
    }

    [Serializable]
    public struct MovingPlatform
    {
        public Transform rootTransform;
        public Transform platform;
        public Collider2D platformCollider;
        public float cycleTime;
        [Range(0,1)]
        public float loopOffset;
        public PlatformTrackType trackType;
        public bool circular_lockAngle;
        public float circular_radius;
        public Transform pingpong_waypoint_0;
        public Transform pingpong_waypoint_1;
        internal float progress;
        internal float2 lastKnownVelocity;
        /// TODO :: pauses? easing? non-looping? start inactive?
    }

#if false // No idea why this stopped working!
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MovingPlatform))]
    public class MovingPlatformDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            
            SerializedProperty typeProp = property.FindPropertyRelative("trackType");
            PlatformTrackType type = (PlatformTrackType)typeProp.enumValueIndex;

            EditorGUILayout.PropertyField(property.FindPropertyRelative("rootTransform"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("platform"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("platformCollider"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("cycleTime"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("loopOffset"));
            EditorGUILayout.PropertyField(typeProp);
            switch (type)
            {
                case PlatformTrackType.CIRCULAR:
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("circular_lockAngle"));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("circular_radius"));

                    break;
                case PlatformTrackType.PINGPONG:
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("pingpong_waypoint_0"));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("pingpong_waypoint_1"));
                    break;
            }
        }
    }
#endif
#endif

    [Serializable]
    public class SceneSet
    {
#if UNITY_EDITOR
        public SceneAsset mainScene;
        public SceneAsset level;
        //public SceneAsset[] levels;
#endif
        [HideInInspector]
        public int level_buildIndex;
        //[HideInInspector]
        //public int[] levels_buildIndicies;
    }

    public struct SceneLoadJob
    {
        internal int sceneBuildIndex;
        internal AsyncOperation asyncOp;
    }

    public struct Colors
    {
        public static readonly Color clear = Color.clear;
        public static readonly Color white = Color.white;
        public static readonly Color grey = Color.grey;
        public static readonly Color darkGrey = new Color(.15f, .15f, .15f);
        public static readonly Color black = Color.black;
        public static readonly Color red = Color.red;
        public static readonly Color green = Color.green;
        public static readonly Color blue = Color.blue;
        public static readonly Color cyan = Color.cyan;
        public static readonly Color magenta = Color.magenta;
        public static readonly Color yellow = Color.yellow;
        public static readonly Color lightSalmon = new Color(1, 160f / 255, 122f / 255);
        public static readonly Color darkOrange = new Color(1, 140f / 255, 0);
        public static readonly Color coral = new Color(1, 127f / 255, 80f / 255);
        public static readonly Color gold = new Color(1, 165f / 255, 0);
        public static readonly Color[] clown = new Color[]
        {
            coral          ,
            cyan           ,
            red            ,
            green          ,
            blue           ,
            magenta        ,
            lightSalmon    ,
            yellow         ,
            gold           ,
            darkOrange     ,
        };
    }

    public struct DebugDrawProfile
    {
        public bool usePriority;
        public uint minPriorityToDraw;
        public uint minPriorityToDrawGrey;
        public Color[] colorsByPriority;
        public Color[] colorsByID;
        public uint[] priority;
        public bool[] ignore;
    }

    public class GizmosCommandBuffer
    {
        const int maxNGizmosCommands = 5000;
        internal GizmosCommand[] commands = new GizmosCommand[maxNGizmosCommands];
        internal int nCommands;
        internal int incrementedCommandIndex
        {
            get
            {
                int res = nCommands;
                nCommands = (nCommands + 1) % maxNGizmosCommands;
                return res;
            }
        }
    }

    public struct GizmosCommand
    {
        public enum CommandType
        {
            DRAW_LINE
            , DRAW_RAY
            , DRAW_WIRE_CUBE
            , DRAW_CUBE
            , SET_MATRIX
            , SET_COLOR
            , SET_ID
        }
        public enum JobID
        {
            invalid
            , nav_culling_mesh
            , nav_culling_triangle
            , nIDs
        }
        internal CommandType type;
        internal Vector3 v0;
        internal Vector3 v1;
        internal Matrix4x4 matrix;
        internal Color color;
        internal JobID id;
    }
}
