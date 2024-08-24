using CellMenu;
using System;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;
using System.Collections.Generic;
using BepInEx;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using GTFO.API.Extensions;
using System.Linq;
using BepInEx.Unity.IL2CPP;
using System.Reflection;

namespace LogLibrary;
internal sealed class CM_PageLogLibrary : MonoBehaviour
{
    private CM_PageBase page = null!;

    public static LogsFileDTO LogInfos { get; set; }
    public static System.Collections.Generic.List<uint> ReadLogs { get; set; } = new();

    private CM_Item[] RundownButtons;
    private CM_ScrollWindow[] LogSelectWindows;

    private CM_GlobalPopup ReadingWindow;
    private CM_ScrollWindow LogScroller;
    private CM_Item[] PageButtons = new CM_Item[2];
    private List<string> CurrLogSplit;
    private int CurrLogPage = 0;
    private string CurrLogName;

    private CM_TimedButton AudioLogButton;
    private uint CurrentAudioLog = 0;
    private bool AudioLogPlaying = false;
    private static CellSoundPlayer s_sound;
    private static Sprite AudioSprite;
    internal void Init(CM_PageBase page)
    {
        this.page = page;
    }
    private void OnDisable()
    {
        HideAllLogSelectWindows();

        MainMenuGuiLayer.Current.PageSettings.ResetAllValueHolders();
    }
    public void Setup(MainMenuGuiLayer guiLayer)
    {
        if (this.page.m_isSetup)
            return;

        GetLogInfo(); 

        this.page.m_spawnMenuBar = true;

        Patches.SetupBaseCall(page, guiLayer);

        // Setup all the different parts we actually needs
        SetupLogWindow();
        SetupLogSelectionWindows();

        var rundownLogs = LogInfos.RundownLogs;
        int numBtns = rundownLogs.Length;
        RundownButtons = new CM_Item[numBtns];
        for (int i = 0; i < numBtns; i++)
        {
            var btn = RundownButtons[i] = this.page.m_guiLayer.AddRectComp("CM_ButtonFramed", GuiAnchor.MidLeft, new Vector2(300, -90 * i + 390), this.page.m_movingContentHolder).TryCast<CM_Item>()!; ;
            
            string CurrRundownName = rundownLogs[i].SectionName;
            btn.SetText(CurrRundownName);
            btn.ID = i+1; // id 0 seems to get changed to something else
            btn.OnBtnPressCallback += (Action<int>)(
                delegate (int id)
                {
                    DisplaySelectWindow(id - 1);
                }
            );
            btn.SetScaleFactor(0.9f);
            btn.UpdateColliderOffset();
        }

        // Can't just resources.load this bitch cause it's in Texture2D so just grab that bitch wholesale
        AudioSprite = this.page.m_guiLayer.PageMap.m_inventory[0].m_voiceControls.m_muteToggleButton.GetComponent<SpriteRenderer>().sprite;
    }

    private void HideAllLogSelectWindows()
    {
        if (LogSelectWindows == null) return;
        foreach (var windowToHide in LogSelectWindows)
            windowToHide.SetVisible(false);
    }

