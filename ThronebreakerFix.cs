using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

using System;
using UnityEngine;
using UnityEngine.UI;

namespace ThronebreakerFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class TB : BasePlugin
    {
        internal static new ManualLogSource Log;

        public static ConfigEntry<bool> bUltrawideFixes;
        public static ConfigEntry<bool> bCustomResolution;
        public static ConfigEntry<float> fDesiredResolutionX;
        public static ConfigEntry<float> fDesiredResolutionY;
        public static ConfigEntry<bool> bFullscreen;
        public static ConfigEntry<bool> bIntroSkip;
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

            // Graphics
            sAAType = Config.Bind("Graphics",
                                "Anti-Aliasing",
                                "None",
                                new ConfigDescription("Set desired anti-aliasing type.",
                                new AcceptableValueList<string>("None", "FXAA", "SMAA", "TAA"))); // Others are broken/invalid

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
            public static float DefaultAspectRatio = (float)16 / 9;
            public static float NewAspectRatio = (float)fDesiredResolutionX.Value / fDesiredResolutionY.Value;
            public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;
            public static float AspectDivider = DefaultAspectRatio / NewAspectRatio;

            // Force cursor on
            // DEBUG
            // REMOVE ON RELEASE
            [HarmonyPatch(typeof(Cursor), nameof(Cursor.visible), MethodType.Setter)]
            [HarmonyPrefix]
            public static bool ForceCursorOn()
            {
                return false;
            }

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
                    if (transform != null & transform.sizeDelta == new Vector2(1600f, 900f))
                    {
                        transform.sizeDelta = new Vector2((float)1600 * AspectMultiplier, (float)900);
                        Log.LogInfo($"ScreenSizeTexture: Resized - {transform.name}.");
                    }
                }        
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
                var overlays = __instance.Overlays;
                foreach (var overlay in overlays)
                {
                    var overlayScale = overlay.transform.localScale;
                    overlay.transform.localScale = new Vector2(overlayScale.x * AspectMultiplier, overlayScale.y);
                    overlayScale = new Vector2(0f,0f);
                    Log.LogInfo($"Weather: Resized weather overlay - {overlay.name}.");
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
            // Skip autosave notice at start
            [HarmonyPatch(typeof(GwentUnity.UISaveNotificationPanel), nameof(GwentUnity.UISaveNotificationPanel.HandleShown))]
            [HarmonyPostfix]
            public static void SkipAutosaveNotice(GwentUnity.UISaveNotificationPanel __instance)
            {
                __instance.HandleHidden();
                Log.LogInfo($"IntroSkip: Skipped autosave notification.");
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
    }
}
