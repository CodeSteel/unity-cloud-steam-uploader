using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using HuntroxGames.Utils;
using HuntroxGames.Utils.DiscordWebhook;

namespace Game.Builder
{
    [Serializable]
    public class SteamBuildDepot
    {
        public int AppId;
        public int DepotId;
        public string Branch;
        public bool Live;
        
        public SteamBuildDepot(int appId, int depotId, string branch = "default", bool setLive = false)
        {
            AppId = appId;
            DepotId = depotId;
            Branch = branch;
            Live = setLive;
        }
    }
    
    public static class UCBSteamUploader
    {
        private static readonly Dictionary<string, SteamBuildDepot> Builds = new Dictionary<string, SteamBuildDepot>()
        {
            { "windows", new SteamBuildDepot(1541370, 1541373) },
            { "windows-demo", new SteamBuildDepot(3810460, 3810462) },
        };
        private const string BuilderPath = "Assets/Editor/Builder/";
        
        public static void PostExport(string exportPath)
        {
            string steamUser = Environment.GetEnvironmentVariable("STEAM_USER");
            string steamConfig = Environment.GetEnvironmentVariable("STEAM_CONFIG");
            string buildTargetId = Environment.GetEnvironmentVariable("BUILD_TARGET")?.ToLowerInvariant();
            
            if (string.IsNullOrEmpty(steamUser) || string.IsNullOrEmpty(buildTargetId) || string.IsNullOrEmpty(steamConfig))
            {
                Debug.LogWarning("Missing STEAM_USER, STEAM_PASS, STEAM_CONFIG, or BUILD_TARGET environment variables. Skipping Steam upload.");
                return;
            }

            if (!Builds.TryGetValue(buildTargetId, out SteamBuildDepot depotInfo))
            {
                Debug.LogWarning($"No build info found for BUILD_TARGET '{buildTargetId}'. Skipping Steam upload.");
                return;
            }
            
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string exe = Application.platform == RuntimePlatform.WindowsEditor ? "steamcmd.exe" : "steamcmd";
            string steamCmdPath =  Path.GetFullPath(Path.Combine(projectRoot, BuilderPath, exe));
            
            if (!File.Exists(steamCmdPath))
            {
                Debug.LogError($"SteamCMD not found at path: {steamCmdPath}. Skipping Steam upload.");
                return;
            }
            
            string buildDir = Directory.Exists(exportPath)
                ? exportPath
                : File.Exists(exportPath) ? Path.GetDirectoryName(exportPath)
                    : null;

            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                Debug.LogError($"Build directory not found at path: {buildDir}. Skipping Steam upload.");
                return;
            }
            
            string vdfPath = CreateTempVdf(buildDir, depotInfo);
            if (!File.Exists(vdfPath))
            {
                Debug.LogError($"Failed to create temporary VDF file at path: {vdfPath}. Skipping Steam upload.");
                return;
            }

            // decode config
            byte[] configBytes;
            try
            {
                configBytes = Convert.FromBase64String(steamConfig);
            }
            catch (FormatException ex)
            {
                Debug.LogError($"Failed to decode STEAM_CONFIG from base64. Exception: {ex.Message}. Skipping Steam upload.");
                return;
            }
            steamConfig = Encoding.UTF8.GetString(configBytes);
            
            // create config.vdf
            var steamCmdDir = Path.GetDirectoryName(steamCmdPath);
            if (steamCmdDir != null)
            {
                var configDir = Path.Combine(steamCmdDir, "config");
                Directory.CreateDirectory(configDir);
                var destConfigPath = Path.Combine(configDir, "config.vdf");
                File.WriteAllText(destConfigPath, steamConfig, Encoding.UTF8);
            }

            // remove DoNotShip/DontShip folders
            foreach (string dir in Directory.GetDirectories(buildDir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(dir);
                if (name.Contains("DoNotShip", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("DontShip", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        Debug.Log($"Deleted DoNotShip directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to delete DoNotShip directory: {dir}. Exception: {ex.Message}");
                    }
                }
            }

            // start steam process
            string loginArgs = $"+login {steamUser}";
            string uploadArgs = $"+run_app_build \"{EscapeVdf(vdfPath)}\"";
            string arguments = $"{loginArgs} {uploadArgs} +quit";
            
            System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = steamCmdPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = steamCmdDir
            };

            using System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogError(e.Data); };

