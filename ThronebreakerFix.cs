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

        public override void Load()
        {
            // Plugin startup logic
            Log = base.Log;
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // Features
            bUltrawideFixes = Config.Bind("Ultrawide UI Fixes",
                                "UltrawideFixes",
                                true,
                                "Set to true to enable ultrawide UI fixes.");

            bIntroSkip = Config.Bind("Intro Skip",
                                "IntroSkip",
                                 true,
                                "Skip intro logos.");

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
                Harmony.CreateAndPatchAll(typeof(CustomResolutionPatch));
            }

            // Run UltrawidePatch
            if (bUltrawideFixes.Value)
            {
                Harmony.CreateAndPatchAll(typeof(UltrawidePatch));
            }

            // Run IntroSkipPatch
            if (bIntroSkip.Value)
            {
                Harmony.CreateAndPatchAll(typeof(IntroSkipPatch));
            }

        }

        [HarmonyPatch]
        public class CustomResolutionPatch
        {
            [HarmonyPatch(typeof(GwentUnity.ResolutionNodeWrapper), nameof(GwentUnity.ResolutionNodeWrapper.ApplyResolution), new Type[] { typeof(int), typeof(int), typeof(bool) })]
            [HarmonyPrefix]
            public static bool ApplyCustomRes(ref int __0, ref int __1, ref bool __2)
            {
                __0 = (int)fDesiredResolutionX.Value;
                __1 = (int)fDesiredResolutionY.Value;
                __2 = bFullscreen.Value;
                Log.LogInfo($"GwentUnity.ResolutionNodeWrapper.ApplyResolution: Resolution set to {__0}x{__1}. Fullscreen = {__2}");
                return true;
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

            // Fix resizable UI camera render textures
            [HarmonyPatch(typeof(GwentUnity.RenderTextureManager), nameof(GwentUnity.RenderTextureManager.GetResizableTexture))]
            [HarmonyPrefix]
            public static bool RenTex1(GwentUnity.RenderTextureManager __instance, ref RectTransform __0, ref float __1, ref float __2, ref float __3)
            {
                __1 = (float)1600 * AspectMultiplier;
                Log.LogInfo($"ResizableTexture: Resized {__0.name}.");
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
                        Log.LogInfo($"ScreenSizeTexture: Resized {transform.name}.");
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
                    Log.LogInfo($"Weather: Resized weather overlay {overlay.name}.");
                }
            }

            // Fix UI Camera aspect ratio
            [HarmonyPatch(typeof(GwentUnity.AspectRatioManager), nameof(GwentUnity.AspectRatioManager.SetupAspectRatio))]
            [HarmonyPostfix]
            public static void AspectRatio(GwentUnity.AspectRatioManager __instance)
            {
                __instance.m_RatioToMaintain = NewAspectRatio;

                if (CrimsonUI.CrimsonUIManager.Instance != null)
                {
                    var UICamera = CrimsonUI.CrimsonUIManager.Instance.UICamera;
                    UICamera.aspect = NewAspectRatio;
                    UICamera.pixelRect = new Rect(0f, 0f, 1f, 1f);
                    UICamera.rect = new Rect(0f, 0f, 1f, 1f);
                }

                Log.LogInfo($"AspectRatioManager: Adjusted target aspect ratio and UI Camera.");
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