    private void SetupLogWindow()
    {
        this.ReadingWindow = this.page.m_guiLayer.AddRectComp("general/popup/CM_GlobalPopup_BoosterMissedPopup", GuiAnchor.MidCenter, new Vector2(0, 0), this.page.m_staticContentHolder).TryCast<CM_GlobalPopup>()!;
        this.ReadingWindow.gameObject.name = "CM_Popup_ReadLogPopup";
        this.ReadingWindow.m_closeButton.OnBtnPressCallback = (Action<int>)delegate (int id) { 
            this.ReadingWindow.SetVisible(false);
            ResetAudioButton();

            CellSettingsManager.ApplyAndSave();
        };

        // Shift the window forward so that the input-blocking background is in front of the other buttons
        var pos = this.ReadingWindow.transform.localPosition;
        pos.z = -5;
        this.ReadingWindow.transform.localPosition = pos;

        float winWidth = 1500;
        float winHeight = 600;
        this.ReadingWindow.SetSize(new Vector2(winWidth, winHeight));

        // make sure window gets resized before it's shown
        UI_SpriteResizer bgResizer = this.ReadingWindow.transform.FindChild("Background").GetComponent<UI_SpriteResizer>();
        bgResizer.ResizeAllChildren();

        LogScroller = this.page.m_guiLayer.AddRectComp("CM_ScrollWindow", GuiAnchor.MidRight, new Vector2(0, 0), this.ReadingWindow.m_contentHolder).TryCast<CM_ScrollWindow>();
        LogScroller.Setup();
        foreach (Renderer renderer in LogScroller.GetComponentsInChildren<Renderer>())
        {
            renderer.sortingLayerID = SortingLayer.NameToID("GlobalPopupBG"); // change the layers so the scroll bar doesn't get dimmed by the bg
        }

        TextMeshPro uppertext_tmp = this.ReadingWindow.m_upperText;
        LogScroller.transform.FindChild("Header").gameObject.SetActive(false);
        LogScroller.transform.FindChild("Background").gameObject.SetActive(false);
        var trans = uppertext_tmp.GetComponent<RectTransform>();
        trans.parent = LogScroller.m_contentContainer.transform;
        trans.anchorMax = new Vector2(0.81f, 0.27f); // honestly, mildly black magic, in ideal world this isn't needed but whatever lmao
        trans.anchorMin = new Vector2(0.86f, 0);
        trans.sizeDelta = new Vector2(winWidth, winHeight);

        LogScroller.SetSize(new Vector2(winWidth, winHeight));
        LogScroller.m_rectTrans.sizeDelta = new Vector2(winWidth, winHeight);
        LogScroller.m_clipRect.sizeDelta = new Vector2(winWidth, winHeight-20);

        uppertext_tmp.fontSizeMax = 22;
        uppertext_tmp.fontSizeMin = uppertext_tmp.fontSizeMax; // we want them to be the same, so now there's just one num to change
        uppertext_tmp.lineSpacing = 20;
        uppertext_tmp.fontStyle = TMPro.FontStyles.Normal;

        LogScroller.m_posStart = -20; // adjusts for the window being in a slightly weird spot before/after scrolling
        LogScroller.m_windowHeight = winHeight-20;
        LogScroller.m_contentContainerHeight = 1000;
        LogScroller.OnWindowHeightUpdate();

        for (int i = 0; i < PageButtons.Length; i++)
        {
            var btn = this.page.m_guiLayer.AddRectComp("gui/player/boosters/PUI_ArrowButton", GuiAnchor.BottomCenter, new Vector2(-50 + (100 * i), -50), this.ReadingWindow.m_contentHolder).Cast<CM_Item>();
            btn.transform.Rotate(0, 0, 90 + (180 * i));
            btn.GetComponent<SpriteRenderer>().sortingLayerID = SortingLayer.NameToID("GlobalPopupBG");

            if (i == 0)
                btn.OnBtnPressCallback = (Action<int>)delegate (int id) { ChangeLogPage(-1); };
            else
                btn.OnBtnPressCallback = (Action<int>)delegate (int id) { ChangeLogPage(1); };

            btn.gameObject.name = $"CM_ReadLogPage_{new string[] {"Left", "Right"}[i]}";

            PageButtons[i] = btn;
        }

        // Audio log button
        s_sound = new();
        
        AudioLogButton = this.page.m_guiLayer.AddRectComp("CM_TimedExpeditionButton", GuiAnchor.BottomLeft, new Vector2(0, -125), this.ReadingWindow.m_contentHolder).TryCast<CM_TimedButton>(); ;
        AudioLogButton.Setup();
        AudioLogButton.SetText("Play Audio");
        AudioLogButton.SOUND_CLICK_HOLD_DONE = 0; // mainly just cause i'm not a fan of it being there to briefly cover sound lmao
        AudioLogButton.OnBtnPressCallback += (Action<int>)delegate (int id)
        {
            ToggleAudioLogPlayer();
        };

        var volumeSlider = Patches.DialogVolumeObject;
        volumeSlider.transform.parent = this.AudioLogButton.transform;
        var rect = Patches.DialogVolumeObject.GetComponent<RectTransform>();
        rect.localPosition = new Vector2(825, 90);
        volumeSlider.transform.Find("Title/TitleText").GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Right;
        volumeSlider.transform.Find("Background").gameObject.SetActive(false);

        foreach (Renderer renderer in AudioLogButton.GetComponentsInChildren<Renderer>())
        {
            renderer.sortingLayerID = SortingLayer.NameToID("GlobalPopupBG"); // change the layers so the audio button and volume slider don't get dimmed by the bg
        }

        this.ReadingWindow.SetVisible(false);
    }

