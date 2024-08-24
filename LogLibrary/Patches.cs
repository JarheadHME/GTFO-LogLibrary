using CellMenu;
using HarmonyLib;
using UnityEngine;
using CustomMenuBarButtons;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Injection;
using System.Collections.Generic;
using FluffyUnderware.DevTools;
using Il2CppSystem.Collections.Generic;
using Localization;
using System.Linq;
using LevelGeneration;

namespace LogLibrary;

[HarmonyPatch]
internal static class Patches
{
    [HarmonyPatch(typeof(MainMenuGuiLayer), nameof(MainMenuGuiLayer.Setup))]
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    public static void SetupPrefix()
    {
        CustomPage.EnumValues.Init();
    }

    [HarmonyPatch(typeof(MainMenuGuiLayer), nameof(MainMenuGuiLayer.Setup))]
    [HarmonyPostfix]
    [HarmonyWrapSafe]
    public static void SetupPostfix(MainMenuGuiLayer __instance)
    {
        CM_PageBase pageComponent = GOUtil.SpawnChildAndGetComp<CM_PageBase>(Resources.Load<GameObject>("CM_PageEmpty_CellUI"), __instance.GuiLayerBase.transform);
        pageComponent.gameObject.name = "CM_LogLibrary_CellUI";
        pageComponent.gameObject.AddComponent<CM_PageLogLibrary>().Init(pageComponent);

        __instance.m_pages[(int)CustomPage.EnumValues.CMP_LOGLIBRARY] = pageComponent;
        __instance.m_pages[(int)CustomPage.EnumValues.CMP_LOGLIBRARY].Setup(__instance);

        //
    }

    [HarmonyPatch(typeof(CM_PageBase), nameof(CM_PageBase.Setup))]
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    public static bool Setup(CM_PageBase __instance, MainMenuGuiLayer guiLayer)
    {
        if (_baseCall) return true;

        CM_PageLogLibrary libraryPage = __instance.gameObject.GetComponent<CM_PageLogLibrary>();

        if (libraryPage == null) return true;

        libraryPage.Setup(guiLayer);
        return false;
    }

    public static void SetupBaseCall(CM_PageBase page, MainMenuGuiLayer guiLayer)
    {
        _baseCall = true;
        page.Setup(guiLayer);
        _baseCall = false;
    }

    private static bool _baseCall;

    /*[HarmonyPatch(typeof(CM_SettingsItem), nameof(CM_SettingsItem.SetupSettingsItem))]
    [HarmonyPostfix]
    public static void TryGetDialogSlider(CM_SettingsItem __instance)
    {
        if (__instance.m_cellSettingsId == eCellSettingID.Audio_DialogVolume) // should be the only thing needed to check?
            DialogVolumeObject = __instance.gameObject;
    }*/
    public static GameObject DialogVolumeObject { get; private set; }

    [HarmonyPatch(typeof(CM_PageSettings), nameof(CM_PageSettings.CreateSubmenu))]
    [HarmonyPostfix]
    public static void SettingsPagePatch(CM_PageSettings __instance, uint textId, eSettingsSubMenuId subMenuId, Il2CppSystem.Collections.Generic.List<iSettingsFieldData> settingsData, bool autoSaveEnabled = true, bool resetEnabled = false)
    {
        if (DialogVolumeObject != null) return;
        if (subMenuId != eSettingsSubMenuId.Audio) return;

        iScrollWindowContent scrollWindowItem = GOUtil.SpawnChildAndGetComp<iScrollWindowContent>(__instance.m_settingsItemPrefab);
        scrollWindowItem.TextMeshRoot = __instance.transform;
        CM_SettingsItem SliderItem = scrollWindowItem.Cast<CM_SettingsItem>();

        SliderItem.SetupSettingsItem(__instance, new SettingsFieldData() { Id = eCellSettingID.Audio_DialogVolume, Type = eSettingInputType.FloatSlider }.Cast<iSettingsFieldData>());
        Localization.Text.AddTextUpdater(SliderItem.Cast<ILocalizedTextUpdater>());
        SliderItem.SetAnchor(GuiAnchor.TopLeft);
        SliderItem.ForcePopupLayer();

        __instance.m_allValueHolders.Add(SliderItem.GetComponentInChildren<CM_SettingScrollReceiver>().Cast<iSettingsValueHolder>());

        DialogVolumeObject = SliderItem.gameObject;
    }


    // These two make the volume slider we place able to live change the volume
    [HarmonyPatch(typeof(CM_PageSettings), nameof(CM_PageSettings.SetFloatValue))]
    [HarmonyPrefix]
    public static void SetFloatPatchPrefix(CM_PageSettings __instance, eCellSettingID setting, float value)
    {
        prev_subMenuID = __instance.m_currentSubMenuId;
        if (setting != eCellSettingID.Audio_DialogVolume) return;
        __instance.m_currentSubMenuId = eSettingsSubMenuId.Audio;
    }

    [HarmonyPatch(typeof(CM_PageSettings), nameof(CM_PageSettings.SetFloatValue))]
    [HarmonyPostfix]
    public static void SetFloatPatchPostfix(CM_PageSettings __instance, eCellSettingID setting, float value)
    {
        __instance.m_currentSubMenuId = prev_subMenuID;
        CellSettingsManager.SettingsData.Audio.ApplyAllValues();
    }
    private static eSettingsSubMenuId prev_subMenuID;

    [HarmonyPatch(typeof(Achievement_ReadAllLogs), nameof(Achievement_ReadAllLogs.OnReadLog))]
    [HarmonyPostfix]
    public static void SaveLogData(pLogRead data)
    {
        uint logID = data.ID;
        if (ModdedLogManager.HasModdedLogs
            && ModdedLogManager.m_allLogs.Contains(logID) // Is a log we care about (i.e not AUTO_GEN_STATUS, or codes)
            && !ModdedLogManager.GottenLogs.Contains(logID)) // Isn't a log we've already gotten
        {
            ModdedLogManager.GottenLogs.Add(logID);
            ModdedLogManager.WriteGottenLogFiles();
        }

    }
}

internal static class CustomPage
{
    public static class EnumValues
    {
        public static readonly eCM_MenuPage CMP_LOGLIBRARY;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Init()
        { // just to get the constructor called
        }

        static EnumValues()
        {
            int startIndex = EnumUtil.GetValueLength<eCM_MenuPage>();

            EnumInjector.InjectEnumValues<eCM_MenuPage>(new System.Collections.Generic.Dictionary<string, object>()
            {
                {
                    nameof(CMP_LOGLIBRARY),
                    startIndex
                }
            });

            EnumValues.CMP_LOGLIBRARY = (eCM_MenuPage)startIndex;
        }
    }
}
