using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace AutoSync
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AutoSyncPlugin : BaseUnityPlugin
    {
        private const string ModGUID = "Harchytek.AutoSync";
        private const string ModName = "AutoSync";
        private const string ModVersion = "1.0.0";

        public static ManualLogSource MLog;

        private ConfigEntry<bool> ConfigEnabled;
        private ConfigEntry<bool> ConfigAutoDisable;
        private ConfigEntry<bool> ConfigUserCustomEnabled;

        private ConfigEntry<string> ConfigSourceProfile;
        private ConfigEntry<string> ConfigFilesToSync;

        private ConfigEntry<string> ConfigExternalSources;
        private ConfigEntry<string> ConfigExternalDestinations;

        private ConfigFile UserConfigFile;
        private ConfigEntry<string> ConfigUserSourceProfile;
        private ConfigEntry<bool> ConfigUserCustomConfigSync;
        private ConfigEntry<string> ConfigUserFilesToSync;
        private ConfigEntry<string> ConfigUserExternalSources;
        private ConfigEntry<string> ConfigUserExternalDestinations;

        private void Awake()
        {
            MLog = Logger;

            ConfigEnabled = Config.Bind("1. General", "Enabled", true, 
                "Set to 'true' to synchronize on startup.");

            ConfigAutoDisable = Config.Bind("1. General", "AutoDisable", false, 
                "If 'true', the 'Enabled' option will automatically revert to 'false' after a successful synchronization.");

            ConfigUserCustomEnabled = Config.Bind("1. General", "UserCustomEnabled", true, 
                "Set to 'true' to allow synchronization of personal user files and settings from UsersCustom.cfg.");

            ConfigSourceProfile = Config.Bind("2. ModManager Source", "SourceProfile", "LastProfile", 
                "Set to 'LastProfile' to use the last modified profile from the manager, or specify a precise profile name/path.");

            ConfigFilesToSync = Config.Bind("2. ModManager Source", "FilesToSync", "", 
                "Files to synchronize from the mod manager profile source.\nExample value: BepInEx/config/quick_connect_servers.cfg");

            ConfigExternalSources = Config.Bind("3. External Source", "ExternalSources", "", 
                "List of full paths for external files to copy, separated by commas.\nExample value: %USERPROFILE%/Downloads/HDPortalFX/HDPortalFX.dll");

            ConfigExternalDestinations = Config.Bind("3. External Source", "ExternalDestinations", "", 
                "List of corresponding destination paths (in the same order), separated by commas.\nExample value: plugins/HDPortalFX.dll");

            string userConfigFileName = $"{ModGUID}.UsersCustom.cfg";
            string userConfigPath = Path.Combine(Paths.ConfigPath, userConfigFileName);
            UserConfigFile = new ConfigFile(userConfigPath, true);

            ConfigUserSourceProfile = UserConfigFile.Bind("1. Source", "SourceProfile", "LastProfile", 
                "Source profile to pull personal files from. Set to 'LastProfile' or specify a profile name/path.");

            ConfigUserCustomConfigSync = UserConfigFile.Bind("1. Source", "UserCustomConfigSync", true, 
                "Set to 'true' to automatically synchronize this UsersCustom.cfg file itself from the source profile.");

            ConfigUserFilesToSync = UserConfigFile.Bind("2. Files", "UserFilesToSync", "", 
                "Add here your personal files to synchronize from your user source profile, separated by commas.\nExample value: BepInEx/config/BepInEx.cfg");

            ConfigUserExternalSources = UserConfigFile.Bind("3. External Source", "ExternalSources", "", 
                "List of full paths for external personal files to copy, separated by commas.\nExample value: %USERPROFILE%/Downloads/MyMod.dll");

            ConfigUserExternalDestinations = UserConfigFile.Bind("3. External Source", "ExternalDestinations", "", 
                "List of corresponding destination paths for external personal files (in the same order), separated by commas.\nExample value: plugins/MyMod.dll");

            if (!ConfigEnabled.Value)
            {
                MLog.LogInfo("Disabled in configuration.");
                return;
            }

            MLog.LogWarning("Synchronization started...");

            try 
            { 
                bool syncSuccess = ExecuteDualSync(userConfigFileName); 
                
                if (syncSuccess && ConfigAutoDisable.Value)
                {
                    ConfigEnabled.Value = false;
                    Config.Save();
                    MLog.LogWarning("Synchronization completed successfully. 'Enabled' set to false.");
                }
                else if (!syncSuccess && ConfigAutoDisable.Value)
                {
                    MLog.LogWarning("Synchronization completed with errors. 'Enabled' remains active to retry.");
                }
                else
                {
                    MLog.LogWarning("Synchronization completed.");
                }
            }
            catch (Exception ex) 
            { 
                MLog.LogError($"Critical error: {ex.Message}"); 
            }
        }

        private bool ExecuteDualSync(string userConfigFileName)
        {
            bool globalSuccess = true;
            string currentBepInEx = Paths.BepInExRootPath;
            string currentProfileRoot = Directory.GetParent(currentBepInEx)?.FullName;
            string profilesDir = Directory.GetParent(currentProfileRoot)?.FullName;

            string[] baseFiles = ConfigFilesToSync.Value.Split(',')
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => f.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
                .Distinct()
                .ToArray();

            if (baseFiles.Length > 0)
            {
                globalSuccess &= SyncProfileFiles(baseFiles, ConfigSourceProfile.Value.Trim(), profilesDir, currentProfileRoot, currentBepInEx, "Modpack", false);
            }

            if (ConfigUserCustomEnabled.Value)
            {
                string userSource = ConfigUserSourceProfile.Value.Trim();

                if (ConfigUserCustomConfigSync.Value)
                {
                    string relativeUserConfigRelPath = Path.Combine("BepInEx", "config", userConfigFileName);
                    SyncProfileFiles(new[] { relativeUserConfigRelPath }, userSource, profilesDir, currentProfileRoot, currentBepInEx, "User", true);
                    UserConfigFile.Reload();
                }

                string[] userFiles = ConfigUserFilesToSync.Value.Split(',')
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Select(f => f.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
                    .Distinct()
                    .ToArray();

                if (userFiles.Length > 0)
                {
                    globalSuccess &= SyncProfileFiles(userFiles, userSource, profilesDir, currentProfileRoot, currentBepInEx, "User", false);
                }

                string userExtSources = ConfigUserExternalSources.Value.Trim();
                string userExtDests = ConfigUserExternalDestinations.Value.Trim();

                if (!string.IsNullOrEmpty(userExtSources) && !string.IsNullOrEmpty(userExtDests))
                {
                    globalSuccess &= SyncExternalFiles(userExtSources, userExtDests, currentBepInEx);
                }
            }

            string sourcesSetting = ConfigExternalSources.Value.Trim();
            string destinationsSetting = ConfigExternalDestinations.Value.Trim();

            if (!string.IsNullOrEmpty(sourcesSetting) && !string.IsNullOrEmpty(destinationsSetting))
            {
                globalSuccess &= SyncExternalFiles(sourcesSetting, destinationsSetting, currentBepInEx);
            }

            return globalSuccess;
        }

        private bool SyncExternalFiles(string sourcesSetting, string destinationsSetting, string currentBepInEx)
        {
            bool success = true;
            string[] rawSources = sourcesSetting.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            string[] rawDests = destinationsSetting.Split(',').Select(d => d.Trim()).Where(d => !string.IsNullOrEmpty(d)).ToArray();

            int count = Math.Min(rawSources.Length, rawDests.Length);

            if (count > 0)
            {
                MLog.LogWarning($"Processing {count} external file(s)...");

                for (int i = 0; i < count; i++)
                {
                    string sourceRaw = Environment.ExpandEnvironmentVariables(rawSources[i]);
                    string source = Path.IsPathRooted(sourceRaw) ? sourceRaw : Path.GetFullPath(sourceRaw);

                    string destRaw = rawDests[i];
                    string cleanDest = destRaw.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    
                    if (cleanDest.StartsWith("BepInEx" + Path.DirectorySeparatorChar))
                    {
                        cleanDest = cleanDest.Substring(8);
                    }
                    string target = Path.Combine(currentBepInEx, cleanDest);

                    if (File.Exists(source))
                    {
                        if (File.Exists(target) && target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            MLog.LogWarning($"External .dll already present, skipped: {Path.GetFileName(target)}");
                            continue;
                        }

                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            File.Copy(source, target, true);
                            MLog.LogWarning($"External copy successful: {Path.GetFileName(target)} -> External Source");
                        }
                        catch (Exception ex)
                        {
                            MLog.LogError($"External copy error ({Path.GetFileName(target)}): {ex.Message}");
                            success = false;
                        }
                    }
                    else
                    {
                        MLog.LogWarning($"External file not found (skipped): {Path.GetFileName(source)}");
                    }
                }
            }

            return success;
        }

        private bool SyncProfileFiles(string[] files, string sourceSetting, string profilesDir, string currentProfileRoot, string currentBepInEx, string prefixLabel, bool isOptional)
        {
            DirectoryInfo sourceProfile = null;

            if (string.Equals(sourceSetting, "LastProfile", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(profilesDir) && Directory.Exists(profilesDir))
                {
                    sourceProfile = new DirectoryInfo(profilesDir).GetDirectories()
                        .Where(d => !d.FullName.Equals(currentProfileRoot, StringComparison.OrdinalIgnoreCase)
                                 && Directory.Exists(Path.Combine(d.FullName, "BepInEx")))
                        .OrderByDescending(d => d.LastWriteTime)
                        .FirstOrDefault();
                }
            }
            else
            {
                if (Path.IsPathRooted(sourceSetting))
                {
                    if (Directory.Exists(sourceSetting)) sourceProfile = new DirectoryInfo(sourceSetting);
                }
                else
                {
                    string customProfilePath = Path.Combine(profilesDir, sourceSetting);
                    if (Directory.Exists(customProfilePath)) sourceProfile = new DirectoryInfo(customProfilePath);
                }
            }

            if (sourceProfile == null)
            {
                if (string.Equals(sourceSetting, "LastProfile", StringComparison.OrdinalIgnoreCase))
                {
                    MLog.LogWarning("No other mod manager profile detected (first launch).");
                }
                else
                {
                    string shortSourceName = Path.GetFileName(sourceSetting.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    MLog.LogError($"Profile not found: '{shortSourceName}'.");
                    return false;
                }
                return true;
            }

            bool success = true;
            foreach (string file in files)
            {
                string source = Path.Combine(sourceProfile.FullName, file);
                string cleanFile = file.StartsWith("BepInEx") ? file.Substring(8) : file;
                string target = Path.Combine(currentBepInEx, cleanFile);

                if (File.Exists(source))
                {
                    if (File.Exists(target) && target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        MLog.LogWarning($"{prefixLabel}: .dll already present, skipped {Path.GetFileName(target)}");
                        continue;
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(source, target, true);

                        string fileName = Path.GetFileName(target);
                        MLog.LogWarning($"{prefixLabel} copy successful: {fileName} -> {sourceProfile.Name}");
                    }
                    catch (Exception ex)
                    {
                        MLog.LogError($"Error copying {Path.GetFileName(target)}: {ex.Message}");
                        success = false;
                    }
                }
                else
                {
                    if (isOptional)
                    {
                        MLog.LogWarning($"{prefixLabel}: Optional config not found in source (skipped)");
                    }
                    else
                    {
                        MLog.LogError($"Profile file not found: {Path.GetFileName(source)}");
                        success = false;
                    }
                }
            }

            return success;
        }
    }
}