    private Vector2 SelectionWindowSize = new Vector2(800, 1000);
    private void SetupLogSelectionWindows()
    {
        LogSelectWindows = new CM_ScrollWindow[8];
        // not a foreach so i can get both the Rundown and the window from the index
        for (int i = 0; i < LogSelectWindows.Length; i++)
        {
            CM_ScrollWindow window = this.page.m_guiLayer.AddRectComp("CM_ScrollWindow", GuiAnchor.MidCenter, new Vector2(0, 0), this.page.m_movingContentHolder).TryCast<CM_ScrollWindow>()!;
            window.Setup();

            
            window.SetSize(SelectionWindowSize);
            window.SetVisible(false);
            LogSelectWindows[i] = window;
        }
    }

    [HideFromIl2Cpp]
    private iScrollWindowContent CreateScrollItem(string text, LogDTO log = null, bool clickable = false)
    {
        CM_SettingsItem settingsItem = this.page.m_guiLayer.AddRectComp("settings/CM_SettingsItem", GuiAnchor.TopLeft, new Vector2(0, 0), this.page.m_movingContentHolder).TryCast<CM_SettingsItem>();
        settingsItem.Setup();
        settingsItem.SetText(text);
        settingsItem.m_clickSound = clickable;
        settingsItem.m_alphaTextOnHover = clickable;
        settingsItem.OnHoverOut();
        if (log != null)
        {
            settingsItem.SetOnBtnPressCallback((Action<int>)delegate (int id) { ShowLog(log.filename, Localization.Text.Get(log.id), log.audio); });
            if (log.audio > 0)
            {
                SpriteRenderer spriterenderer = settingsItem.m_inputAlign.gameObject.AddComponent<SpriteRenderer>();
                spriterenderer.sprite = AudioSprite;
                spriterenderer.transform.localScale = new Vector3(25, 25, 1);
                Vector3 spritetransform = spriterenderer.transform.localPosition;
                spritetransform.z = 0;
                spriterenderer.transform.localPosition = spritetransform;
                var rec = spriterenderer.GetComponent<RectTransform>();
                rec.anchorMax = new Vector2(0, 0.518f);
                rec.anchorMin = new Vector2(-0.42f, 0);
                var col = spriterenderer.color;
                col.a = 0.5f;
                spriterenderer.color = col;
                spriterenderer.sortingLayerID = SortingLayer.NameToID("MenuPopupText");
            }
        }

        int item_offset = 50;
        var trans = settingsItem.m_title.GetComponent<RectTransform>();
        Vector2 size = trans.sizeDelta;
        size.x = SelectionWindowSize.x - item_offset;
        trans.sizeDelta = size;

        var collider = settingsItem.m_collider;
        collider.size = size;
        Vector2 offset = new Vector2(size.x / 2, size.y / -2);
        offset.x -= item_offset;
        collider.offset = offset;

        return settingsItem.Cast<iScrollWindowContent>();
    }

