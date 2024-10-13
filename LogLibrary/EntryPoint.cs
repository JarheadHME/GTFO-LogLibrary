using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Linq;
using CustomMenuBarButtons;
using CellMenu;
namespace LogLibrary
{
    [BepInPlugin("JarheadHME.LogLibrary", "LogLibrary", VersionInfo.Version)]
    [BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("dev.flaff.gtfo.CustomMenuBarButtons", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(MTFO.MTFO.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MTFO.Extension.PartialBlocks", BepInDependency.DependencyFlags.SoftDependency)]
    internal class EntryPoint : BasePlugin
    {
        private Harmony _Harmony = null;

        public override void Load()
        {
            _Harmony = new Harmony($"{VersionInfo.RootNamespace}.Harmony");
            _Harmony.PatchAll();
            Logger.Info($"Plugin has loaded with {_Harmony.GetPatchedMethods().Count()} patches!");

            MenuBarHandler.CreateButtons += CreateMenuButton;
        }

        private static void CreateMenuButton(MenuBarHandler menubar)
        {
            menubar.AddLeftButton(
                (handler, item) => new LogLibraryButton(handler, item),
                insertIndex: int.MaxValue,
                page: eCM_MenuPage.CMP_EXPEDITION_FAIL
                );
        }

        public override bool Unload()
        {
            _Harmony.UnpatchSelf();
            return base.Unload();
        }
    }

    public sealed class LogLibraryButton(MenuBarHandler handler, CM_MenuBarItem item) : MenuBarCustomButton(handler, item, "Library")
    {
        public sealed override bool ShouldBeVisible => true;

        protected sealed override void OnButtonClick(int id)
        {
            GuiManager.MainMenuLayer.ChangePage(CustomPage.EnumValues.CMP_LOGLIBRARY);
        }
    }

}