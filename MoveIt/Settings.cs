using ColossalFramework;
using System;

namespace MoveIt
{
    public class Settings
    {
        public const string settingsFileName = "MoveItTool";

        public static SavedBool hideChangesWindow = new SavedBool("hideChanges297", settingsFileName, false, true);
        public static SavedBool autoCloseAlignTools = new SavedBool("autoCloseAlignTools", settingsFileName, false, true);
        public static SavedBool POShowDeleteWarning = new SavedBool("POShowDeleteWarning", settingsFileName, true, true);
        public static SavedBool useCardinalMoves = new SavedBool("useCardinalMoves", settingsFileName, false, true);
        public static SavedBool rmbCancelsCloning = new SavedBool("rmbCancelsCloning", settingsFileName, false, true);
        public static SavedBool advancedPillarControl = new SavedBool("advancedPillarControl", settingsFileName, false, true);
        public static SavedBool fastMove = new SavedBool("fastMove", settingsFileName, false, true);
        public static SavedBool altSelectNodeBuildings = new SavedBool("altSelectNodeBuildings", settingsFileName, false, true);
        public static SavedBool altSelectSegmentNodes = new SavedBool("altSelectSegmentNodes", settingsFileName, true, true);
        public static SavedBool followTerrainModeEnabled = new SavedBool("followTerrainModeEnabled", settingsFileName, true, true);
        public static SavedBool showDebugPanel = new SavedBool("showDebugPanel", settingsFileName, false, true);
        public static SavedBool useUUI = new SavedBool("useUUI", settingsFileName, false, true);
        public static SavedBool autoMergeNodes = new SavedBool("autoMergeNodes", settingsFileName, true, true);

        public static void Init()
        {
            try
            {
                // Creating setting file
                if (GameSettings.FindSettingsFileByName(settingsFileName) == null)
                {
                    GameSettings.AddSettingsFile(new SettingsFile[] { new SettingsFile() { fileName = settingsFileName } });
                }
            }
            catch (Exception e)
            {
                DebugUtils.Log("Could not load/create the setting file.");
                DebugUtils.LogException(e);
            }
        }
    }
}