    private PopupMessage popup = new PopupMessage()
    {
        Header = "Header",
        UpperText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        LowerText = "",
        BlinkInContent = false,
        PopupType = PopupType.Confirmation
    };
    public void ShowLog(string logName, string text, uint audio = 0)
    {
        /*if (logName.Equals("000-000-000")) {
            Process.Start("explorer.exe", "https://docs.google.com/spreadsheets/d/e/2PACX-1vSLHVp3ReG56bunlm1cuCMwLQxtJkINITk1vVMCgGbrJj-Ce6GWc9TpBNlYYmAujwqh4kSGF-pOjdqb/pubhtml");
            return;
        }*/

        this.CurrLogPage = 0;
        this.CurrLogName = logName;
        this.CurrLogSplit = SplitStringBySize(text);
        popup.UpperText = CurrLogSplit[CurrLogPage]; // always show the first page when opening a log

        SetWindowHeader();

        foreach (var btn in PageButtons)
            btn.SetVisible(this.CurrLogSplit.Count > 1);

        this.ReadingWindow.ShowMessage(popup, null); // null is soundplayer, should end up being fine cause the code that should use it should get skipped?
        this.ReadingWindow.m_headerText.gameObject.SetActive(true);

        this.CurrentAudioLog = audio;
        this.AudioLogButton.SetVisible(audio > 0); // only show button if it has an audio file

        UpdateLogScrollWindow();

        MainMenuGuiLayer.Current.PageSettings.ResetAllValueHolders();
    }

    private static int mod(int k, int n) { return ((k %= n) < 0) ? k + n : k; } // to do proper modulus, so a negative number will wrap back around
    private void ChangeLogPage(int pageChange)
    {
        CurrLogPage = mod(CurrLogPage + pageChange, CurrLogSplit.Count);
        popup.UpperText = CurrLogSplit[CurrLogPage];
        SetWindowHeader();
        this.ReadingWindow.ShowMessage(popup, null);
        this.ReadingWindow.m_headerText.gameObject.SetActive(true);
        UpdateLogScrollWindow();
    }

    private void SetWindowHeader()
    {
        popup.Header = $"{this.CurrLogName}{(this.CurrLogSplit.Count > 1 ? $" {CurrLogPage + 1} / {CurrLogSplit.Count}" : "")}";
    }


    private const int occurrenceToSplitOn = 200;
    // Will split the string by every 200th newline
    private static List<string> SplitStringBySize(string input)
    {
        List<string> outstrlist = new List<string>();
        List<string> split = input.Split('\n').ToList();
        while (split.Count > 0)
        {
            outstrlist.Add(string.Join<string>('\n', split.GetRange(0, Math.Min(occurrenceToSplitOn, split.Count))));
            split.RemoveRange(0, Math.Min(occurrenceToSplitOn, split.Count));
        }
        return outstrlist;

    }

    private void UpdateLogScrollWindow()
    {
        LogScroller.ScrollFromHandle(float.NegativeInfinity);
        LogScroller.m_contentContainerHeight = this.ReadingWindow.m_upperText.GetPreferredHeight() + 50;
        LogScroller.OnWindowHeightUpdate();
    }

    public void DisplaySelectWindow(int id)
    {
        HideAllLogSelectWindows();
        UpdateReadLogs();

        var window = LogSelectWindows[id];

        var currRD = LogInfos.RundownLogs[id];
        string RundownName = currRD.SectionName;

        // Actually set the logs into the window
        var items = new List<iScrollWindowContent>();

        window.SetHeader($"<b>{RundownName} Logs</b>");

        bool first = true;
        foreach (var expedition in currRD.Expeditions)
        {
            if (!first)
            {
                // spacer between expeditions
                items.Add(CreateScrollItem("<color=#202020>__________________________________________</color>"));
            }
            first = false;
            // expedition name and separator
            items.Add(CreateScrollItem($"   <b><u><color=white>{expedition.expedition}</color></u></b>"));
            foreach (var log in expedition.logs)
            {
                if (ReadLogs.Contains(log.id) || LogInfos.Dev)
                    items.Add(CreateScrollItem($"   {log.filename} - Found in {log.zone}", log: log, clickable: true));
                else
                {
                    if (LogInfos.HideUngotten)
                        items.Add(CreateScrollItem($"<color=#101010>   {new String('?', log.filename.Length)} - Found in {new String('?', log.zone.Length)}</color>", clickable: true));
                    else
                        items.Add(CreateScrollItem($"<color=#101010>   {log.filename} - Found in {log.zone}</color>", clickable: true));
                }
            }
        }
        window.SetContentItems(items.ToIl2Cpp());
        LogSelectWindows[id].SetVisible(true);
    }

