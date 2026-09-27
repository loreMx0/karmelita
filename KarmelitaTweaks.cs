using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KarmelitaTweaks
{
    internal enum ProjectileVariation
    {
        Fan5, Stagger4, Aimed, Radial, Boomerang, P3Combo, DelayedWall, AirFan, SkyRain,
    }

    [BepInPlugin("com.btw.tribetower.karmelita", "Karmelita Tweaks", "1.10.2")]
    public class KarmelitaTweaksPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static KarmelitaTweaksPlugin Instance;

        private const string BossName = "Hunter Queen Boss";
        private const string BossFsmName = "Control";
        private const string SicklePrefabName = "Carmelita Sickle";

        // ---- config : general ----
        private static ConfigEntry<bool> cfgEnabled;
        private static ConfigEntry<bool> cfgLogDiagnostics;
        private static ConfigEntry<bool> cfgLogClipChanges;
        private static ConfigEntry<bool> cfgLogStructureEvents;

        // ---- config : animation speed ----
        private static ConfigEntry<float> cfgRecoilSpeed;
        private static ConfigEntry<float> cfgRoarAnticSpeed;
        private static ConfigEntry<float> cfgAnticSpeed;
        private static ConfigEntry<float> cfgAttackSpeed;
        private static ConfigEntry<float> cfgMovementSpeed;
        private static ConfigEntry<float> cfgStunRatio;
        private static ConfigEntry<float> cfgP3SpeedMultiplier;
        private static ConfigEntry<bool> cfgP3VanillaSpeed;
        private static ConfigEntry<float> cfgMinSpeedMult;
        private static ConfigEntry<float> cfgMaxSpeedMult;

        // ---- config : behavior ----
        private static ConfigEntry<bool> cfgDisableBlock;
        private static ConfigEntry<bool> cfgDisableForceEvade;
        private static ConfigEntry<bool> cfgRemoveContactDamage;
        private static ConfigEntry<bool> cfgSkipPatchInP3;

        // ---- config : projectiles ----
        private static ConfigEntry<bool> cfgProjectilesEnabled;
        private static ConfigEntry<float> cfgProjectileSpeed;
        private static ConfigEntry<bool> cfgAirSicklesBothSides;
        private static ConfigEntry<string> cfgThrowL_Angles;
        private static ConfigEntry<string> cfgThrowR_Angles;
        private static ConfigEntry<string> cfgAirSickles_Angles;
        private static ConfigEntry<float> cfgProjectileLifetime;
        private static ConfigEntry<int> cfgMaxLiveExtras;
        private static ConfigEntry<int> cfgForceProjectileDamage;

        // ---- config : phases / variations ----
        private static ConfigEntry<string> cfgP1Variations;
        private static ConfigEntry<string> cfgP2Variations;
        private static ConfigEntry<string> cfgP3Variations;
        private static ConfigEntry<float> cfgStaggerDelay;
        private static ConfigEntry<int> cfgRadialCount;
        private static ConfigEntry<float> cfgBoomerangDelay;
        private static ConfigEntry<float> cfgBoomerangReturnMul;
        private static ConfigEntry<float> cfgAimedSpeed;
        private static ConfigEntry<float> cfgAimedCooldown;
        private static ConfigEntry<int> cfgBoomerangShotCount;
        private static ConfigEntry<float> cfgBoomerangSpread;

        // ---- config : delayed wall ----
        private static ConfigEntry<float> cfgWallDistance;
        private static ConfigEntry<float> cfgWallSpacing;
        private static ConfigEntry<float> cfgWallHoldTime;
        private static ConfigEntry<float> cfgWallStagger;
        private static ConfigEntry<float> cfgWallLaunchSpeed;

        // ---- config : sky rain ----
        private static ConfigEntry<int> cfgSkyRainCount;
        private static ConfigEntry<float> cfgSkyRainHeight;
        private static ConfigEntry<float> cfgSkyRainSpan;
        private static ConfigEntry<float> cfgSkyRainFallSpeed;
        private static ConfigEntry<float> cfgSkyRainMaxY;

        // ---- config : scream ----
        private static ConfigEntry<bool> cfgScreamEnabled;
        private static ConfigEntry<float> cfgScreamDuration;
        private static ConfigEntry<float> cfgScreamSlowdown;
        private static ConfigEntry<string> cfgScreamClipName;
        private static ConfigEntry<float> cfgScreamWhiteStrength;

        // ---- config : feint ----
        private static ConfigEntry<bool> cfgFeintEnabled;
        private static ConfigEntry<string> cfgFeintActiveInPhases;
        private static ConfigEntry<float> cfgFeintChance;
        private static ConfigEntry<string> cfgFeintFromStates;
        private static ConfigEntry<string> cfgFeintToStates;

        // ---- config : wall dive loop ----
        private static ConfigEntry<bool> cfgWallDiveLoopEnabled;
        private static ConfigEntry<string> cfgWallDiveLoopActiveInPhases;
        private static ConfigEntry<int> cfgWallDiveLoopMax;
        private static ConfigEntry<float> cfgWallDiveSpeed;

        // ---- config : visual telegraph ----
        private static ConfigEntry<bool> cfgTelegraphEnabled;
        private static ConfigEntry<string> cfgTelegraphThrowStates;
        private static ConfigEntry<float> cfgTelegraphTintStrength;
        private static ConfigEntry<string> cfgPreserveThrowStates;
        private static ConfigEntry<float> cfgDoubleThrowWindow;
        private static ConfigEntry<string> cfgColorFan;
        private static ConfigEntry<string> cfgColorStagger;
        private static ConfigEntry<string> cfgColorAimed;
        private static ConfigEntry<string> cfgColorRadial;
        private static ConfigEntry<string> cfgColorBoomerang;
        private static ConfigEntry<string> cfgColorCombo;
        private static ConfigEntry<string> cfgColorDelayedWall;
        private static ConfigEntry<string> cfgColorAirFan;
        private static ConfigEntry<string> cfgColorSkyRain;

        // ---- config : LAST STAND ----
        private static ConfigEntry<bool>   cfgLastStandEnabled;
        private static ConfigEntry<int>    cfgLastStandHpThreshold;
        private static ConfigEntry<float>  cfgLastStandTriggerDelay;
        private static ConfigEntry<float>  cfgLastStandDuration;
        private static ConfigEntry<float>  cfgLastStandCornerOffsetXLeftWall;
        private static ConfigEntry<float>  cfgLastStandCornerOffsetXRightWall;
        private static ConfigEntry<float>  cfgLastStandCornerOffsetY;
        private static ConfigEntry<string> cfgLastStandPattern;
        private static ConfigEntry<float>  cfgLastStandBurstDelay;
        private static ConfigEntry<float>  cfgLastStandProjectileSpeed;
        private static ConfigEntry<float>  cfgLastStandBurstSpread;
        private static ConfigEntry<string> cfgLastStandHoldStates;
        private static ConfigEntry<string> cfgLastStandExitState;

        // ---- runtime : fsm / animator ----
        private static PlayMakerFSM _bossFsm;
        private static object _bossAnimator;
        private static Func<object, object> _getCurrentClipDelegate;
        private static PropertyInfo _animCurrentClipProp;
        private static FieldInfo _animCurrentClipField;
        private static FieldInfo _clipFpsField;
        private static PropertyInfo _clipNameProp;
        private static FieldInfo _clipNameField;
        private static bool _animatorAccessPrepared;
        private static bool _permanentTweaksApplied;
        private static bool _coroutineStarted;

        private static Dictionary<string, float> _animSpeedByState;
        private static readonly Dictionary<object, int> _originalFps = new Dictionary<object, int>();
        private static string _lastLoggedState = "";
        private static string _lastLoggedClip = "";
        private static string _lastAppliedState;
        private static object _lastAppliedClip;

        // ---- runtime : projectiles ----
        private static Type _rb2dType;
        private static PropertyInfo _rb2dVelocityProp;
        private static Action<object, Vector2> _setVelocity;
        private static Func<object, Vector2>   _getVelocity;
        private static string _lastProcessedSpawnState = "";
        private static GameObject _sicklePrefabCached;
        private static bool _prefabWarmed;

        private static Type _damageHeroType;
        private static FieldInfo _damageDealtField;

        private static readonly Dictionary<string, float[]> _angleCache = new Dictionary<string, float[]>();
        private static readonly Queue<float> _spawnTimes = new Queue<float>(128);

        // ---- runtime : phases / variations ----
        private static int _currentPhase = 1;
        private static Transform _cachedPlayer;
        private static bool _variationPicked;
        private static ProjectileVariation _currentVariation = ProjectileVariation.Fan5;
        private static ProjectileVariation _lastRolledVariation = ProjectileVariation.Fan5;
        private static float _lastThrowStateTime = -999f;
        private static float _lastAimedSpawnTime = -999f;

        // ---- runtime : last stand ----
        private static bool    _lastStandUsed;
        private static bool    _lastStandActive;
        private static float   _lastStandEndTime;
        private static float   _lastStandTriggerScheduledAt = -1f;
        private static Vector3 _lastStandCorner;
        private static int     _lastStandBurstIndex;
        private static string[] _lastStandHoldStates;
        private static int     _lastStandHoldIdx;
        private static float   _lastStandGroundY;
        private static float   _lastStandNextStateChange;

        // ---- runtime : visual telegraph ----
        private static object _bossSprite;
        private static PropertyInfo _spriteColorProp;
        private static Color _originalSpriteColor = Color.white;
        private static bool _originalSpriteColorCached;
        private static bool _telegraphActive;
        private static readonly Dictionary<ProjectileVariation, Color> _variationColors =
            new Dictionary<ProjectileVariation, Color>();

        // ---- runtime : HP reading ----
        private static object _healthManager;
        private static FieldInfo _hpField;
        private static bool _healthManagerCached;
        private static bool _healthManagerWarned;

        // ---- runtime : scream ----
        private static AudioClip _screamClip;
        private static bool _screamClipResolved;
        private static bool _screamActive;
        private static float _screamEndTime;

        // ---- runtime : transition patching ----
        private static FsmState _patchedState;
        private static readonly List<KeyValuePair<FsmTransition, string>> _patchedTransitions =
            new List<KeyValuePair<FsmTransition, string>>();

        private static int _wallDiveChainCount;

        // ---- runtime : CSV caches ----
        private static string _cachedThrowCsv;
        private static string[] _cachedThrowSubstrings;
        private static string _cachedPreserveCsv;
        private static string[] _cachedPreserveSubstrings;

        private static readonly float[] Angles_Stagger_L   = { 200f, 180f, 160f };
        private static readonly float[] Angles_Stagger_R   = { -20f, 0f, 20f };
        private static readonly float[] Angles_AimedFan_L  = { 195f, 165f };
        private static readonly float[] Angles_AimedFan_R  = { -15f, 15f };

        private static MethodInfo _finishMethod;
        private static bool _finishMethodChecked;

        // =====================================================================

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            cfgEnabled = Config.Bind("General", "Enabled", true, "Master toggle.");
            cfgLogDiagnostics = Config.Bind("General", "LogDiagnostics", false, "Verbose log. KEEP FALSE on Android.");
            cfgLogClipChanges = Config.Bind("General", "LogClipChanges", false, "Log state+clip transitions.");
            cfgLogStructureEvents = Config.Bind("General", "LogStructureEvents", true, "One-time structural events.");

            cfgRecoilSpeed = Config.Bind("AnimationSpeed", "RecoilSpeed", 1.8f, "Slash End / Cyclone Recoil / Spin Recoil.");
            cfgRoarAnticSpeed = Config.Bind("AnimationSpeed", "RoarAnticSpeed", 1.4f, "P2/P3 Roar Antic.");
            cfgAnticSpeed = Config.Bind("AnimationSpeed", "AnticSpeed", 1.4f, "All telegraphs.");
            cfgAttackSpeed = Config.Bind("AnimationSpeed", "AttackSpeed", 1.25f, "Attack execution states.");
            cfgMovementSpeed = Config.Bind("AnimationSpeed", "MovementSpeed", 1.4f, "Movement / Dash / Evade.");
            cfgStunRatio = Config.Bind("Timing", "StunRatio", 1.0f, "Stun Start duration multiplier.");
            cfgP3VanillaSpeed = Config.Bind("AnimationSpeed", "P3VanillaSpeed", true,
                "If true, ALL Phase 3 states play at vanilla (1.0x) speed.");
            cfgP3SpeedMultiplier = Config.Bind("AnimationSpeed", "P3SpeedMultiplier", 1.0f,
                "Extra multiplier in Phase 3 (only used when P3VanillaSpeed = false).");
            cfgMinSpeedMult = Config.Bind("AnimationSpeed", "MinMultiplier", 0.5f,
                new ConfigDescription("Hard lower clamp for any animation multiplier (0 disables).",
                    new AcceptableValueRange<float>(0f, 5f), Array.Empty<object>()));
            cfgMaxSpeedMult = Config.Bind("AnimationSpeed", "MaxMultiplier", 3.0f,
                new ConfigDescription("Hard upper clamp for any animation multiplier (0 disables).",
                    new AcceptableValueRange<float>(0f, 10f), Array.Empty<object>()));

            cfgDisableBlock = Config.Bind("Behavior", "DisableBlock", true, "Rewire Block to Attack Choice.");
            cfgDisableForceEvade = Config.Bind("Behavior", "DisableForceEvade", true, "Rewire Force Evade to Attack Choice.");
            cfgRemoveContactDamage = Config.Bind("Behavior", "RemoveContactDamage", true, "Disable root DamageHero.");
            cfgSkipPatchInP3 = Config.Bind("Behavior", "SkipTransitionPatchesInP3", true,
                "Disable Wall Dive Loop and Feint transition patches once Phase 3 begins.");

            cfgProjectilesEnabled = Config.Bind("Projectiles", "Enabled", true, "Master toggle for extras.");
            cfgProjectileSpeed = Config.Bind("Projectiles", "ProjectileSpeed", 28.0f, "Velocity for mod-fired projectiles.");
            cfgAirSicklesBothSides = Config.Bind("Projectiles", "AirSicklesBothSides", true, "Mirror Air Sickles.");
            cfgThrowL_Angles = Config.Bind("Projectiles", "ThrowL_Angles", "205,192,180,167", "Land-throw fan L.");
            cfgThrowR_Angles = Config.Bind("Projectiles", "ThrowR_Angles", "-25,-12,0,12", "Land-throw fan R.");
            cfgAirSickles_Angles = Config.Bind("Projectiles", "AirSickles_Angles", "-30,-10,10", "Air Sickles angles.");
            cfgProjectileLifetime = Config.Bind("Projectiles", "ProjectileLifetime", 3.0f, "Auto-destroy delay.");
            cfgMaxLiveExtras = Config.Bind("Projectiles", "MaxSpawnsPer2s", 100, "Safety spawn cap.");
            cfgForceProjectileDamage = Config.Bind("Projectiles", "ForceDamage", 2,
                "If > 0, force this damage value on every mod-spawned sickle. " +
                "2 = vanilla sickle damage, -1 = leave untouched.");

            cfgP1Variations = Config.Bind("Phases", "P1Variations", "fan5,stagger4,radial", "P1 pool.");
            cfgP2Variations = Config.Bind("Phases", "P2Variations", "radial,stagger4,skyrain,delayedwall", "P2 pool.");
            cfgP3Variations = Config.Bind("Phases", "P3Variations", "boomerang,p3combo,skyrain,delayedwall", "P3 pool.");

            cfgStaggerDelay = Config.Bind("Variations", "StaggerDelay", 0.12f, "Stagger shot delay.");
            cfgRadialCount = Config.Bind("Variations", "RadialCount", 4, "Radial semi-circle count.");
            cfgBoomerangDelay = Config.Bind("Variations", "BoomerangDelay", 0.70f, "Boomerang reverse delay.");
            cfgBoomerangReturnMul = Config.Bind("Variations", "BoomerangReturnMul", 0.9f, "Boomerang return multiplier.");
            cfgAimedSpeed = Config.Bind("Variations", "AimedSpeed", 32.0f, "Aimed sickle speed.");
            cfgAimedCooldown = Config.Bind("Variations", "AimedCooldown", 0.25f,
                new ConfigDescription("Seconds between aimed-at-player shots inside one throw.",
                    new AcceptableValueRange<float>(0f, 2f), Array.Empty<object>()));
            cfgBoomerangShotCount = Config.Bind("Variations", "BoomerangShotCount", 3,
                new ConfigDescription("Number of shots per boomerang volley.",
                    new AcceptableValueRange<int>(1, 8), Array.Empty<object>()));
            cfgBoomerangSpread = Config.Bind("Variations", "BoomerangSpread", 40.0f,
                new ConfigDescription("Total angular spread for boomerang.",
                    new AcceptableValueRange<float>(0f, 180f), Array.Empty<object>()));

            cfgWallDistance = Config.Bind("Variations.DelayedWall", "WallDistance", 20.0f, "Distance to wall.");
            cfgWallSpacing = Config.Bind("Variations.DelayedWall", "WallSpacing", 1.2f, "Vertical spacing.");
            cfgWallHoldTime = Config.Bind("Variations.DelayedWall", "HoldTime", 2.0f, "Wall hold seconds.");
            cfgWallStagger = Config.Bind("Variations.DelayedWall", "StaggerBetweenShots", 0.6f, "Launch stagger.");
            cfgWallLaunchSpeed = Config.Bind("Variations.DelayedWall", "LaunchSpeed", 22.0f, "Launch speed.");

            cfgSkyRainCount = Config.Bind("Variations.SkyRain", "ShotCount", 5, "Falling shots.");
            cfgSkyRainHeight = Config.Bind("Variations.SkyRain", "Height", 14.0f, "Spawn height above boss.");
            cfgSkyRainSpan = Config.Bind("Variations.SkyRain", "Span", 16.0f, "Horizontal spread.");
            cfgSkyRainFallSpeed = Config.Bind("Variations.SkyRain", "FallSpeed", 12.0f, "Downward speed.");
            cfgSkyRainMaxY = Config.Bind("Variations.SkyRain", "MaxSpawnY", 40f,
                "Hard ceiling for Sky Rain spawn Y.");

            cfgScreamEnabled = Config.Bind("Scream", "Enabled", true, "Fake scream on Sky Rain.");
            cfgScreamDuration = Config.Bind("Scream", "Duration", 0.8f, "Scream duration (sec).");
            cfgScreamSlowdown = Config.Bind("Scream", "SlowdownMultiplier", 0.35f,
                "Anim speed during scream. Only affects throw-related states.");
            cfgScreamClipName = Config.Bind("Scream", "ClipName", "carmelita_scream", "AudioClip name.");
            cfgScreamWhiteStrength = Config.Bind("Scream", "WhiteFlashStrength", 0.9f, "White flash strength.");

            cfgFeintEnabled = Config.Bind("Attacks.Feint", "Enabled", true, "Feint toggle.");
            cfgFeintActiveInPhases = Config.Bind("Attacks.Feint", "ActiveInPhases", "P2", "Feint phases.");
            cfgFeintChance = Config.Bind("Attacks.Feint", "TriggerChancePercent", 30f, "Feint chance %.");
            cfgFeintFromStates = Config.Bind("Attacks.Feint", "FromStates", "Slash Antic", "Feint source states.");
            cfgFeintToStates = Config.Bind("Attacks.Feint", "ToStates", "Throw Antic,Cyclone Antic,Spin Antic", "Feint targets.");

            cfgWallDiveLoopEnabled = Config.Bind("Attacks.WallDiveLoop", "Enabled", true, "Wall Dive chains.");
            cfgWallDiveLoopActiveInPhases = Config.Bind("Attacks.WallDiveLoop", "ActiveInPhases", "P1", "Phases.");
            cfgWallDiveLoopMax = Config.Bind("Attacks.WallDiveLoop", "MaxConsecutiveDives", 3, "Max dives.");
            cfgWallDiveSpeed = Config.Bind("Attacks.WallDiveLoop", "AnimationSpeedMultiplier", 1.0f, "Anim FPS multiplier.");

            cfgTelegraphEnabled = Config.Bind("Telegraph", "Enabled", true, "Tint telegraph.");
            cfgTelegraphThrowStates = Config.Bind("Telegraph", "ThrowSequenceSubstrings", "Throw,Rethrow,Air Sickles", "Throw states.");
            cfgTelegraphTintStrength = Config.Bind("Telegraph", "TintStrength", 0.90f, "Tint strength.");
            cfgPreserveThrowStates = Config.Bind("Telegraph", "PreserveThrowSequenceStates", "Attack Choice", "Hub states.");
            cfgDoubleThrowWindow = Config.Bind("Telegraph", "DoubleThrowWindow", 0.8f,
                "Seconds a variation pick survives a brief detour through a non-throw state. " +
                "Prevents the telegraph color from being lost if she repositions between antic and throw.");

            cfgColorFan         = Config.Bind("Telegraph.Colors", "Fan5",        "1.00,1.00,1.00", "Fan5 (white).");
            cfgColorStagger     = Config.Bind("Telegraph.Colors", "Stagger4",    "1.00,1.00,0.00", "Stagger (yellow).");
            cfgColorAimed       = Config.Bind("Telegraph.Colors", "Aimed",       "0.00,1.00,1.00", "Aimed (cyan).");
            cfgColorRadial      = Config.Bind("Telegraph.Colors", "Radial",      "1.00,0.40,0.00", "Radial (orange).");
            cfgColorBoomerang   = Config.Bind("Telegraph.Colors", "Boomerang",   "0.00,1.00,0.00", "Boomerang (lime).");
            cfgColorCombo       = Config.Bind("Telegraph.Colors", "P3Combo",     "1.00,0.00,1.00", "P3Combo (magenta).");
            cfgColorDelayedWall = Config.Bind("Telegraph.Colors", "DelayedWall", "1.00,0.10,0.10", "DelayedWall (red).");
            cfgColorAirFan      = Config.Bind("Telegraph.Colors", "AirFan",      "0.50,0.00,0.80", "Air Sickles (violet).");
            cfgColorSkyRain     = Config.Bind("Telegraph.Colors", "SkyRain",     "0.35,0.70,1.00", "Sky Rain (blue).");

            // ---- LAST STAND ----
            cfgLastStandEnabled = Config.Bind("LastStand", "Enabled", true,
                "Trigger a scripted Last Stand once her HP drops to the threshold.");
            cfgLastStandHpThreshold = Config.Bind("LastStand", "HpThreshold", 500,
                "HP threshold (inclusive) that triggers the Last Stand.");
            cfgLastStandTriggerDelay = Config.Bind("LastStand", "TriggerDelay", 0.65f,
                "Seconds after HP<=threshold before the Last Stand starts.");
            cfgLastStandDuration = Config.Bind("LastStand", "Duration", 10.0f,
                "How long the Last Stand lasts (seconds).");
            cfgLastStandCornerOffsetXLeftWall = Config.Bind("LastStand", "CornerOffsetXLeftWall", 7f,
                "X offset used when she triggers on the LEFT side of the player. " +
                "Positive = toward the player (away from the left wall).");
            cfgLastStandCornerOffsetXRightWall = Config.Bind("LastStand", "CornerOffsetXRightWall", -7f,
                "X offset used when she triggers on the RIGHT side of the player. " +
                "Negative = toward the player (away from the right wall).");
            cfgLastStandCornerOffsetY = Config.Bind("LastStand", "CornerOffsetY", 2f,
                "Y offset (from her trigger position) to teleport to at the start.");
            cfgLastStandPattern = Config.Bind("LastStand", "ProjectilePattern", "3,4,3,4,3,4,3,4,3",
                "Comma-separated projectile counts per burst. Loop until duration ends.");
            cfgLastStandBurstDelay = Config.Bind("LastStand", "BurstDelay", 1.0f,
                new ConfigDescription("Seconds between bursts.",
                    new AcceptableValueRange<float>(0.3f, 5f), Array.Empty<object>()));
            cfgLastStandProjectileSpeed = Config.Bind("LastStand", "ProjectileSpeed", 20f,
                "Speed of Last Stand projectiles.");
            cfgLastStandBurstSpread = Config.Bind("LastStand", "BurstSpread", 35f,
                new ConfigDescription("Total angular spread of each burst (degrees).",
                    new AcceptableValueRange<float>(0f, 180f), Array.Empty<object>()));
            cfgLastStandHoldStates = Config.Bind("LastStand", "HoldStates",
                "Throw L,Throw R",
                "Comma-separated FSM states cycled during the Last Stand for the throw animation.");
            cfgLastStandExitState = Config.Bind("LastStand", "ExitState", "Wall Dive",
                "FSM state forced immediately after the Last Stand ends.");

            RebuildVariationColors();
            RebuildAnimationSpeedMap();
            CacheRigidbodyAccess();
            CacheDamageAccess();

            try
            {
                var harmony = new Harmony();
                var fsmEnter = AccessTools.Method(typeof(FsmState), "OnEnter");
                if (fsmEnter != null)
                {
                    var postfix = AccessTools.Method(typeof(KarmelitaTweaksPlugin), nameof(OnFsmStateEntered));
                    harmony.Patch(fsmEnter, postfix: postfix);
                }
                var spawnEnter = AccessTools.Method(typeof(SpawnObjectFromGlobalPool), "OnEnter");
                if (spawnEnter != null)
                {
                    var prefix = AccessTools.Method(typeof(KarmelitaTweaksPlugin), nameof(OnSpawnObjectPrefix));
                    harmony.Patch(spawnEnter, prefix: prefix);
                }
            }
            catch (Exception ex) { Log.LogError("Patching failed: " + ex); }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ResetRuntimeState(scene.name); }

        private static void ResetRuntimeState(string sceneName)
        {
            try
            {
                _bossFsm = null;
                _bossAnimator = null;
                _getCurrentClipDelegate = null;
                _animCurrentClipProp = null;
                _animCurrentClipField = null;
                _clipFpsField = null;
                _clipNameProp = null;
                _clipNameField = null;
                _animatorAccessPrepared = false;
                _permanentTweaksApplied = false;
                _coroutineStarted = false;

                _variationPicked = false;
                _currentVariation = ProjectileVariation.Fan5;
                _lastRolledVariation = ProjectileVariation.Fan5;
                _lastThrowStateTime = -999f;
                _lastAimedSpawnTime = -999f;
                _lastProcessedSpawnState = "";
                _currentPhase = 1;

                _bossSprite = null;
                _spriteColorProp = null;
                _originalSpriteColorCached = false;
                _telegraphActive = false;

                _healthManager = null;
                _hpField = null;
                _healthManagerCached = false;
                _healthManagerWarned = false;

                _screamActive = false;
                _screamEndTime = -1f;

                _sicklePrefabCached = null;
                _prefabWarmed = false;

                _lastStandUsed = false;
                _lastStandActive = false;
                _lastStandEndTime = 0f;
                _lastStandTriggerScheduledAt = -1f;
                _lastStandCorner = Vector3.zero;
                _lastStandBurstIndex = 0;
                _lastStandHoldStates = null;
                _lastStandHoldIdx = 0;
                _lastStandGroundY = 0f;
                _lastStandNextStateChange = 0f;

                RestorePatchedTransitions();
                _wallDiveChainCount = 0;

                _lastAppliedState = null;
                _lastAppliedClip = null;
                _lastLoggedState = "";
                _lastLoggedClip = "";
                _cachedPlayer = null;
                _spawnTimes.Clear();

                Log.LogInfo("[Karmelita] Runtime state reset on scene load: " + sceneName);
            }
            catch (Exception ex) { Log.LogWarning("[Karmelita] ResetRuntimeState failed: " + ex.Message); }
        }

        private static void LogStructure(string msg)
        {
            try
            {
                if (cfgLogStructureEvents != null && cfgLogStructureEvents.Value)
                    Log?.LogInfo("[Karmelita] " + msg);
            }
            catch { }
        }

        // =====================================================================
        //  Colors
        // =====================================================================

        private static void RebuildVariationColors()
        {
            _variationColors[ProjectileVariation.Fan5]        = ParseColor(cfgColorFan.Value);
            _variationColors[ProjectileVariation.Stagger4]    = ParseColor(cfgColorStagger.Value);
            _variationColors[ProjectileVariation.Aimed]       = ParseColor(cfgColorAimed.Value);
            _variationColors[ProjectileVariation.Radial]      = ParseColor(cfgColorRadial.Value);
            _variationColors[ProjectileVariation.Boomerang]   = ParseColor(cfgColorBoomerang.Value);
            _variationColors[ProjectileVariation.P3Combo]     = ParseColor(cfgColorCombo.Value);
            _variationColors[ProjectileVariation.DelayedWall] = ParseColor(cfgColorDelayedWall.Value);
            _variationColors[ProjectileVariation.AirFan]      = ParseColor(cfgColorAirFan.Value);
            _variationColors[ProjectileVariation.SkyRain]     = ParseColor(cfgColorSkyRain.Value);
        }

        private static Color ParseColor(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return Color.white;
            var parts = csv.Split(',');
            if (parts.Length < 3) return Color.white;
            float r, g, b;
            if (!float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out r)) r = 1f;
            if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out g)) g = 1f;
            if (!float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out b)) b = 1f;
            return new Color(r, g, b, 1f);
        }

        private static void CacheRigidbodyAccess()
        {
            _rb2dType = AccessTools.TypeByName("UnityEngine.Rigidbody2D");
            if (_rb2dType == null) return;

            const BindingFlags F = BindingFlags.Public | BindingFlags.Instance;
            _rb2dVelocityProp = _rb2dType.GetProperty("velocity", F);
            if (_rb2dVelocityProp == null) return;

            var setter = _rb2dVelocityProp.GetSetMethod(true);
            if (setter != null)
            {
                try
                {
                    _setVelocity = (Action<object, Vector2>)Delegate.CreateDelegate(
                        typeof(Action<object, Vector2>), setter);
                }
                catch { _setVelocity = null; }
            }

            var getter = _rb2dVelocityProp.GetGetMethod(true);
            if (getter != null)
            {
                try
                {
                    _getVelocity = (Func<object, Vector2>)Delegate.CreateDelegate(
                        typeof(Func<object, Vector2>), getter);
                }
                catch { _getVelocity = null; }
            }
        }

        private static void CacheDamageAccess()
        {
            _damageHeroType = AccessTools.TypeByName("DamageHero");
            if (_damageHeroType != null)
                _damageDealtField = _damageHeroType.GetField("damageDealt",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static Transform GetPlayer()
        {
            if (_cachedPlayer != null) return _cachedPlayer;
            try
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _cachedPlayer = go.transform;
            }
            catch { }
            return _cachedPlayer;
        }

        // =====================================================================
        //  HP reading
        // =====================================================================

        private static int ReadBossHp()
        {
            if (_bossFsm == null) return -1;

            if (!_healthManagerCached)
            {
                try
                {
                    var hmType = AccessTools.TypeByName("HealthManager");
                    if (hmType != null)
                    {
                        var hm = _bossFsm.gameObject.GetComponent(hmType);
                        if (hm == null)
                            hm = _bossFsm.gameObject.GetComponentInChildren(hmType, true);
                        if (hm == null)
                        {
                            var parents = _bossFsm.gameObject.GetComponentsInParent(hmType, true);
                            if (parents != null && parents.Length > 0) hm = parents[0];
                        }

                        if (hm != null)
                        {
                            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                            _hpField = hmType.GetField("hp", F)
                                    ?? hmType.GetField("HP", F)
                                    ?? hmType.GetField("health", F)
                                    ?? hmType.GetField("currentHp", F)
                                    ?? hmType.GetField("currentHealth", F);
                            if (_hpField != null)
                            {
                                _healthManager = hm;
                                _healthManagerCached = true;
                                Log.LogInfo("[Karmelita] HealthManager resolved (field='" + _hpField.Name + "').");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_healthManagerWarned)
                    {
                        _healthManagerWarned = true;
                        Log.LogWarning("[Karmelita] HP lookup failed: " + ex.Message);
                    }
                }
            }

            if (_healthManager == null || _hpField == null) return -1;
            try { return Convert.ToInt32(_hpField.GetValue(_healthManager)); }
            catch { return -1; }
        }

        // =====================================================================
        //  CSV
        // =====================================================================

        private static string[] SplitCsv(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return Array.Empty<string>();
            var parts = csv.Split(',');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        private static bool IsThrowSequenceState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return false;
            string csv = cfgTelegraphThrowStates.Value;
            if (!string.Equals(csv, _cachedThrowCsv, StringComparison.Ordinal))
            {
                _cachedThrowCsv = csv;
                _cachedThrowSubstrings = SplitCsv(csv);
            }
            var arr = _cachedThrowSubstrings;
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
                if (stateName.IndexOf(arr[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool IsPreserveThrowState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return false;
            string csv = cfgPreserveThrowStates.Value;
            if (!string.Equals(csv, _cachedPreserveCsv, StringComparison.Ordinal))
            {
                _cachedPreserveCsv = csv;
                _cachedPreserveSubstrings = SplitCsv(csv);
            }
            var arr = _cachedPreserveSubstrings;
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
                if (string.Equals(stateName, arr[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsActiveInCurrentPhase(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return false;
            foreach (var raw in csv.Split(','))
            {
                var t = raw.Trim();
                if (t.Length == 0) continue;
                string num = t;
                if (num.Length > 0 && char.IsLetter(num[0])) num = num.Substring(1);
                int val;
                if (int.TryParse(num, out val) && val == _currentPhase) return true;
            }
            return false;
        }

        // =====================================================================
        //  Transition patching
        // =====================================================================

        private static void PatchTransitionsTo(FsmState state, string targetStateName)
        {
            if (state == null) return;
            if (_bossFsm == null || _bossFsm.Fsm == null) return;
            if (string.IsNullOrEmpty(targetStateName)) return;
            if (_bossFsm.Fsm.GetState(targetStateName) == null)
            {
                Log.LogWarning("[Karmelita] Target state not found: " + targetStateName);
                return;
            }
            RestorePatchedTransitions();
            var trans = state.Transitions;
            if (trans == null || trans.Length == 0) return;
            _patchedState = state;
            foreach (var t in trans)
            {
                if (t == null) continue;
                _patchedTransitions.Add(new KeyValuePair<FsmTransition, string>(t, t.ToState));
                t.ToState = targetStateName;
            }
        }

        private static void RestorePatchedTransitions()
        {
            if (_patchedTransitions.Count > 0)
            {
                foreach (var item in _patchedTransitions)
                    if (item.Key != null) item.Key.ToState = item.Value;
                _patchedTransitions.Clear();
            }
            _patchedState = null;
        }

        private static bool ShouldSkipPatches()
        {
            if (_lastStandActive) return true;
            if (cfgSkipPatchInP3 != null && cfgSkipPatchInP3.Value && _currentPhase >= 3) return true;
            return false;
        }

        // =====================================================================
        //  LAST STAND
        // =====================================================================

        private static void CheckLastStandTrigger()
        {
            if (!cfgLastStandEnabled.Value) return;
            if (_lastStandUsed || _lastStandActive) return;
            if (_bossFsm == null) return;

            int hp = ReadBossHp();
            if (hp < 0) return;

            if (hp > cfgLastStandHpThreshold.Value)
            {
                _lastStandTriggerScheduledAt = -1f;
                return;
            }

            if (_lastStandTriggerScheduledAt < 0f)
            {
                _lastStandTriggerScheduledAt = Time.time + Mathf.Max(0f, cfgLastStandTriggerDelay.Value);
                LogStructure("Last Stand scheduled (hp=" + hp + ")");
                return;
            }

            if (Time.time >= _lastStandTriggerScheduledAt)
            {
                if (Instance != null) Instance.StartCoroutine(LastStandRoutine());
            }
        }

        private static void ComputeLastStandCorner(Vector3 from)
        {
            Transform player = GetPlayer();
            bool onLeftHalf;

            if (player != null)
                onLeftHalf = from.x < player.position.x;
            else
                onLeftHalf = cfgLastStandCornerOffsetXLeftWall.Value < 0f; // fallback by sign

            float offsetX = onLeftHalf
                ? cfgLastStandCornerOffsetXLeftWall.Value
                : cfgLastStandCornerOffsetXRightWall.Value;

            _lastStandCorner = new Vector3(
                from.x + offsetX,
                from.y + cfgLastStandCornerOffsetY.Value,
                from.z);

            LogStructure("Last Stand corner chosen (side=" + (onLeftHalf ? "LEFT" : "RIGHT")
                + ", offsetX=" + offsetX + ") -> " + _lastStandCorner);
        }

        private static void LockBossForLastStand()
        {
            if (!_lastStandActive || _bossFsm == null) return;

            _bossFsm.transform.position = _lastStandCorner;

            var rb = GetRigidbodyOf(_bossFsm.gameObject);
            if (rb != null) SetVelocity(rb, Vector2.zero);

            if (_lastStandHoldStates == null || _lastStandHoldStates.Length == 0) return;
            if (Time.time < _lastStandNextStateChange) return;

            string cur = _bossFsm.Fsm != null ? _bossFsm.Fsm.ActiveStateName : "";
            bool isThrow = !string.IsNullOrEmpty(cur)
                && (cur.StartsWith("Throw", StringComparison.Ordinal)
                 || cur.StartsWith("Rethrow", StringComparison.Ordinal)
                 || cur.StartsWith("Air Sickles", StringComparison.Ordinal));

            if (isThrow) return;

            string target = _lastStandHoldStates[_lastStandHoldIdx % _lastStandHoldStates.Length];
            _lastStandHoldIdx++;
            if (_bossFsm.Fsm.GetState(target) != null)
            {
                try { _bossFsm.Fsm.SetState(target); } catch { }
            }
            _lastStandNextStateChange = Time.time + 0.4f;
        }

        private static IEnumerator LastStandRoutine()
        {
            if (_lastStandUsed || _lastStandActive) yield break;

            _lastStandUsed = true;
            _lastStandActive = true;
            _lastStandTriggerScheduledAt = -1f;
            _lastStandBurstIndex = 0;
            _lastStandHoldIdx = 0;
            _lastStandNextStateChange = 0f;

            _lastStandGroundY = _bossFsm != null ? _bossFsm.transform.position.y : 0f;

            try
            {
                _lastStandHoldStates = SplitCsv(cfgLastStandHoldStates.Value);
                if (_lastStandHoldStates == null || _lastStandHoldStates.Length == 0)
                    _lastStandHoldStates = new[] { "Throw L", "Throw R" };

                RestorePatchedTransitions();
                _wallDiveChainCount = 0;

                Vector3 from = _bossFsm != null ? _bossFsm.transform.position : Vector3.zero;
                ComputeLastStandCorner(from);

                Log.LogInfo("[Karmelita] Last Stand START. Corner=" + _lastStandCorner
                    + " restoreY=" + _lastStandGroundY);

                if (_bossFsm != null)
                {
                    _bossFsm.transform.position = _lastStandCorner;
                    var rb = GetRigidbodyOf(_bossFsm.gameObject);
                    if (rb != null) SetVelocity(rb, Vector2.zero);
                }

                yield return new WaitForSeconds(0.1f);

                _lastStandEndTime = Time.time + Mathf.Max(1f, cfgLastStandDuration.Value);

                if (_sicklePrefabCached == null)
                {
                    Log.LogWarning("[Karmelita] Last Stand: no prefab cached; holding without fire.");
                    while (Time.time < _lastStandEndTime) yield return null;
                    yield break;
                }

                int[] pattern = ParseIntPattern(cfgLastStandPattern.Value);
                if (pattern == null || pattern.Length == 0) pattern = new[] { 3, 4 };

                float burstDelay = Mathf.Max(0.3f, cfgLastStandBurstDelay.Value);
                float speed = Mathf.Max(1f, cfgLastStandProjectileSpeed.Value);
                float spread = Mathf.Clamp(cfgLastStandBurstSpread.Value, 0f, 180f);

                while (Time.time < _lastStandEndTime)
                {
                    int count = pattern[_lastStandBurstIndex % pattern.Length];
                    _lastStandBurstIndex++;

                    try { SpawnLastStandBurst(count, speed, spread); }
                    catch (Exception ex) { Log.LogWarning("[Karmelita] Last Stand burst failed: " + ex.Message); }

                    float wait = Mathf.Min(burstDelay, Mathf.Max(0.016f, _lastStandEndTime - Time.time));
                    yield return new WaitForSeconds(wait);
                }
            }
            finally
            {
                if (_lastStandActive)
                {
                    _lastStandActive = false;
                    Log.LogInfo("[Karmelita] Last Stand cleanup (finally).");
                }

                if (_bossFsm != null)
                {
                    try
                    {
                        Vector3 p = _bossFsm.transform.position;
                        _bossFsm.transform.position = new Vector3(p.x, _lastStandGroundY, p.z);

                        var rb = GetRigidbodyOf(_bossFsm.gameObject);
                        if (rb != null) SetVelocity(rb, Vector2.zero);
                    }
                    catch { }

                    if (Instance != null)
                        Instance.StartCoroutine(LastStandExitRoutine());
                }

                _lastStandCorner = Vector3.zero;
            }
        }

        private static IEnumerator LastStandExitRoutine()
        {
            yield return new WaitForSeconds(0.2f);

            if (_bossFsm == null) yield break;

            var rb = GetRigidbodyOf(_bossFsm.gameObject);
            if (rb != null) SetVelocity(rb, Vector2.zero);

            string exit = cfgLastStandExitState.Value;
            if (!string.IsNullOrEmpty(exit) && _bossFsm.Fsm != null && _bossFsm.Fsm.GetState(exit) != null)
            {
                try
                {
                    _bossFsm.Fsm.SetState(exit);
                    Log.LogInfo("[Karmelita] Last Stand exited to state: " + exit);
                }
                catch (Exception ex) { Log.LogWarning("[Karmelita] Last Stand exit failed: " + ex.Message); }
            }

            _lastAppliedState = null;
            _lastAppliedClip = null;
        }

        private static void SpawnLastStandBurst(int count, float speed, float spread)
        {
            if (_sicklePrefabCached == null || _bossFsm == null) return;
            if (count <= 0) return;

            Vector3 origin = _bossFsm.transform.position + new Vector3(0f, 1.5f, 0f);

            Transform player = GetPlayer();
            float baseAngle;
            if (player != null)
            {
                Vector2 to = (Vector2)(player.position - origin);
                baseAngle = to.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg
                    : 180f;
            }
            else baseAngle = 180f;

            if (count == 1)
            {
                SpawnOneAtWorldPos(_sicklePrefabCached, origin, baseAngle, speed);
                return;
            }

            float step = spread / (count - 1);
            float start = baseAngle - spread * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float a = start + i * step;
                SpawnOneAtWorldPos(_sicklePrefabCached, origin, a, speed);
            }
        }

        private static void SpawnOneAtWorldPos(GameObject prefab, Vector3 pos, float angleDeg, float speed)
        {
            try
            {
                if (!CanSpawnExtra())
                {
                    if (cfgLogDiagnostics != null && cfgLogDiagnostics.Value)
                        Log.LogWarning("[Karmelita] Spawn cap reached; skipping projectile.");
                    return;
                }
                _spawnTimes.Enqueue(Time.time);
                GameObject clone = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                clone.name = prefab.name;
                if (_rb2dType == null) { UnityEngine.Object.Destroy(clone, 0.1f); return; }
                var rb = clone.GetComponent(_rb2dType) ?? clone.GetComponentInChildren(_rb2dType, true);
                if (rb == null) { UnityEngine.Object.Destroy(clone, 0.1f); return; }
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 vel = new Vector2(Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);
                SetVelocity(rb, vel);
                ApplyForcedDamage(clone);
                UnityEngine.Object.Destroy(clone, Mathf.Max(0.5f, cfgProjectileLifetime.Value));
            }
            catch { }
        }

        private static int[] ParseIntPattern(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return null;
            var parts = csv.Split(',');
            var list = new List<int>(parts.Length);
            foreach (var p in parts)
            {
                int v;
                if (int.TryParse(p.Trim(), out v) && v > 0) list.Add(v);
            }
            return list.ToArray();
        }

        // =====================================================================
        //  Spawn prefix
        // =====================================================================

        private static bool OnSpawnObjectPrefix(SpawnObjectFromGlobalPool __instance)
        {
            try
            {
                if (__instance == null) return true;

                var fsm = __instance.Fsm?.FsmComponent;
                if (fsm == null) return true;

                if (_bossFsm != null) { if (fsm != _bossFsm) return true; }
                else
                {
                    if (fsm.gameObject.name != BossName) return true;
                    if (fsm.FsmName != BossFsmName) return true;
                    _bossFsm = fsm;
                }

                if (!cfgProjectilesEnabled.Value) return true;

                string prefabName = __instance.gameObject?.Value != null
                    ? __instance.gameObject.Value.name : "";
                if (prefabName != SicklePrefabName) return true;

                GameObject prefab = __instance.gameObject?.Value;
                if (_sicklePrefabCached == null && prefab != null)
                {
                    _sicklePrefabCached = prefab;
                    LogStructure("Sickle prefab cached: " + prefab.name);
                    if (!_prefabWarmed && Instance != null)
                        Instance.StartCoroutine(WarmPrefabOnlyRoutine());
                }
                if (_sicklePrefabCached != null) prefab = _sicklePrefabCached;

                if (_lastStandActive) { SafeFinishAction(__instance); return false; }

                string stateName = fsm.Fsm?.ActiveStateName ?? "";
                if (string.IsNullOrEmpty(stateName)) return true;

                bool isThrowL = stateName == "Throw L" || stateName == "Rethrow L";
                bool isThrowR = stateName == "Throw R" || stateName == "Rethrow R";
                bool isAir = stateName == "Air Sickles" || stateName == "Air Sickles 2";
                if (!isThrowL && !isThrowR && !isAir) return true;

                if (_lastProcessedSpawnState == stateName) { SafeFinishAction(__instance); return false; }

                GameObject spawnPt = __instance.spawnPoint?.Value;
                if (prefab == null || spawnPt == null) return true;

                _lastProcessedSpawnState = stateName;

                if (!_variationPicked)
                {
                    _variationPicked = true;
                    _currentVariation = RollVariation(isAir);
                    _lastRolledVariation = _currentVariation;
                    _lastThrowStateTime = Time.time;
                    ApplyTelegraphTint(_currentVariation);
                }
                ExecuteVariation(_currentVariation, prefab, spawnPt,
                    isThrowL, isThrowR, isAir, stateName, cfgProjectileSpeed.Value);

                SafeFinishAction(__instance);
                return false;
            }
            catch (Exception ex) { Log.LogError("[Karmelita] OnSpawnObjectPrefix failed: " + ex); return true; }
        }

        private static void SafeFinishAction(FsmStateAction action)
        {
            try
            {
                if (!_finishMethodChecked)
                {
                    _finishMethodChecked = true;
                    _finishMethod = typeof(FsmStateAction).GetMethod("Finish",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                }
                if (_finishMethod != null) _finishMethod.Invoke(action, null);
            }
            catch { }
        }

        // =====================================================================
        //  Variation roll
        // =====================================================================

        private static ProjectileVariation RollVariation(bool isAir)
        {
            if (isAir) return ProjectileVariation.AirFan;
            ProjectileVariation[] pool = GetVariationPool(_currentPhase);
            if (pool == null || pool.Length == 0) return ProjectileVariation.Fan5;

            bool excludeBoomerang = _lastRolledVariation == ProjectileVariation.Boomerang;
            if (excludeBoomerang && pool.Length > 1)
            {
                var filtered = new List<ProjectileVariation>(pool.Length);
                foreach (var v in pool)
                    if (v != ProjectileVariation.Boomerang) filtered.Add(v);
                if (filtered.Count > 0)
                    return filtered[UnityEngine.Random.Range(0, filtered.Count)];
            }
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private static ProjectileVariation[] GetVariationPool(int phase)
        {
            string csv;
            switch (phase)
            {
                case 2: csv = cfgP2Variations.Value; break;
                case 3: csv = cfgP3Variations.Value; break;
                default: csv = cfgP1Variations.Value; break;
            }
            return ParseVariations(csv);
        }

        private static ProjectileVariation[] ParseVariations(string csv)
        {
            var parts = SplitCsv(csv);
            var list = new List<ProjectileVariation>(parts.Length);
            foreach (var raw in parts)
            {
                switch (raw.ToLowerInvariant())
                {
                    case "fan5":
                    case "fan": list.Add(ProjectileVariation.Fan5); break;
                    case "stagger4":
                    case "stagger": list.Add(ProjectileVariation.Stagger4); break;
                    case "aimed": list.Add(ProjectileVariation.Aimed); break;
                    case "radial": list.Add(ProjectileVariation.Radial); break;
                    case "boomerang": list.Add(ProjectileVariation.Boomerang); break;
                    case "p3combo":
                    case "combo": list.Add(ProjectileVariation.P3Combo); break;
                    case "delayedwall":
                    case "wall":
                    case "delayed_wall": list.Add(ProjectileVariation.DelayedWall); break;
                    case "skyrain":
                    case "sky": list.Add(ProjectileVariation.SkyRain); break;
                }
            }
            return list.ToArray();
        }

        private static void ExecuteVariation(ProjectileVariation v, GameObject prefab, GameObject spawnPt,
                                             bool isLeft, bool isRight, bool isAir, string stateName, float speed)
        {
            switch (v)
            {
                case ProjectileVariation.Fan5:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else SpawnLandFan(prefab, spawnPt, isLeft, speed, stateName, false, 0f);
                    break;
                case ProjectileVariation.AirFan:
                    SpawnAirFan(prefab, spawnPt, speed, stateName);
                    break;
                case ProjectileVariation.Stagger4:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else Instance.StartCoroutine(StaggerRoutine(prefab, spawnPt, isLeft, speed, stateName));
                    break;
                case ProjectileVariation.Aimed:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else
                    {
                        SpawnAimedFan(prefab, spawnPt, isLeft, speed, stateName);
                        float now = Time.time;
                        if (now - _lastAimedSpawnTime >= cfgAimedCooldown.Value)
                        {
                            _lastAimedSpawnTime = now;
                            SpawnAimedAtPlayer(prefab, spawnPt, cfgAimedSpeed.Value, stateName + "_aimed");
                        }
                    }
                    break;
                case ProjectileVariation.Radial:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else SpawnRadialSemi(prefab, spawnPt, speed, cfgRadialCount.Value, isLeft, stateName);
                    break;
                case ProjectileVariation.Boomerang:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else SpawnBoomerangFan(prefab, spawnPt, isLeft, speed, stateName);
                    break;
                case ProjectileVariation.P3Combo:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else Instance.StartCoroutine(P3ComboRoutine(prefab, spawnPt, isLeft, speed, stateName));
                    break;
                case ProjectileVariation.DelayedWall:
                    if (isAir) SpawnAirFan(prefab, spawnPt, speed, stateName);
                    else Instance.StartCoroutine(DelayedWallRoutine(prefab, spawnPt, isLeft, stateName));
                    break;
                case ProjectileVariation.SkyRain:
                    Instance.StartCoroutine(SkyRainRoutine(prefab, stateName));
                    break;
            }
        }

        // =====================================================================
        //  Spawn helpers
        // =====================================================================

        private static void SpawnLandFan(GameObject prefab, GameObject spawnPt, bool isLeft,
                                         float speed, string tag, bool boomerang, float boomDelay)
        {
            float[] angles = ParseAngles(isLeft ? cfgThrowL_Angles.Value : cfgThrowR_Angles.Value);
            foreach (var a in angles) SpawnOne(prefab, spawnPt, a, speed, tag, boomerang, boomDelay);
        }

        private static void SpawnBoomerangFan(GameObject prefab, GameObject spawnPt, bool isLeft,
                                              float speed, string tag)
        {
            int count = Mathf.Clamp(cfgBoomerangShotCount.Value, 1, 8);
            float spread = Mathf.Clamp(cfgBoomerangSpread.Value, 0f, 180f);
            float centerAngle = isLeft ? 180f : 0f;
            float boomDelay = cfgBoomerangDelay.Value;

            if (count == 1)
            {
                SpawnOne(prefab, spawnPt, centerAngle, speed, tag + "_boom0", true, boomDelay);
                return;
            }

            float step = spread / (count - 1);
            float startAngle = centerAngle - spread * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * step;
                SpawnOne(prefab, spawnPt, angle, speed, tag + "_boom" + i, true, boomDelay);
            }
        }

        private static void SpawnAirFan(GameObject prefab, GameObject spawnPt, float speed, string tag)
        {
            float[] angles = ParseAngles(cfgAirSickles_Angles.Value);
            bool mirror = cfgAirSicklesBothSides.Value;
            foreach (var a in angles) SpawnOne(prefab, spawnPt, a, speed, tag);
            if (mirror)
                foreach (var a in angles) SpawnOne(prefab, spawnPt, MirrorAngle(a), speed, tag + "_mirror");
        }

        private static void SpawnAimedFan(GameObject prefab, GameObject spawnPt, bool isLeft,
                                          float speed, string tag)
        {
            float[] angles = isLeft ? Angles_AimedFan_L : Angles_AimedFan_R;
            foreach (var a in angles) SpawnOne(prefab, spawnPt, a, speed, tag);
        }

        private static void SpawnAimedAtPlayer(GameObject prefab, GameObject spawnPt, float speed, string tag)
        {
            Transform p = GetPlayer();
            if (p == null) return;
            Vector2 to = (Vector2)(p.position - spawnPt.transform.position);
            float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            SpawnOne(prefab, spawnPt, ang, speed, tag);
        }

        private static void SpawnRadialSemi(GameObject prefab, GameObject spawnPt,
                                            float speed, int count, bool isLeft, string tag)
        {
            if (count < 2) count = 2;
            float start = isLeft ? 90f : -90f;
            float end = isLeft ? 270f : 90f;
            float step = (end - start) / (count - 1);
            for (int i = 0; i < count; i++) SpawnOne(prefab, spawnPt, start + i * step, speed, tag + "_r" + i);
        }

        private static IEnumerator StaggerRoutine(GameObject prefab, GameObject spawnPt, bool isLeft,
                                                  float speed, string tag)
        {
            float[] angles = isLeft ? Angles_Stagger_L : Angles_Stagger_R;
            float d = Mathf.Max(0f, cfgStaggerDelay.Value);
            for (int i = 0; i < angles.Length; i++)
            {
                SpawnOne(prefab, spawnPt, angles[i], speed, tag + "_s" + i);
                if (i < angles.Length - 1 && d > 0f) yield return new WaitForSeconds(d);
            }
        }

        private static IEnumerator P3ComboRoutine(GameObject prefab, GameObject spawnPt, bool isLeft,
                                                  float speed, string tag)
        {
            float d = Mathf.Max(0f, cfgStaggerDelay.Value);
            float boomDelay = cfgBoomerangDelay.Value;
            float[] angles = isLeft ? Angles_Stagger_L : Angles_Stagger_R;
            for (int i = 0; i < angles.Length; i++)
            {
                SpawnOne(prefab, spawnPt, angles[i], speed, tag + "_boom" + i, true, boomDelay);
                if (i < angles.Length - 1 && d > 0f) yield return new WaitForSeconds(d);
            }
            yield return new WaitForSeconds(0.10f);
            SpawnAimedAtPlayer(prefab, spawnPt, cfgAimedSpeed.Value, tag + "_p3aimed");
        }

        // =====================================================================
        //  Delayed Wall
        // =====================================================================

        private static GameObject SpawnStationary(GameObject prefab, Vector3 worldPos, string tag, float lifetime)
        {
            try
            {
                if (!CanSpawnExtra()) { LogStructure("SpawnStationary rejected by spawn cap"); return null; }
                _spawnTimes.Enqueue(Time.time);
                GameObject clone = UnityEngine.Object.Instantiate(prefab, worldPos, Quaternion.identity);
                clone.name = prefab.name;
                if (_rb2dType == null) { UnityEngine.Object.Destroy(clone, 0.1f); return null; }
                var rb = clone.GetComponent(_rb2dType) ?? clone.GetComponentInChildren(_rb2dType, true);
                if (rb == null) { UnityEngine.Object.Destroy(clone, 0.1f); return null; }
                SetVelocity(rb, Vector2.zero);
                ApplyForcedDamage(clone);
                UnityEngine.Object.Destroy(clone, Mathf.Max(1f, lifetime));
                return clone;
            }
            catch { return null; }
        }

        private static GameObject SpawnOneWithVelocity(GameObject prefab, Vector3 worldPos,
                                                       Vector2 velocity, string tag)
        {
            try
            {
                if (!CanSpawnExtra()) return null;
                _spawnTimes.Enqueue(Time.time);
                GameObject clone = UnityEngine.Object.Instantiate(prefab, worldPos, Quaternion.identity);
                clone.name = prefab.name;
                if (_rb2dType == null) { UnityEngine.Object.Destroy(clone, 0.1f); return null; }
                var rb = clone.GetComponent(_rb2dType) ?? clone.GetComponentInChildren(_rb2dType, true);
                if (rb == null) { UnityEngine.Object.Destroy(clone, 0.1f); return null; }
                SetVelocity(rb, velocity);
                ApplyForcedDamage(clone);
                UnityEngine.Object.Destroy(clone, Mathf.Max(0.5f, cfgProjectileLifetime.Value));
                return clone;
            }
            catch { return null; }
        }

        private static object GetRigidbodyOf(GameObject go)
        {
            if (go == null || _rb2dType == null) return null;
            try { return go.GetComponent(_rb2dType) ?? go.GetComponentInChildren(_rb2dType, true); }
            catch { return null; }
        }

        private static IEnumerator DelayedWallRoutine(GameObject prefab, GameObject spawnPt, bool isLeft, string tag)
        {
            Vector3 origin = spawnPt.transform.position;
            if (_bossFsm != null && _bossFsm.transform != null) origin = _bossFsm.transform.position;
            LogStructure("DelayedWall scheduled at " + origin + " dirX=" + (isLeft ? -1f : 1f));

            float dirX = isLeft ? -1f : 1f;
            float wallDistance = Mathf.Max(2f, cfgWallDistance.Value);
            float spacing = Mathf.Max(0.1f, cfgWallSpacing.Value);
            float[] yOffsets = { -spacing, 0f, spacing };
            float hold = Mathf.Max(0f, cfgWallHoldTime.Value);
            float stagger = Mathf.Max(0f, cfgWallStagger.Value);
            float launchSpeed = Mathf.Max(1f, cfgWallLaunchSpeed.Value);
            float totalLife = hold + stagger * 3f + 8f;

            var shots = new List<GameObject>(3);
            var targetPositions = new List<Vector3>(3);

            for (int i = 0; i < 3; i++)
            {
                Vector3 target = new Vector3(origin.x + dirX * wallDistance, origin.y + yOffsets[i], origin.z);
                targetPositions.Add(target);
                shots.Add(SpawnStationary(prefab, target, tag + "_wall" + i, totalLife));
            }

            float holdEnd = Time.time + hold;
            while (Time.time < holdEnd)
            {
                for (int i = 0; i < shots.Count; i++)
                {
                    var go = shots[i];
                    if (go == null) continue;
                    go.transform.position = targetPositions[i];
                    var rb = GetRigidbodyOf(go);
                    if (rb != null) SetVelocity(rb, Vector2.zero);
                }
                yield return null;
            }

            Transform player = GetPlayer();
            for (int i = 0; i < shots.Count; i++)
            {
                var go = shots[i];
                if (go == null) continue;
                var rb = GetRigidbodyOf(go);
                if (rb == null) continue;

                Vector2 launchDir;
                if (player != null)
                {
                    Vector2 to = (Vector2)(player.position - go.transform.position);
                    launchDir = to.sqrMagnitude > 0.0001f ? to.normalized : new Vector2(-dirX, 0f);
                }
                else launchDir = new Vector2(-dirX, 0f);

                SetVelocity(rb, launchDir * launchSpeed);
                if (i < shots.Count - 1 && stagger > 0f) yield return new WaitForSeconds(stagger);
            }
        }

        // =====================================================================
        //  Sky Rain
        // =====================================================================

        private static IEnumerator SkyRainRoutine(GameObject prefab, string tag)
        {
            Instance.StartCoroutine(FakeScreamRoutine());

            Transform player = GetPlayer();
            float centerX, baseY;

            if (player != null)
            {
                centerX = player.position.x;
                baseY   = player.position.y;
            }
            else if (_bossFsm != null)
            {
                centerX = _bossFsm.transform.position.x;
                baseY   = _bossFsm.transform.position.y;
            }
            else
            {
                centerX = 0f;
                baseY   = 0f;
            }

            int count = Mathf.Max(1, cfgSkyRainCount.Value);
            float span = Mathf.Max(1f, cfgSkyRainSpan.Value);
            float height = Mathf.Max(2f, cfgSkyRainHeight.Value);
            float fallSpeed = Mathf.Max(1f, cfgSkyRainFallSpeed.Value);
            float ceiling = cfgSkyRainMaxY != null ? cfgSkyRainMaxY.Value : 40f;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float x = centerX - span * 0.5f + span * t;
                float y = Mathf.Min(baseY + height, ceiling);
                y += UnityEngine.Random.Range(-0.5f, 0.5f);

                Vector3 spawnPos = new Vector3(x, y, 0f);
                Vector2 vel = new Vector2(0f, -fallSpeed);
                SpawnOneWithVelocity(prefab, spawnPos, vel, tag + "_sr" + i);
            }

            LogStructure("SkyRain fired: " + count + " shots (ceiling=" + ceiling + ", fall=" + fallSpeed + ")");
            yield break;
        }

        // =====================================================================
        //  Core spawn helper
        // =====================================================================

        private static bool CanSpawnExtra()
        {
            float now = Time.time;
            while (_spawnTimes.Count > 0 && now - _spawnTimes.Peek() > 2f) _spawnTimes.Dequeue();
            return _spawnTimes.Count < cfgMaxLiveExtras.Value;
        }

        private static void SetVelocity(object rb, Vector2 v)
        {
            if (rb == null) return;
            if (_setVelocity != null) { try { _setVelocity(rb, v); return; } catch { _setVelocity = null; } }
            if (_rb2dVelocityProp != null) { try { _rb2dVelocityProp.SetValue(rb, v); } catch { } }
        }

        private static Vector2 GetVelocity(object rb)
        {
            if (rb == null) return Vector2.zero;
            if (_getVelocity != null) { try { return _getVelocity(rb); } catch { _getVelocity = null; } }
            if (_rb2dVelocityProp != null) { try { return (Vector2)_rb2dVelocityProp.GetValue(rb); } catch { } }
            return Vector2.zero;
        }

        private static void ApplyForcedDamage(GameObject clone)
        {
            if (clone == null) return;
            if (cfgForceProjectileDamage == null || cfgForceProjectileDamage.Value <= 0) return;
            if (_damageHeroType == null || _damageDealtField == null) return;
            try
            {
                var dh = clone.GetComponent(_damageHeroType);
                if (dh == null) dh = clone.GetComponentInChildren(_damageHeroType, true);
                if (dh != null) _damageDealtField.SetValue(dh, cfgForceProjectileDamage.Value);
            }
            catch { }
        }

        private static void SpawnOne(GameObject prefab, GameObject spawnPt, float angleDeg,
                                     float speed, string tag, bool boomerang = false, float boomDelay = 0.7f)
        {
            try
            {
                if (!CanSpawnExtra()) return;
                _spawnTimes.Enqueue(Time.time);
                Vector3 pos = spawnPt.transform.position;
                Quaternion rot = spawnPt.transform.rotation;
                GameObject clone = UnityEngine.Object.Instantiate(prefab, pos, rot);
                clone.name = prefab.name;
                if (_rb2dType == null) return;
                var rb = clone.GetComponent(_rb2dType) ?? clone.GetComponentInChildren(_rb2dType, true);
                if (rb == null) { UnityEngine.Object.Destroy(clone, 0.1f); return; }
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 vel = new Vector2(Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);
                SetVelocity(rb, vel);
                ApplyForcedDamage(clone);
                UnityEngine.Object.Destroy(clone, Mathf.Max(0.5f, cfgProjectileLifetime.Value));
                if (boomerang && Instance != null) Instance.StartCoroutine(BoomerangRoutine(clone, boomDelay));
            }
            catch { }
        }

        private static IEnumerator BoomerangRoutine(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go == null) yield break;
            var rb = go.GetComponent(_rb2dType) ?? go.GetComponentInChildren(_rb2dType, true);
            if (rb == null) yield break;
            try
            {
                var v = GetVelocity(rb);
                SetVelocity(rb, -v * cfgBoomerangReturnMul.Value);
            }
            catch { }
        }

        private static float MirrorAngle(float angle)
        {
            float a = angle % 360f;
            if (a < 0) a += 360f;
            return 180f - a;
        }

        private static float[] ParseAngles(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return Array.Empty<float>();
            if (_angleCache.TryGetValue(csv, out var cached)) return cached;
            var parts = csv.Split(',');
            var list = new List<float>(parts.Length);
            foreach (var p in parts)
            {
                float v;
                if (float.TryParse(p.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
                    list.Add(v);
            }
            var result = list.ToArray();
            _angleCache[csv] = result;
            return result;
        }

        // =====================================================================
        //  Scream
        // =====================================================================

        private static void ResolveScreamClip()
        {
            if (_screamClipResolved) return;
            _screamClipResolved = true;

            string wanted = cfgScreamClipName.Value;
            if (string.IsNullOrEmpty(wanted)) return;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<AudioClip>();
                if (all == null) return;

                foreach (var c in all)
                    if (c != null && c.name != null && string.Equals(c.name, wanted, StringComparison.OrdinalIgnoreCase))
                    { _screamClip = c; LogStructure("Scream clip resolved (exact): " + c.name); return; }

                foreach (var c in all)
                    if (c != null && c.name != null && c.name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    { _screamClip = c; LogStructure("Scream clip resolved (substring): " + c.name); return; }

                foreach (var c in all)
                {
                    if (c == null || c.name == null) continue;
                    string n = c.name.ToLowerInvariant();
                    if (n.Contains("carmelita") && n.Contains("scream"))
                    { _screamClip = c; LogStructure("Scream clip resolved (fallback): " + c.name); return; }
                }
                Log.LogWarning("[Karmelita] No scream clip found for '" + wanted + "'.");
            }
            catch (Exception ex) { Log.LogWarning("[Karmelita] ResolveScreamClip failed: " + ex.Message); }
        }

        private static IEnumerator FakeScreamRoutine()
        {
            if (!cfgScreamEnabled.Value) yield break;
            ResolveScreamClip();

            if (_screamClip != null && _bossFsm != null)
            {
                try { AudioSource.PlayClipAtPoint(_screamClip, _bossFsm.transform.position, 1f); } catch { }
            }

            _screamActive = true;
            _screamEndTime = Time.time + Mathf.Max(0.1f, cfgScreamDuration.Value);
            _lastAppliedState = null;
            _lastAppliedClip = null;

            LogStructure("Scream started, duration=" + cfgScreamDuration.Value);

            yield return new WaitForSeconds(cfgScreamDuration.Value);

            _screamActive = false;
            if (_variationPicked) ApplyTelegraphTint(_currentVariation);
            else ClearTelegraphTint();
            _lastAppliedState = null;
            _lastAppliedClip = null;

            LogStructure("Scream ended");
        }

        private static void UpdateScreamState()
        {
            if (!_screamActive) return;
            if (Time.time >= _screamEndTime) return;
            if (cfgScreamWhiteStrength.Value <= 0f) return;
            if (_bossSprite == null || _spriteColorProp == null) return;
            if (!_originalSpriteColorCached) return;

            float s = Mathf.Clamp01(cfgScreamWhiteStrength.Value);
            Color white = new Color(
                Mathf.Lerp(_originalSpriteColor.r, 1f, s),
                Mathf.Lerp(_originalSpriteColor.g, 1f, s),
                Mathf.Lerp(_originalSpriteColor.b, 1f, s),
                _originalSpriteColor.a);
            try { _spriteColorProp.SetValue(_bossSprite, white); } catch { }
        }

        // =====================================================================
        //  State enter
        // =====================================================================

        private static void OnFsmStateEntered(FsmState __instance)
        {
            try
            {
                if (__instance == null) return;

                var fsmComp = __instance.Fsm?.FsmComponent;
                if (fsmComp == null) return;

                if (_bossFsm != null) { if (fsmComp != _bossFsm) return; }
                else
                {
                    if (fsmComp.gameObject.name != BossName) return;
                    if (fsmComp.FsmName != BossFsmName) return;
                    _bossFsm = fsmComp;
                    Log.LogInfo("[Karmelita] Boss FSM identified: " + fsmComp.gameObject.name);
                }

                if (!cfgEnabled.Value) return;

                UpdatePhaseFromState(__instance.Name, fsmComp);

                string stateName = __instance.Name ?? "";
                float now = Time.time;

                if (_patchedState != null && _patchedState.Name != stateName)
                    RestorePatchedTransitions();

                bool throwRelated = IsThrowSequenceState(stateName);
                bool preserveState = IsPreserveThrowState(stateName);

                if (throwRelated)
                {
                    float window = Mathf.Max(0f, cfgDoubleThrowWindow.Value);
                    bool continuation = _variationPicked && (now - _lastThrowStateTime) <= window;
                    bool isAirState = stateName.StartsWith("Air Sickles", StringComparison.Ordinal);

                    if (isAirState)
                    {
                        _currentVariation = ProjectileVariation.AirFan;
                        _lastRolledVariation = _currentVariation;
                    }
                    else if (!continuation)
                    {
                        _currentVariation = RollVariation(false);
                        _lastRolledVariation = _currentVariation;
                    }
                    else
                    {
                        bool isAnticState = stateName.IndexOf("Antic", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isUnfair = _currentVariation == ProjectileVariation.Boomerang
                                     || _currentVariation == ProjectileVariation.SkyRain
                                     || _currentVariation == ProjectileVariation.DelayedWall;
                        if (isAnticState && isUnfair)
                        {
                            _currentVariation = RollVariation(false);
                            _lastRolledVariation = _currentVariation;
                        }
                    }

                    _variationPicked = true;
                    _lastThrowStateTime = now;
                    ApplyTelegraphTint(_currentVariation);
                }
                else if (preserveState)
                {
                    ClearTelegraphTint();
                }
                else
                {
                    float window = Mathf.Max(0f, cfgDoubleThrowWindow.Value);
                    bool inThrowWindow = _variationPicked && (now - _lastThrowStateTime) <= window;
                    if (!inThrowWindow)
                    {
                        ClearTelegraphTint();
                        _variationPicked = false;
                    }
                }

                _lastProcessedSpawnState = "";

                if (cfgWallDiveLoopEnabled.Value && !ShouldSkipPatches()
                    && IsActiveInCurrentPhase(cfgWallDiveLoopActiveInPhases.Value))
                {
                    if (stateName == "Wall Dive") _wallDiveChainCount++;
                    else if (stateName == "Wall Land")
                    {
                        if (_wallDiveChainCount < cfgWallDiveLoopMax.Value)
                            PatchTransitionsTo(__instance, "Wall Dive");
                        else
                            RestorePatchedTransitions();
                    }
                    else _wallDiveChainCount = 0;
                }
                else _wallDiveChainCount = 0;

                if (cfgFeintEnabled.Value && !ShouldSkipPatches()
                    && IsActiveInCurrentPhase(cfgFeintActiveInPhases.Value))
                {
                    bool isFrom = false;
                    foreach (var raw in cfgFeintFromStates.Value.Split(','))
                    {
                        var t = raw.Trim();
                        if (t.Length == 0) continue;
                        if (string.Equals(stateName, t, StringComparison.OrdinalIgnoreCase)) { isFrom = true; break; }
                    }

                    if (isFrom)
                    {
                        float roll = UnityEngine.Random.Range(0f, 100f);
                        if (roll < cfgFeintChance.Value)
                        {
                            var toParts = SplitCsv(cfgFeintToStates.Value);
                            if (toParts.Length > 0)
                            {
                                string target = toParts[UnityEngine.Random.Range(0, toParts.Length)];
                                PatchTransitionsTo(__instance, target);
                            }
                        }
                    }
                }

                if (!_coroutineStarted)
                {
                    _coroutineStarted = true;
                    if (Instance != null)
                        Instance.StartCoroutine(DeferredInit(fsmComp));
                }
            }
            catch (Exception ex) { Log.LogError("[Karmelita] OnFsmStateEntered failed: " + ex); }
        }

        private static void UpdatePhaseFromState(string stateName, PlayMakerFSM fsm)
        {
            int previous = _currentPhase;
            int fromVar = TryReadPhaseVar(fsm);
            if (fromVar > 0) { _currentPhase = fromVar; }
            else if (!string.IsNullOrEmpty(stateName))
            {
                if (stateName.StartsWith("P3", StringComparison.Ordinal)) _currentPhase = 3;
                else if (stateName.StartsWith("P2", StringComparison.Ordinal)) _currentPhase = 2;
                else if (stateName.StartsWith("P1", StringComparison.Ordinal)) _currentPhase = 1;
            }
            if (_currentPhase != previous)
            {
                _lastAppliedState = null;
                _lastAppliedClip = null;
                RestorePatchedTransitions();
                _wallDiveChainCount = 0;
            }
        }

        private static int TryReadPhaseVar(PlayMakerFSM fsm)
        {
            if (fsm == null) return -1;
            string[] names = { "Phase", "PhaseNum", "phase", "BossPhase", "Phase Number", "CurrentPhase", "PhaseNumber" };
            foreach (var n in names)
            {
                var v = fsm.FsmVariables.GetFsmInt(n);
                if (v != null && v.Value >= 1 && v.Value <= 3) return v.Value;
            }
            return -1;
        }

        private static IEnumerator DeferredInit(PlayMakerFSM fsm)
        {
            for (int i = 0; i < 10; i++) yield return null;
            try
            {
                _bossAnimator = FindAnimator(fsm.gameObject);
                PrepareAnimatorAccess();
                PrepareSpriteAccess();
            }
            catch { }

            try
            {
                if (Instance != null && !_prefabWarmed)
                    Instance.StartCoroutine(WarmCachesRoutine(fsm));
            }
            catch { }

            try
            {
                if (!_permanentTweaksApplied)
                {
                    _permanentTweaksApplied = true;
                    ApplyPermanentTweaks(fsm);
                }
            }
            catch (Exception ex) { Log.LogError("[Karmelita] Permanent tweaks: " + ex); }
        }

        private static IEnumerator WarmCachesRoutine(PlayMakerFSM fsm)
        {
            for (int i = 0; i < 20; i++) yield return null;
            try { ResolveScreamClip(); } catch { }

            if (!_prefabWarmed && _sicklePrefabCached != null)
            {
                _prefabWarmed = true;
                try
                {
                    Vector3 pos = fsm != null ? fsm.transform.position + Vector3.up * 1000f
                                              : new Vector3(0f, 1000f, 0f);
                    var warm = UnityEngine.Object.Instantiate(_sicklePrefabCached, pos, Quaternion.identity);
                    if (warm != null) UnityEngine.Object.Destroy(warm, 0.05f);
                    LogStructure("Prefab warmed.");
                }
                catch { }
            }
        }

        private static IEnumerator WarmPrefabOnlyRoutine()
        {
            if (_prefabWarmed || _sicklePrefabCached == null) yield break;
            _prefabWarmed = true;
            try
            {
                Vector3 pos = _bossFsm != null
                    ? _bossFsm.transform.position + Vector3.up * 1000f
                    : new Vector3(0f, 1000f, 0f);
                var warm = UnityEngine.Object.Instantiate(_sicklePrefabCached, pos, Quaternion.identity);
                if (warm != null) UnityEngine.Object.Destroy(warm, 0.05f);
                LogStructure("Prefab warmed (late).");
            }
            catch { }
            yield break;
        }

        // =====================================================================
        //  Sprite / tint
        // =====================================================================

        private static void PrepareSpriteAccess()
        {
            if (_bossAnimator == null) return;
            if (_bossSprite != null && _spriteColorProp != null) return;
            try
            {
                var animType = _bossAnimator.GetType();
                var spriteProp = animType.GetProperty("Sprite",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (spriteProp == null) return;
                _bossSprite = spriteProp.GetValue(_bossAnimator);
                if (_bossSprite == null) return;
                var spriteType = _bossSprite.GetType();
                _spriteColorProp = spriteType.GetProperty("color",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_spriteColorProp == null) return;
                if (!_originalSpriteColorCached)
                {
                    _originalSpriteColor = (Color)_spriteColorProp.GetValue(_bossSprite);
                    _originalSpriteColorCached = true;
                }
            }
            catch { }
        }

        private static void ApplyTelegraphTint(ProjectileVariation v)
        {
            if (!cfgTelegraphEnabled.Value) return;
            if (_bossSprite == null || _spriteColorProp == null) return;
            if (!_originalSpriteColorCached) return;
            Color tint;
            if (!_variationColors.TryGetValue(v, out tint)) tint = Color.white;
            float s = Mathf.Clamp01(cfgTelegraphTintStrength.Value);
            Color target = new Color(
                Mathf.Lerp(_originalSpriteColor.r, tint.r, s),
                Mathf.Lerp(_originalSpriteColor.g, tint.g, s),
                Mathf.Lerp(_originalSpriteColor.b, tint.b, s),
                _originalSpriteColor.a);
            try { _spriteColorProp.SetValue(_bossSprite, target); _telegraphActive = true; }
            catch { }
        }

        private static void ClearTelegraphTint()
        {
            if (!_telegraphActive) return;
            _telegraphActive = false;
            if (_bossSprite == null || _spriteColorProp == null) return;
            if (!_originalSpriteColorCached) return;
            try { _spriteColorProp.SetValue(_bossSprite, _originalSpriteColor); } catch { }
        }

        // =====================================================================
        //  Animation speed map
        // =====================================================================

        private static void RebuildAnimationSpeedMap()
        {
            _animSpeedByState = new Dictionary<string, float>(StringComparer.Ordinal);

            AddState("Slash End", cfgRecoilSpeed.Value);
            AddState("Cyclone Recoil", cfgRecoilSpeed.Value);
            AddState("Spin Recoil", cfgRecoilSpeed.Value);

            AddState("P2 Roar Antic", cfgRoarAnticSpeed.Value);
            AddState("P3 Roar Antic", cfgRoarAnticSpeed.Value);
            AddState("P2 Roar", cfgRoarAnticSpeed.Value);
            AddState("P3 Roar", cfgRoarAnticSpeed.Value);
            AddState("Roar Antic", cfgRoarAnticSpeed.Value);
            AddState("Roar", cfgRoarAnticSpeed.Value);

            foreach (var s in new[] {
                "Slash Antic","Cyclone Antic","Spin Antic","Throw Antic",
                "Rethrow Antic 1","Rethrow Antic 2","Rethrow Antic 3","Rethrow Antic 4","Rethrow Antic 5",
                "Air Throw Antic","A Rethrow Antic","Jump Antic","Launch Antic","Charge Antic","Dthrust Antic",
                "Block","Approach Block"
            }) AddState(s, cfgAnticSpeed.Value);

            foreach (var s in new[] {
                "Slash 1","Slash 2","Slash 3","Slash 4","Slash 5","Slash 6","Slash 7","Slash 8","Slash 9",
                "Cyclone 1","Cyclone 2","Cyclone 3","Cyclone 4","Cyclone Multihit",
                "Spin Attack","Spin Attack Land","Spin Multihit",
                "Spear Slam","Dash Grind","Dash Grind Spin 1","Dash Grind Spin 2","Dash Grind Spin 3",
                "Wall Dive","Wall Land",
                "Air Sickles","Air Sickles 2",
                "Air Throw","Air Throw Slash","Air Throw Slash 2"
            }) AddState(s, cfgAttackSpeed.Value);

            foreach (var s in new[] {
                "Movement 1","Movement 2","Movement 3","Movement 4","Movement 5",
                "Dash","Evade","Long Evade","Air Evade","Jump Back","Approach","Long Approach"
            }) AddState(s, cfgMovementSpeed.Value);

            foreach (var s in new[] {
                "Throw 1","Throw 2","Rethrow","Rethrow 2",
                "Throw L","Throw R","Rethrow L","Rethrow R"
            }) AddState(s, cfgAttackSpeed.Value);

            AddState("Wall Dive", cfgWallDiveSpeed.Value);
            AddState("Wall Land", cfgWallDiveSpeed.Value);
        }

        private static void AddState(string name, float mult)
        {
            if (_animSpeedByState == null) return;
            if (mult <= 0f) mult = 1.0f;
            _animSpeedByState[name] = mult;
        }

        // =====================================================================
        //  Update
        // =====================================================================

        private void Update()
        {
            try
            {
                if (_screamActive && Time.time >= _screamEndTime) _screamActive = false;

                UpdateScreamState();
                CheckLastStandTrigger();
                LockBossForLastStand();

                if (_lastStandActive && _lastStandEndTime > 0f && Time.time > _lastStandEndTime + 3f)
                {
                    Log.LogWarning("[Karmelita] Last Stand watchdog fired — forcing release.");
                    _lastStandActive = false;
                    _lastStandCorner = Vector3.zero;

                    if (_bossFsm != null)
                    {
                        try
                        {
                            Vector3 p = _bossFsm.transform.position;
                            _bossFsm.transform.position = new Vector3(p.x, _lastStandGroundY, p.z);
                            var rb = GetRigidbodyOf(_bossFsm.gameObject);
                            if (rb != null) SetVelocity(rb, Vector2.zero);
                        }
                        catch { }
                    }

                    if (Instance != null) Instance.StartCoroutine(LastStandExitRoutine());
                }

                if (!_permanentTweaksApplied) return;
                if (_bossFsm == null || _bossAnimator == null) return;
                if (_animSpeedByState == null) return;

                string stateName = _bossFsm.Fsm?.ActiveStateName;
                if (string.IsNullOrEmpty(stateName)) return;

                object clip = GetCurrentClip();
                if (clip == null) return;

                if (stateName == _lastAppliedState && ReferenceEquals(clip, _lastAppliedClip))
                    return;

                if (!cfgEnabled.Value) return;

                _lastAppliedState = stateName;
                _lastAppliedClip = clip;

                if (!EnsureClipFpsAccess(clip)) return;

                int currentFps = GetClipFps(clip);
                if (currentFps <= 0) return;

                int origFps;
                if (!_originalFps.TryGetValue(clip, out origFps))
                {
                    if (_screamActive) return;
                    origFps = currentFps;
                    _originalFps[clip] = origFps;
                }

                float mult;
                if (_currentPhase == 3 && cfgP3VanillaSpeed.Value)
                {
                    mult = 1.0f;
                }
                else
                {
                    if (!_animSpeedByState.TryGetValue(stateName, out mult)) mult = 1.0f;
                    if (mult <= 0f) mult = 1.0f;

                    if (_currentPhase == 3 && cfgP3SpeedMultiplier.Value > 0f)
                        mult *= cfgP3SpeedMultiplier.Value;
                }

                if (_screamActive && cfgScreamEnabled.Value && cfgScreamSlowdown.Value > 0f
                    && IsThrowSequenceState(stateName))
                {
                    mult *= cfgScreamSlowdown.Value;
                }

                if (cfgMinSpeedMult != null && cfgMaxSpeedMult != null)
                {
                    float lo = cfgMinSpeedMult.Value > 0f ? cfgMinSpeedMult.Value : 0f;
                    float hi = cfgMaxSpeedMult.Value > 0f ? cfgMaxSpeedMult.Value : float.MaxValue;
                    if (hi < lo) { float t = lo; lo = hi; hi = t; }
                    mult = Mathf.Clamp(mult, lo, hi);
                }

                int targetFps = Mathf.Max(1, Mathf.RoundToInt(origFps * mult));

                if (cfgLogClipChanges.Value)
                {
                    string clipName = GetClipName(clip);
                    if (stateName != _lastLoggedState || clipName != _lastLoggedClip)
                    {
                        Log.LogInfo("[Karmelita] state=" + stateName
                            + " clip='" + clipName + "' fps=" + currentFps
                            + " -> " + targetFps + " (x" + mult.ToString("0.00") + ")");
                        _lastLoggedState = stateName;
                        _lastLoggedClip = clipName;
                    }
                }

                if (currentFps != targetFps) SetClipFps(clip, targetFps);
            }
            catch { }
        }

        // =====================================================================
        //  Animator reflection
        // =====================================================================

        private static object FindAnimator(GameObject root)
        {
            var tk2dType = AccessTools.TypeByName("tk2dSpriteAnimator");
            if (tk2dType != null)
            {
                var comp = root.GetComponentInChildren(tk2dType, true);
                if (comp != null)
                {
                    Log.LogInfo("[Karmelita] Animator: tk2dSpriteAnimator on '"
                        + ((Component)comp).gameObject.name + "'.");
                    return comp;
                }
            }
            Log.LogWarning("[Karmelita] No tk2dSpriteAnimator found.");
            return null;
        }

        private static void PrepareAnimatorAccess()
        {
            if (_bossAnimator == null || _animatorAccessPrepared) return;
            _animatorAccessPrepared = true;

            var animType = _bossAnimator.GetType();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            _animCurrentClipProp = animType.GetProperty("CurrentClip", F);
            _animCurrentClipField = animType.GetField("currentClip", F);

            if (_animCurrentClipProp != null)
            {
                var getter = _animCurrentClipProp.GetGetMethod(true);
                if (getter != null)
                {
                    try
                    {
                        _getCurrentClipDelegate = (Func<object, object>)Delegate.CreateDelegate(
                            typeof(Func<object, object>), getter);
                    }
                    catch { _getCurrentClipDelegate = null; }
                }
            }

            object clip = GetCurrentClip();
            if (clip != null)
            {
                var clipType = clip.GetType();
                _clipFpsField = clipType.GetField("fps", F);
                _clipNameField = clipType.GetField("name", F);
                _clipNameProp = clipType.GetProperty("name", F);
            }
        }

        private static object GetCurrentClip()
        {
            if (_bossAnimator == null) return null;
            if (_getCurrentClipDelegate != null)
            {
                try { return _getCurrentClipDelegate(_bossAnimator); }
                catch { _getCurrentClipDelegate = null; }
            }
            try
            {
                if (_animCurrentClipProp != null) return _animCurrentClipProp.GetValue(_bossAnimator);
                if (_animCurrentClipField != null) return _animCurrentClipField.GetValue(_bossAnimator);
            }
            catch { }
            return null;
        }

        private static bool EnsureClipFpsAccess(object clip)
        {
            if (clip == null) return false;
            if (_clipFpsField != null) return true;
            var clipType = clip.GetType();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _clipFpsField = clipType.GetField("fps", F);
            if (_clipNameField == null) _clipNameField = clipType.GetField("name", F);
            if (_clipNameProp == null) _clipNameProp = clipType.GetProperty("name", F);
            return _clipFpsField != null;
        }

        private static int GetClipFps(object clip)
        {
            if (clip == null || _clipFpsField == null) return -1;
            try { return Convert.ToInt32(_clipFpsField.GetValue(clip)); } catch { return -1; }
        }

        private static void SetClipFps(object clip, int fps)
        {
            if (clip == null || _clipFpsField == null) return;
            try { _clipFpsField.SetValue(clip, fps); } catch { }
        }

        private static string GetClipName(object clip)
        {
            if (clip == null) return "?";
            try
            {
                if (_clipNameProp != null) return _clipNameProp.GetValue(clip) as string ?? "?";
                if (_clipNameField != null) return _clipNameField.GetValue(clip) as string ?? "?";
            }
            catch { }
            return "?";
        }

        // =====================================================================
        //  Permanent tweaks
        // =====================================================================

        private static void ApplyPermanentTweaks(PlayMakerFSM fsm)
        {
            ScalePositiveSetFloats(fsm, "Stun Start", cfgStunRatio.Value);
            if (cfgDisableBlock.Value) RewireTransitions(fsm, "Block", "Attack Choice");
            if (cfgDisableForceEvade.Value) RewireTransitions(fsm, "Force Evade", "Attack Choice");
            if (cfgRemoveContactDamage.Value) RemoveContactDamage(fsm);
            Log.LogInfo("[Karmelita] Permanent tweaks applied.");
        }

        private static void ScalePositiveSetFloats(PlayMakerFSM fsm, string stateName, float ratio)
        {
            var state = fsm.Fsm.GetState(stateName);
            if (state?.Actions == null) return;
            int scaled = 0;
            foreach (var action in state.Actions)
            {
                if (action is SetFloatValue sfv && sfv.floatValue != null && sfv.floatValue.Value > 0f)
                {
                    sfv.floatValue.Value = sfv.floatValue.Value * ratio;
                    scaled++;
                }
            }
            if (scaled == 0) Log.LogWarning("[Karmelita] " + stateName + ": no positive SetFloatValue.");
        }

        private static void RewireTransitions(PlayMakerFSM fsm, string fromState, string toState)
        {
            var from = fsm.Fsm.GetState(fromState);
            if (from == null) return;
            if (fsm.Fsm.GetState(toState) == null) return;
            int rewired = 0;
            foreach (var state in fsm.Fsm.States)
            {
                if (state?.Transitions == null) continue;
                foreach (var t in state.Transitions)
                    if (t != null && string.Equals(t.ToState, fromState, StringComparison.Ordinal))
                    { t.ToState = toState; rewired++; }
            }
            from.Name = fromState + "_DISABLED";
            Log.LogInfo("[Karmelita] Rewired " + rewired + " into '" + fromState + "' -> '" + toState + "'.");
        }

        private static void RemoveContactDamage(PlayMakerFSM fsm)
        {
            var h = fsm.gameObject.GetComponent<DamageHero>();
            if (h != null && h.enabled)
            {
                h.enabled = false;
                Log.LogInfo("[Karmelita] Disabled root DamageHero.");
            }
        }
    }
}
