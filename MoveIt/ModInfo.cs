using ColossalFramework.Globalization;
using ColossalFramework.IO;
using ColossalFramework.UI;
using ICities;
using MoveIt.GUI;
using MoveIt.Lang;
using QCommonLib;
using QCommonLib.Lang;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Error code M83

namespace MoveIt
{
    public class ModInfo : IUserMod
    {
        public ModInfo()
        {
            Settings.Init();
        }

        public static string version = QVersion.Version();
        private readonly string m_shortName = "Move It";
        public string Name => m_shortName + " " + QVersion.Version();
        public string Description => Str.mod_description;

        internal static CultureInfo Culture => QCommon.GetCultureInfo();
        protected LocalizeManager LocalizeManager => Str.LocaleManager;
        protected LocalizeManager QLocalizeManager => QStr.LocaleManager;

        public static readonly string debugPath = Path.Combine(DataLocation.localApplicationData, "MoveIt.log");

        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                LocaleManager.eventLocaleChanged -= MoveItLoader.LocaleChanged;
                MoveItLoader.LocaleChanged();
                LocaleManager.eventLocaleChanged += MoveItLoader.LocaleChanged;

                ModOptions options = new ModOptions(helper, Name);
            }
            catch (Exception e)
            {
                DebugUtils.Log("OnSettingsUI failed");
                DebugUtils.LogException(e);
            }
        }

        public void OnEnabled()
        {
            if (QCommon.Scene ==  QCommon.SceneTypes.Game)
            {
                // basic ingame hot reload
                MoveItLoader.loadMode = LoadMode.NewGame;
                MoveItLoader.InstallMod();
            }

            if (UIView.GetAView() == null)
            { // Game loaded to main menu
                LoadingManager.instance.m_introLoaded += CheckIncompatibleMods;
            }
            else
            { // Mod enabled in Content Manager
                CheckIncompatibleMods();
            }
        }

        public void OnDisabled()
        {
            if (QCommon.Scene == QCommon.SceneTypes.Game)
            {
                // basic in game hot unload
                MoveItLoader.UninstallMod();
            }
        }

        public void CheckIncompatibleMods()
        {
            Dictionary<ulong, string> incompatbleMods = new Dictionary<ulong, string>
            {
                { 2696146165,   "Extended Managers Library 1.0.3" },
                { 2696146766,   "Prop Anarchy 0.7.6" }
            };

            _ = new QIncompatible(incompatbleMods, Log.instance, m_shortName);
        }
    }

    public class Log : QLoggerStatic { }
}
