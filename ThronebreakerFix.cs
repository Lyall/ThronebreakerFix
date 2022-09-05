using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;

namespace ThronebreakerFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class TB : BasePlugin
    {
        internal static new ManualLogSource Log;

        // Features
        public static ConfigEntry<bool> bUltrawideFixes;
        public static ConfigEntry<bool> bIntroSkip;
#if DEBUG
        public static ConfigEntry<bool> bSpannedUI;
#endif

        // Custom Resolution
        public static ConfigEntry<bool> bCustomResolution;
        public static ConfigEntry<float> fDesiredResolutionX;
        public static ConfigEntry<float> fDesiredResolutionY;
        public static ConfigEntry<bool> bFullscreen;

        // Graphics
        public static ConfigEntry<string> sAAType;



        public override void Load()
        {
            // Plugin startup logic
            Log = base.Log;
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // Features
            bUltrawideFixes = Config.Bind("Features",
                                "UltrawideFixes",
                                true,
                                "Set to true to enable ultrawide UI fixes.");

            bIntroSkip = Config.Bind("Features",
                                "IntroSkip",
                                 true,
                                "Skip intro logos.");
#if DEBUG
            bSpannedUI = Config.Bind("Features",
                                "SpannedUI",
                                 false,
                                "Set to true for spanned UI.");
#endif
            // Graphics
            sAAType = Config.Bind("Graphics",
                                "Anti-Aliasing",
                                "None",
                                new ConfigDescription("Set desired anti-aliasing type.",
                                new AcceptableValueList<string>("None", "FXAA", "SMAA", "TAA")));

            // Custom Resolution
            bCustomResolution = Config.Bind("Set Custom Resolution",
                                "CustomResolution",
                                 true,
                                "Set to true to enable the custom resolution below.");

            fDesiredResolutionX = Config.Bind("Set Custom Resolution",
                                "ResolutionWidth",
                                (float)Display.main.systemWidth, // Set default to display width so we don't leave an unsupported resolution as default.
                                "Set desired resolution width.");

            fDesiredResolutionY = Config.Bind("Set Custom Resolution",
                                "ResolutionHeight",
                                (float)Display.main.systemHeight, // Set default to display height so we don't leave an unsupported resolution as default.
                                "Set desired resolution height.");

            bFullscreen = Config.Bind("Set Custom Resolution",
                                "Fullscreen",
                                true,
                                "Set to true for fullscreen or false for windowed.");


            // Run CustomResolutionPatch
            if (bCustomResolution.Value)
            {
                Screen.SetResolution((int)fDesiredResolutionX.Value, (int)fDesiredResolutionY.Value, bFullscreen.Value);
                Log.LogInfo($"Set screen resolution to {fDesiredResolutionX.Value}x{fDesiredResolutionY.Value}, Fullscreen = {bFullscreen.Value}.");
                Harmony.CreateAndPatchAll(typeof(CustomResolutionPatch));
            }

            // Run UltrawidePatch
            if (bUltrawideFixes.Value && bCustomResolution.Value)
            {
                Harmony.CreateAndPatchAll(typeof(UltrawidePatch));
            }

#if DEBUG
            // Run SpannedUIPatch
            if (bSpannedUI.Value)
            {
                Harmony.CreateAndPatchAll(typeof(SpannedUIPatch));
            }
#endif

            // Run IntroSkipPatch
            if (bIntroSkip.Value)
            {
                Harmony.CreateAndPatchAll(typeof(IntroSkipPatch));
            }

            Harmony.CreateAndPatchAll(typeof(SettingsPatch));

        }


        [HarmonyPatch]
        public class SettingsPatch
        {
#if DEBUG
            // Cursor lock for debugging
            [HarmonyPatch(typeof(Cursor), nameof(Cursor.visible), MethodType.Setter)]
            [HarmonyPrefix]
            public static bool ForceCursorOn()
            {
                return false;
            }
#endif

            [HarmonyPatch(typeof(GwentUnity.CameraAntiAliasingController), nameof(GwentUnity.CameraAntiAliasingController.Init))]
            [HarmonyPostfix]
            public static void ChangeAA(GwentUnity.CameraAntiAliasingController __instance)
            {
                var antiAliasing = sAAType.Value switch
                {
                    "FXAA" => UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.FastApproximateAntialiasing, // FXAA
                    "SMAA" => UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing, // SMAA
                    "TAA" => UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.TemporalAntialiasing, // TAA
                    _ => UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.None,
                };

                if (__instance.m_PostProcessLayer != null)
                {
                    __instance.m_PostProcessLayer.antialiasingMode = antiAliasing;
                    Log.LogInfo($"Antialiasing: Set camera anti-aliasing type to {antiAliasing}.");
                }
            }
        }

        [HarmonyPatch]
        public class CustomResolutionPatch
        {
            [HarmonyPatch(typeof(GwentUnity.ResolutionNodeWrapper._HACK_SetResolution_c__Iterator0), nameof(GwentUnity.ResolutionNodeWrapper._HACK_SetResolution_c__Iterator0.MoveNext))]
            [HarmonyPrefix]
            public static bool ApplyCustomRes()
            {
                return false;
            }
        }

        [HarmonyPatch]
        public class UltrawidePatch
        {
            // Aspect Ratio
            public static float DefaultAspectRatio = (float)16 / 9;
            public static float NewAspectRatio = (float)fDesiredResolutionX.Value / fDesiredResolutionY.Value;
            public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;
            public static float AspectDivider = DefaultAspectRatio / NewAspectRatio;

            public static Vector2 NativeCanvasSize = new Vector2((float)1600, (float)900);
            public static Vector2 NewCanvasSize = new Vector2((float)1600 * AspectMultiplier, (float)900);

            // Fix UI Camera aspect ratio
            [HarmonyPatch(typeof(GwentUnity.AspectRatioManager), nameof(GwentUnity.AspectRatioManager.SetupAspectRatio))]
            [HarmonyPostfix]
            public static void AspectRatio(GwentUnity.AspectRatioManager __instance)
            {
                __instance.m_RatioToMaintain = NewAspectRatio;

                if (__instance.CurrentViewportData != null)
                {
                    var currViewport = __instance.CurrentViewportData;
                    currViewport.ShouldEnableBlackBars = false;
                }

                if (CrimsonUI.CrimsonUIManager.Instance != null)
                {
                    var UICamera = CrimsonUI.CrimsonUIManager.Instance.UICamera;
                    UICamera.aspect = NewAspectRatio;
                    UICamera.pixelRect = new Rect(0f, 0f, 1f, 1f);
                    UICamera.rect = new Rect(0f, 0f, 1f, 1f);
                }

                Log.LogInfo($"AspectRatioManager: Adjusted target aspect ratio and UI Camera.");
            }


            // Fix resizable UI camera render textures
            [HarmonyPatch(typeof(GwentUnity.RenderTextureManager), nameof(GwentUnity.RenderTextureManager.GetResizableTexture))]
            [HarmonyPrefix]
            public static bool RenTex1(GwentUnity.RenderTextureManager __instance, ref RectTransform __0, ref float __1, ref float __2, ref float __3)
            {
                __1 = (float)1600 * AspectMultiplier;
                Log.LogInfo($"ResizableTexture: Resized - {__0.name}.");
                return true;
            }

            // Fix screen size UI camera render textures
            [HarmonyPatch(typeof(GwentUnity.RenderTextureAssociator), nameof(GwentUnity.RenderTextureAssociator.Associate))]
            [HarmonyPostfix]
            public static void RenTex2(GwentUnity.RenderTextureAssociator __instance)
            {
                if (__instance.GetComponent<RectTransform>() != null)
                {
                    var transform = __instance.GetComponent<RectTransform>();
                    if (transform != null & transform.sizeDelta == NativeCanvasSize)
                    {
                        transform.sizeDelta = NewCanvasSize;
                        Log.LogInfo($"ScreenSizeTexture: Resized - {transform.name}.");
                    }
                }
            }


            // Fix canteen/dialog render textures
            [HarmonyPatch(typeof(GwentUnity.Stage), nameof(GwentUnity.Stage.SetRenderTextureImageTarget))]
            [HarmonyPostfix]
            public static void RenTex3(GwentUnity.Stage __instance, ref RawImage __0)
            {
                // Check if screen size render texture
                if (__instance.m_RenderTexture.width == (int)fDesiredResolutionX.Value)
                {
                    __0.gameObject.transform.localScale = new Vector3((float)1 * AspectMultiplier, 1f, 1f);
                    Log.LogInfo($"SetRenderTextureImageTarget: Resize: rawImageTarget = {__instance.m_RawImageTarget.gameObject.name}, rawImage = {__0.gameObject.name}, renTex = {__instance.m_RenderTexture.name}.");
                }
            }

            // Load more grid squares
            [HarmonyPatch(typeof(GwentUnity.GridManager), nameof(GwentUnity.GridManager.LoadGridSquaresForCurrentTrackedPosition))]
            [HarmonyPrefix]
            public static bool LoadMoreGridSquares(GwentUnity.GridManager __instance)
            {
                if (NewAspectRatio > 2.39f)
                {
                    __instance.m_CurrentGridSquareLoadLevel = GwentUnity.GridManager.EGridSquareLoadLevel.Level1ZoomedOut;
                    Log.LogInfo($"GridLoadFix: Set GridSquareLoadLevel to {__instance.m_CurrentGridSquareLoadLevel}.");
                }
                if (NewAspectRatio > 4.79f)
                {
                    __instance.m_CurrentGridSquareLoadLevel = GwentUnity.GridManager.EGridSquareLoadLevel.Level2ZoomedOut;
                    Log.LogInfo($"GridLoadFix: Set GridSquareLoadLevel to {__instance.m_CurrentGridSquareLoadLevel}.");
                }
                return true;
            }

            // Remove background from exploration buttons
            [HarmonyPatch(typeof(GwentVisuals.Campaign.ExplorationButtonsView), nameof(GwentVisuals.Campaign.ExplorationButtonsView.HandleShown))]
            [HarmonyPostfix]
            public static void RemoveExplorationBG(GwentVisuals.Campaign.ExplorationButtonsView __instance)
            {
                var bg = __instance.gameObject.transform.FindChild("Background");
                bg.gameObject.SetActive(false);
                Log.LogInfo($"ExplorationButtons: Disabled background.");
            }

            // Widen weather overlay
            [HarmonyPatch(typeof(GwentVisuals.Campaign.Weather), nameof(GwentVisuals.Campaign.Weather.SetActiveness))]
            [HarmonyPostfix]
            public static void RemoveExplorationBG(GwentVisuals.Campaign.Weather __instance)
            {
                if (NewAspectRatio > 2.39f)
                {
                    var overlays = __instance.Overlays;
                    foreach (var overlay in overlays)
                    {
                        var overlayScale = overlay.transform.localScale;
                        float multiplier = (float)NewAspectRatio / 2.4f;
                        overlay.transform.localScale = new Vector2(overlayScale.x * multiplier, overlayScale.y);
                        overlayScale = new Vector2(0f, 0f);
                        Log.LogInfo($"Weather: Resized weather overlay - {overlay.name}.");
                    }
                }
            }

            // Fix marker offset
            [HarmonyPatch(typeof(GwentVisuals.Campaign.WorldPositionTracker), nameof(GwentVisuals.Campaign.WorldPositionTracker.Awake))]
            [HarmonyPostfix]
            public static void FixMarkerOffset(GwentVisuals.Campaign.WorldPositionTracker __instance)
            {
                __instance.m_PanelRectSize = GameObject.Find("MainCanvas").GetComponent<RectTransform>().sizeDelta;
                Log.LogInfo($"MarkerFix: Adjusted panel rect size - {__instance.gameObject.name}");
            }

            // Widen letterboxing in cutscenes
            [HarmonyPatch(typeof(GwentVisuals.Campaign.CinematicModeBarsView), nameof(GwentVisuals.Campaign.CinematicModeBarsView.RefreshHiddenPosition))]
            [HarmonyPostfix]
            public static void LetterboxingStretch(GwentVisuals.Campaign.CinematicModeBarsView __instance)
            {
                var top = __instance.m_TopBar;
                var bottom = __instance.m_BottomBar;

                if (top && bottom)
                {
                    top.localScale = new Vector3((float)1 * AspectMultiplier, 1f, 1f);
                    bottom.localScale = new Vector3((float)1 * AspectMultiplier, 1f, 1f);
                    Log.LogInfo($"LetterboxingFix: Widened letterboxing.");
                }
            }

            // Fix video playback
            [HarmonyPatch(typeof(GwentUnity.CutsceneMoviePlayer.CutscenePlayer), nameof(GwentUnity.CutsceneMoviePlayer.CutscenePlayer.OnMovieStarted))]
            [HarmonyPostfix]
            public static void FixVideo(GwentUnity.CutsceneMoviePlayer.CutscenePlayer __instance)
            {
                __instance.m_MoviePlayer.m_Player.aspectRatio = UnityEngine.Video.VideoAspectRatio.FitVertically;
                Log.LogInfo($"VideoFix: Set aspect ratio to FitVertically - {__instance.m_MoviePlayer.MovieName}.");
            }
        }

        [HarmonyPatch]
        public class IntroSkipPatch
        {
            public static bool bAutosaveRunOnce = false;

            // Skip autosave notice at start
            [HarmonyPatch(typeof(GwentUnity.UISaveNotificationPanel), nameof(GwentUnity.UISaveNotificationPanel.HandleShown))]
            [HarmonyPostfix]
            public static void SkipAutosaveNotice(GwentUnity.UISaveNotificationPanel __instance)
            {
                if (bAutosaveRunOnce)
                {
                    __instance.HandleHidden();
                    Log.LogInfo($"IntroSkip: Skipped autosave notification.");
                    bAutosaveRunOnce = true;
                }
            }

            // Skip intro
            [HarmonyPatch(typeof(GwentUnity.AppIntroState), nameof(GwentUnity.AppIntroState.PlayIntroFlowChart))]
            [HarmonyPostfix]
            public static void SkipIntro(GwentUnity.AppIntroState __instance)
            {
                __instance.m_ShouldProceedFurther = true;
                __instance.ProceedToMenus();
                Log.LogInfo($"IntroSkip: Skipped intro.");
            }
        }
#if DEBUG
        [HarmonyPatch]
        public class SpannedUIPatch
        {
            // Aspect Ratio
            public static float DefaultAspectRatio = (float)16 / 9;
            public static float NewAspectRatio = (float)fDesiredResolutionX.Value / fDesiredResolutionY.Value;
            public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;
            public static float AspectDivider = DefaultAspectRatio / NewAspectRatio;

            [HarmonyPatch(typeof(GwentUnity.OverlayCanvasScaler), nameof(GwentUnity.OverlayCanvasScaler.Start))]
            [HarmonyPostfix]
            public static void ChangeAA(GwentUnity.OverlayCanvasScaler __instance)
            {
                var rects = __instance.GetComponentsInChildren<RectTransform>(true);
                foreach (var rect in rects)
                {
                    if (rect.sizeDelta == new Vector2((float)1600, (float)900))
                    {
                        rect.sizeDelta = new Vector2((float)1600 * AspectMultiplier, (float)900);
                        Log.LogInfo($"SpannedUI: Resized {rect.name}");
                    }
                }
            }

        }
#endif
    }
}
