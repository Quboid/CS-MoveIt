﻿using ColossalFramework.Globalization;
using ColossalFramework.IO;
using ICities;
using MoveIt.GUI;
using MoveIt.Localization;
using QCommonLib;
using System;
using System.Globalization;
using System.IO;

namespace MoveIt
{
    public class ModInfo : IUserMod
    {
        public ModInfo()
        {
            Settings.Init();
        }

        public static string version = QVersion.Version();
        public string Name => "Move It " + QVersion.Version();
        public string Description => Str.mod_description;

        internal static CultureInfo Culture => QCommon.GetCultureInfo();

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
        }

        public void OnDisabled()
        {
            if (QCommon.Scene == QCommon.SceneTypes.Game)
            {
                // basic in game hot unload
                MoveItLoader.UninstallMod();
            }
        }
    }

}