            // results
            try
            {
                Debug.Log("Starting Steam upload for build target: " + buildTargetId);
                var startTime = Time.realtimeSinceStartup;
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                
                string buildSeconds = (Time.realtimeSinceStartup - startTime).ToString("F2");
                var timestamp = HuntroxGames.Utils.Utils.DateToDiscordTimestamp(DateTime.Now, DiscordTimestampFormat.ShortDate);
                
                if (process.ExitCode == 0)
                {
                    Debug.Log("Steam upload completed successfully in " + buildSeconds + " seconds.");
                    string message = $"**Build Target:** {buildTargetId}\n" +
                                     $"**App ID:** {depotInfo.AppId}\n" +
                                     $"**Depot ID:** {depotInfo.DepotId}\n" +
                                     $"**Branch:** {depotInfo.Branch}\n" +
                                     $"**Set Live:** {(depotInfo.Live ? "Yes" : "No")}\n" +
                                     $"**Duration:** {buildSeconds} seconds\n" +
                                     $"**Date:** {timestamp}";
                    Debug.Log(message);
                    SendDiscordNotification("Build uploaded to steam!", message, "https://partner.steamgames.com/apps/builds/" + depotInfo.AppId, Color.green);
                }
                else
                {
                    Debug.LogError($"Steam upload failed with exit code {process.ExitCode}. Duration: {buildSeconds} seconds.");
                    string message = $"**Build Target:** {buildTargetId}\n" +
                                     $"**App ID:** {depotInfo.AppId}\n" +
                                     $"**Depot ID:** {depotInfo.DepotId}\n" +
                                     $"**Branch:** {depotInfo.Branch}\n" +
                                     $"**Set Live:** {(depotInfo.Live ? "Yes" : "No")}\n" +
                                     $"**Exit Code:** {process.ExitCode}\n" +
                                     $"**Duration:** {buildSeconds} seconds\n" +
                                     $"**Date:** {timestamp}";
                    Debug.LogError(message);
                    SendDiscordNotification("Build failed to upload!", message, string.Empty, Color.red);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception during Steam upload: {ex.Message}");
            }
        }
        
        private static void SendDiscordNotification(string title, string message, string embedUrl, Color embedColor)
        {
            string webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK");
            if (string.IsNullOrEmpty(webhookUrl))
            {
                Debug.LogWarning("Discord webhook URL is not set. Skipping Discord notification.");
                return;
            }

            try
            {
                var discordWebhook = new Webhook()
                    .SetAuthor("Steam Builder")
                    .AddEmbed(Embed.CreateEmbed(title, message, embedColor, embedUrl));
                discordWebhook.SendWebhook(webhookUrl);
                Debug.Log("Sent Discord notification.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send Discord notification: {ex.Message}");
            }
        }

        private static string CreateTempVdf(string buildPath, SteamBuildDepot depotInfo)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ucb_steam_build");
            Directory.CreateDirectory(tempDir);

            string vdfPath = Path.Combine(tempDir, $"app_build_{depotInfo.AppId}.vdf");

            var sb = new StringBuilder();
            sb.AppendLine(@"""appbuild""");
            sb.AppendLine("{");
            sb.AppendLine($@"    ""appid""        ""{depotInfo.AppId}""");
            sb.AppendLine($@"    ""desc""         ""UCBSteamUploader automated build {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC""");
            sb.AppendLine($@"    ""buildoutput""  ""{tempDir}""");

            if (depotInfo.Live && !string.IsNullOrWhiteSpace(depotInfo.Branch))
                sb.AppendLine($@"    ""setlive""      ""{depotInfo.Branch}""");

            sb.AppendLine(@"    ""depots""");
            sb.AppendLine("    {");
            sb.AppendLine($@"        ""{depotInfo.DepotId}""");
            sb.AppendLine("        {");
            sb.AppendLine($@"            ""contentroot"" ""{Path.GetFullPath(buildPath)}""");
            sb.AppendLine(@"            ""filemapping""");
            sb.AppendLine("            {");
            sb.AppendLine(@"                ""localpath""   ""*""");
            sb.AppendLine(@"                ""depotpath""   "".""");
            sb.AppendLine(@"                ""recursive""   ""1""");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(vdfPath, sb.ToString(), Encoding.UTF8);
            return vdfPath;
        }
        
        private static string EscapeVdf(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}