    private void ToggleAudioLogPlayer()
    {
        if (AudioLogPlaying)
        {
            ResetAudioButton();
        }
        else
        {
            // Posts the thing with a callback to turn off the button once it finishes playing
            s_sound.Post(this.CurrentAudioLog, 1U, (AkCallbackManager.EventCallback)((Action<Il2CppSystem.Object, AkCallbackType, AkCallbackInfo>)AudioDoneCallback), this.CurrentAudioLog);
            this.AudioLogButton.SetText("Stop Audio");
            AudioLogPlaying = true;
        }
    }
    private void ResetAudioButton()
    {
        s_sound.Stop();
        this.AudioLogButton.SetText("Play Audio");
        AudioLogPlaying = false;
    }

    private void AudioDoneCallback(Il2CppSystem.Object cookie, AkCallbackType type, AkCallbackInfo callbackInfo)
    {
        ResetAudioButton();
    }

    static CM_PageLogLibrary()
    {
        ClassInjector.RegisterTypeInIl2Cpp<CM_PageLogLibrary>();
    }

    // Gets all the log from the json file
    private const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
    public static void GetLogInfo()
    {
        if (IL2CPPChainloader.Instance.Plugins.TryGetValue(MTFO.MTFO.GUID, out var mtfoInfo)) {
            var MTFO = mtfoInfo.Instance;
            Type PathAPIType = MTFO?.GetType()?.Assembly?.GetType("MTFO.API.MTFOPathAPI") ?? null;
            if (PathAPIType != null)
            {
                bool HasCustomPath = (bool)(PathAPIType.GetProperty("HasCustomPath", PUBLIC_STATIC)?.GetValue(null) ?? false);
                string CustomPath = (string)(PathAPIType.GetProperty("CustomPath", PUBLIC_STATIC)?.GetValue(null) ?? null);
                if (HasCustomPath && !string.IsNullOrEmpty(CustomPath)) // just double checking lol
                {
                    string path = Path.Join(CustomPath, "LogLibrary", "Logs.json");
                    if (File.Exists(path))
                    {
                        ReadLogInfosFile(path);
                        Logger.Info("Using Modded Logs");
                        ModdedLogManager.HasModdedLogs = true;
                        return;
                    }

                }
            }
        }
        string defaultPath = Path.Join(Paths.ConfigPath, "LogLibraryInfo.json");
        ReadLogInfosFile(defaultPath);
    }

    public static void ReadLogInfosFile(string path)
    {
        LogInfos = GTFO.API.JSON.JsonSerializer.Deserialize<LogsFileDTO>(File.ReadAllText(path));
        if (string.IsNullOrEmpty(LogInfos.Name))
        {
            Logger.Error("'Name' field in provided Log Info file is unset, please contact your rundown developer or access `(RundownFolder)/Custom/LogLibrary/Logs.json` to set it.\nFor now, it will use 'UNSET'.");
            LogInfos.Name = "UNSET";
        }

    }

    public static void UpdateReadLogs()
    {
        if (!ModdedLogManager.HasModdedLogs)
        {
            var achManager = AchievementManager.Current;
            foreach (var ach in achManager.m_allAchievements)
            {
                var logManager = ach.TryCast<Achievement_ReadAllLogs>();
                if (logManager != null)
                {
                    ReadLogs = logManager.m_readLogsOnStart.toManaged();
                    return;
                }
            }
            Logger.Error("Isn't modded but didn't find achievement manager??");
        }
        // Has Modded Log file
        ReadLogs = ModdedLogManager.GottenLogs;
    }
}

public class LogsFileDTO
{
    public string Name { get; set; } // File name to save log info to
    public bool HideUngotten { get; set; } = true;
    public RundownLogsDTO[] RundownLogs { get; set; }
    public bool Dev { get; set;} = false;
}
public class RundownLogsDTO
{
    public string SectionName { get; set; } // Text that shows up on the buttons and on the window headers
    public ExpeditionLogsDTO[] Expeditions { get; set; }
}

public class ExpeditionLogsDTO
{
    public string expedition { get; set; }
    public LogDTO[] logs { get; set; }
}
public class LogDTO
{
    public string filename { get; set; }
    public uint id { get; set; }
    public string zone { get; set; }
    public uint audio { get; set; }
}
