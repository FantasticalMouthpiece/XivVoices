using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using XivVoices.Engine;

namespace XivVoices;

public class PluginWindow : Window, IDisposable
{
    public Plugin PluginReference { get; internal set; }

    public string currentTab = "General";
    private string selectedDrive = string.Empty;

    private uint GeneralSettingsIconId = 1;
    private uint DialogueSettingsIconId = 29;
    private uint AudioSettingsIconId = 36;
    private uint AudioLogsIconId = 45;
    private uint WineSettingsIconId = 24423;
    private uint ChangelogIconId = 47;

    public PluginWindow() : base("XIVV###XIVV")
    {
        Size = new Vector2(440, 650);
        SizeCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
    }

    public void Dispose()
    {
    }

    private IntPtr GetImGuiHandleForIconId(uint iconId)
    {
        if (Plugin.TextureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var icon))
        {
            return icon.GetWrapOrEmpty().ImGuiHandle;
        }

        return 0;
    }

    public override void Draw()
    {
      if (!Plugin.ClientState.IsLoggedIn)
      {
          ImGui.TextUnformatted("Please login to access and configure settings.");
          return;
      }

      if (!Plugin.Config.Initialized)
      {
          InitializationWindow();
          return;
      }

      if (Updater.Instance.State.Count > 0)
      {
          using (var tabBar = ImRaii.TabBar("ConfigTabs"))
          {
              if (tabBar)
              {
                using (var tabItem = ImRaii.TabItem("  (           Update Process           )  "))
                {
                    if (tabItem)
                    {
                        UpdateWindow();
                    }
                }
                using (var tabItem = ImRaii.TabItem("^"))
                {
                    if (tabItem)
                    {
                        DrawSettings();
                    }
                }
                using (var tabItem = ImRaii.TabItem("Audio Settings"))
                {
                    if (tabItem)
                    {
                        AudioSettings();
                    }
                }
              }
          }

          return;
      }

      // Floating Button ----------------------------------
      var originPos = ImGui.GetCursorPos();
      ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMax().X + (8f * ImGuiHelpers.GlobalScale));
      ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - (26f * ImGuiHelpers.GlobalScale ));
      DrawImageButton("Changelog", currentTab == "Changelog", GetImGuiHandleForIconId(ChangelogIconId));
      ImGui.SetCursorPos(originPos);

      // The sidebar with the tab buttons
      using (var child = ImRaii.Child("Sidebar", new Vector2(50 * ImGuiHelpers.GlobalScale, 500 * ImGuiHelpers.GlobalScale), false))
      {
          if (child)
          {
              DrawSidebarButton("General", GetImGuiHandleForIconId(GeneralSettingsIconId));
              DrawSidebarButton("Dialogue Settings", GetImGuiHandleForIconId(DialogueSettingsIconId));
              DrawSidebarButton("Audio Settings", GetImGuiHandleForIconId(AudioSettingsIconId));
              DrawSidebarButton("Audio Logs", GetImGuiHandleForIconId(AudioLogsIconId));
              if (Dalamud.Utility.Util.IsWine())
                DrawSidebarButton("Wine Settings", GetImGuiHandleForIconId(WineSettingsIconId));

              // Draw the Discord Button
              // dalamud has this cached internally, just get it every frame duh
              var discord = Plugin.TextureProvider.GetFromFile(Path.Combine(Plugin.Interface.AssemblyLocation.Directory?.FullName!, "discord.png")).GetWrapOrDefault();
              if (discord == null) return;
              using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
              {
                  if (ImGui.ImageButton(discord.ImGuiHandle, new Vector2(42 * ImGuiHelpers.GlobalScale, 42 * ImGuiHelpers.GlobalScale)))
                  {
                      var process = new Process();
                      try
                      {
                          // true is the default, but it is important not to set it to false
                          process.StartInfo.UseShellExecute = true;
                          process.StartInfo.FileName = "https://discord.gg/jX2vxDRkyq";
                          process.Start();
                      }
                      catch (Exception e)
                      {
                      }
                  }
              }

              if (ImGui.IsItemHovered())
                  using (ImRaii.Tooltip())
                      ImGui.TextUnformatted("Join Our Discord Community");
          }
      }

      // Draw a vertical line separator
      ImGui.SameLine();
      var drawList = ImGui.GetWindowDrawList();
      var lineStart = ImGui.GetCursorScreenPos() - new Vector2(0, 10);
      var lineEnd = new Vector2(lineStart.X, lineStart.Y + (630 * ImGuiHelpers.GlobalScale));
      var lineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.15f, 1));
      drawList.AddLine(lineStart, lineEnd, lineColor, 1f);
      ImGui.SameLine(85 * ImGuiHelpers.GlobalScale);

      // The content area where the selected tab's contents will be displayed
      using (var group = ImRaii.Group())
      {
          if (currentTab == "General")
              DrawGeneral();
          else if (currentTab == "Dialogue Settings")
              DrawSettings();
          else if (currentTab == "Audio Settings")
              AudioSettings();
          else if (currentTab == "Audio Logs")
              LogsSettings();
          else if (currentTab == "Wine Settings")
              WineSettings();
          else if (currentTab == "Changelog")
              Changelog();
      }
    }

    private void DrawImageButton(string tabName, bool active, IntPtr imageHandle)
    {
        var style = ImGui.GetStyle();
        if (active)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            var screenPos = ImGui.GetCursorScreenPos();
            Vector2 rectMin = screenPos + new Vector2(style.FramePadding.X - 1);
            Vector2 rectMax = screenPos + new Vector2(42 * ImGuiHelpers.GlobalScale + style.FramePadding.X + 1);
            uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.7f, 1.0f, 1.0f));
            drawList.AddRect(rectMin, rectMax, borderColor, 5.0f, ImDrawFlags.None, 2.0f);
        }

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
        {
          Vector4 tintColor = active ? new Vector4(0.6f, 0.8f, 1.0f, 1.0f) : new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
          if (ImGui.ImageButton(imageHandle, new Vector2(42 * ImGuiHelpers.GlobalScale), Vector2.Zero, Vector2.One, (int)style.FramePadding.X, Vector4.Zero, tintColor)) currentTab = tabName;
          
          if (ImGui.IsItemHovered()) 
              using (ImRaii.Tooltip())
                  ImGui.TextUnformatted(tabName);
        }
    }

    private void DrawSidebarButton(string tabName, IntPtr imageHandle)
    {
        DrawImageButton(tabName, currentTab == tabName, imageHandle);
    }

    private void InitializationWindow()
    {
        ImGui.Dummy(new Vector2(0, 10));
        ImGui.Indent(150);
        ImGui.TextWrapped("Xiv Voices Initialization");
        ImGui.Unindent(140);
        ImGui.Dummy(new Vector2(0, 10));
        ImGui.TextWrapped(
            "Choose a working directory that will hold all the voice files in your computer, afterwards press \"Start\" to begin downloading Xiv Voices into your computer.");

        ImGui.Indent(35);
        ImGui.Dummy(new Vector2(0, 20));
        ImGui.Indent(65);

        // dalamud has this cached internally, just get it every frame duh
        var logo = Plugin.TextureProvider.GetFromFile(Path.Combine(Plugin.Interface.AssemblyLocation.Directory?.FullName!, "logo.png")).GetWrapOrDefault();
        if (logo == null) return;
        ImGui.Image(logo.ImGuiHandle, new Vector2(200, 200));

        ImGui.TextWrapped("Working Directory is " + Plugin.Config.WorkingDirectory);
        ImGui.Dummy(new Vector2(0, 10));
        ImGui.Unindent(30);

        // Get available drives
        var allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Where(drive => drive.IsReady).Select(drive => drive.Name.Trim('\\')).ToList();
        var driveNames = drives.ToArray();

        using (var combo = ImRaii.Combo("##Drives", selectedDrive.Length > 0 ? selectedDrive : "Select Drive"))
        {
            if (combo)
            {
                for (int i = 0; i < driveNames.Length; i++)
                {
                    if (ImGui.Selectable(driveNames[i]))
                    {
                        selectedDrive = driveNames[i];
                        Plugin.Config.WorkingDirectory = $"{selectedDrive}/XIV_Voices";
                        Plugin.Config.Save();
                    }
                }
            }
        }

        ImGui.Dummy(new Vector2(0, 50));

        if (ImGui.Button("Start Downloading Xiv Voices", new Vector2(260, 50)))
            if (selectedDrive != string.Empty)
                Updater.Instance.Check();
        ImGui.Unindent(45);
    }

    private void UpdateWindow()
    {
        if (Updater.Instance.State.Contains(1))
        {
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.TextWrapped("Checking Server Manifest...");
        }

        if (Updater.Instance.State.Contains(2))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("Checking Local Manifest...");
        }

        if (Updater.Instance.State.Contains(3))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("Checking Xiv Voices Tools...");
        }

        if (Updater.Instance.State.Contains(-1))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("Error: Unable to load Manifests");
        }

        if (Updater.Instance.State.Contains(4))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("Xiv Voices Tools are Ready.");
        }

        if (Updater.Instance.State.Contains(5))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("Xiv Voices Tools Missing, Downloading..");
        }

        if (Updater.Instance.State.Contains(6))
        {
            ImGui.Dummy(new Vector2(0, 5));
            var progress = Updater.Instance.ToolsDownloadState / 100.0f;
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"{Updater.Instance.ToolsDownloadState}% Complete");
            ImGui.Dummy(new Vector2(0, 5));
        }

        if (Updater.Instance.State.Contains(7))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("All Voice Files are Up to Date");
        }

        if (Updater.Instance.State.Contains(8))
        {
            ImGui.Dummy(new Vector2(0, 5));
            ImGui.TextWrapped("There is a new update, downloading...");
            if (Updater.Instance.State.Contains(9))
            {
                ImGui.SameLine();
                ImGui.Text(" " + Updater.Instance.DataDownloadCount + " files left");
            }

            ImGui.Dummy(new Vector2(0, 5));
        }

        if (Updater.Instance.State.Contains(9))
        {
            var downloadInfoSnapshot = Updater.Instance.DownloadInfoState.ToList();
            foreach (var item in downloadInfoSnapshot)
                ImGui.ProgressBar(item.percentage, new Vector2(-1, 0), $"{item.file} {item.status}");
        }

        if (Updater.Instance.State.Contains(10)) ImGui.TextWrapped("Done Updating.");
    }

    private void DrawGeneral()
    {
        ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child("ScrollingRegion", new Vector2(360 * ImGuiHelpers.GlobalScale, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (child)
            {
                ImGui.Columns(2, "ScrollingRegionColumns", false);
                ImGui.SetColumnWidth(0, 350 * ImGuiHelpers.GlobalScale);

                // START

                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                ImGui.Indent(60 * ImGuiHelpers.GlobalScale);

                // dalamud has this cached internally, just get it every frame duh
                var logo = Plugin.TextureProvider.GetFromFile(Path.Combine(Plugin.Interface.AssemblyLocation.Directory?.FullName!, "logo.png")).GetWrapOrDefault();
                if (logo == null) return;
                ImGui.Image(logo.ImGuiHandle, new Vector2(200 * ImGuiHelpers.GlobalScale, 200 * ImGuiHelpers.GlobalScale));

                // Working Directory
                ImGui.TextWrapped("Working Directory is " + Plugin.Config.WorkingDirectory);
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));

                // Data
                ImGui.Indent(10 * ImGuiHelpers.GlobalScale);
                ImGui.TextWrapped("NPCs:");
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.0f, 1.0f, 0.0f, 1.0f))) // Green Color
                {
                    ImGui.TextWrapped(XivEngine.Instance.Database.Data["npcs"]);
                }
                ImGui.SameLine();

                ImGui.TextWrapped(" Voices:");
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.0f, 1.0f, 0.0f, 1.0f))) // Green color
                {
                    ImGui.TextWrapped(XivEngine.Instance.Database.Data["voices"]);
                }

                // Update Button
                ImGui.Unindent(70 * ImGuiHelpers.GlobalScale);
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.25f, 0.25f, 0.25f, 1.0f))) // Gray color
                {
                    if (ImGui.Button("Click here to download the latest Voice Files", new Vector2(336 * ImGuiHelpers.GlobalScale, 60 * ImGuiHelpers.GlobalScale)))
                        Updater.Instance.Check();
                }

                // Xiv Voices Enabled
                ImGui.Dummy(new Vector2(0, 15 * ImGuiHelpers.GlobalScale));
                var activeValue = Plugin.Config.Active;
                if (ImGui.Checkbox("##xivVoicesActive", ref activeValue))
                {
                    Plugin.Config.Active = activeValue;
                    Plugin.Config.Save();
                }

              
                ImGui.SameLine();
                ImGui.Text("Xiv Voices Enabled");

                // Reports Enabled
                ImGui.Dummy(new Vector2(0, 8 * ImGuiHelpers.GlobalScale));
                var reports = Plugin.Config.Reports;
                if (ImGui.Checkbox("##reports", ref reports))
                {
                    Plugin.Config.Reports = reports;
                    Plugin.Config.Save();
                };
                ImGui.SameLine();
                ImGui.Text("Report Missing Dialogues Automatically");
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.6f, 0.25f, 0.25f, 1.0f)))
                {
                  ImGui.Text("( English lines only, do not enable for other languages )");
                  ImGui.Text("( Currently lines are only recorded to be missing locally )");
                }

                // AnnounceReports
                var announceReports = Plugin.Config.AnnounceReports;
                if (ImGui.Checkbox("##announceReports", ref announceReports))
                {
                    Plugin.Config.AnnounceReports = announceReports;
                    Plugin.Config.Save();
                };
                ImGui.SameLine();
                ImGui.Text("Announce Reported Lines");
                // END

                ImGui.Columns(1);
            }
        }

        ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
    }

    private void DrawSettings()
    {
        ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child("ScrollingRegion", new Vector2(360 * ImGuiHelpers.GlobalScale, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (child)
            {
                ImGui.Columns(2, "ScrollingRegionColumns", false);
                ImGui.SetColumnWidth(0, 350 * ImGuiHelpers.GlobalScale);

                // START

                // Chat Settings ----------------------------------------------
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Chat Settings");
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));

                // SayEnabled
                var sayEnabled = Plugin.Config.SayEnabled;
                if (ImGui.Checkbox("##sayEnabled", ref sayEnabled))
                {
                    Plugin.Config.SayEnabled = sayEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Say Enabled");

                // TellEnabled
                var tellEnabled = Plugin.Config.TellEnabled;
                if (ImGui.Checkbox("##tellEnabled", ref tellEnabled))
                {
                    Plugin.Config.TellEnabled = tellEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Tell Enabled");

                // ShoutEnabled
                var shoutEnabled = Plugin.Config.ShoutEnabled;
                if (ImGui.Checkbox("##shoutEnabled", ref shoutEnabled))
                {
                    Plugin.Config.ShoutEnabled = shoutEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Shout/Yell Enabled");

                // PartyEnabled
                var partyEnabled = Plugin.Config.PartyEnabled;
                if (ImGui.Checkbox("##partyEnabled", ref partyEnabled))
                {
                    Plugin.Config.PartyEnabled = partyEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Party Enabled");

                // AllianceEnabled
                var allianceEnabled = Plugin.Config.AllianceEnabled;
                if (ImGui.Checkbox("##allianceEnabled", ref allianceEnabled))
                {
                    Plugin.Config.AllianceEnabled = allianceEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Alliance Enabled");

                // FreeCompanyEnabled
                var freeCompanyEnabled = Plugin.Config.FreeCompanyEnabled;
                if (ImGui.Checkbox("##freeCompanyEnabled", ref freeCompanyEnabled))
                {
                    Plugin.Config.FreeCompanyEnabled = freeCompanyEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Free Company Enabled");

                // LinkshellEnabled
                var linkshellEnabled = Plugin.Config.LinkshellEnabled;
                if (ImGui.Checkbox("##linkshellEnabled", ref linkshellEnabled))
                {
                    Plugin.Config.LinkshellEnabled = linkshellEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Linkshell Enabled");

                // BattleDialoguesEnabled
                var battleDialoguesEnabled = Plugin.Config.BattleDialoguesEnabled;
                if (ImGui.Checkbox("##battleDialoguesEnabled", ref battleDialoguesEnabled))
                {
                    Plugin.Config.BattleDialoguesEnabled = battleDialoguesEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Battle Dialogues Enabled");

                // RetainersEnabled
                var retainersEnabled = Plugin.Config.RetainersEnabled;
                if (ImGui.Checkbox("##retainersEnabled", ref retainersEnabled))
                {
                    Plugin.Config.RetainersEnabled = retainersEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Retainers Enabled");

                // Bubble Settings ----------------------------------------------
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Bubble Settings");
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));

                // BubblesEnabled
                var bubblesEnabled = Plugin.Config.BubblesEnabled;
                if (ImGui.Checkbox("##bubblesEnabled", ref bubblesEnabled))
                {
                    Plugin.Config.BubblesEnabled = bubblesEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Chat Bubbles Enabled");

                ImGui.Indent(28 * ImGuiHelpers.GlobalScale);
                var nullcheck = false;
                // BubblesEverywhere
                var bubblesEverywhere = Plugin.Config.BubblesEverywhere;
                if (Plugin.Config.BubblesEnabled)
                {
                    if (ImGui.Checkbox("##bubblesEverywhere", ref bubblesEverywhere))
                        if (bubblesEverywhere)
                        {
                            Plugin.Config.BubblesEverywhere = bubblesEverywhere;
                            Plugin.Config.BubblesInSafeZones = !bubblesEverywhere;
                            Plugin.Config.BubblesInBattleZones = !bubblesEverywhere;
                            Plugin.Config.Save();
                        }
                }
                else
                {
                    ImGui.Checkbox("##null", ref nullcheck);
                }

                ImGui.SameLine();
                ImGui.Text("Enable Bubbles Everywhere");

                // BubblesInSafeZones
                var bubblesOutOfBattlesOnly = Plugin.Config.BubblesInSafeZones;
                if (Plugin.Config.BubblesEnabled)
                {
                    if (ImGui.Checkbox("##bubblesOutOfBattlesOnly", ref bubblesOutOfBattlesOnly))
                        if (bubblesOutOfBattlesOnly)
                        {
                            Plugin.Config.BubblesEverywhere = !bubblesOutOfBattlesOnly;
                            Plugin.Config.BubblesInSafeZones = bubblesOutOfBattlesOnly;
                            Plugin.Config.BubblesInBattleZones = !bubblesOutOfBattlesOnly;
                            Plugin.Config.Save();
                        }
                }
                else
                {
                    ImGui.Checkbox("##null", ref nullcheck);
                }

                ImGui.SameLine();
                ImGui.Text("Only Enable Chat Bubbles In Safe Zones");

                // BubblesInBattleZones
                var bubblesInBattlesOnly = Plugin.Config.BubblesInBattleZones;
                if (Plugin.Config.BubblesEnabled)
                {
                    if (ImGui.Checkbox("##bubblesInBattlesOnly", ref bubblesInBattlesOnly))
                        if (bubblesInBattlesOnly)
                        {
                            Plugin.Config.BubblesEverywhere = !bubblesInBattlesOnly;
                            Plugin.Config.BubblesInSafeZones = !bubblesInBattlesOnly;
                            Plugin.Config.BubblesInBattleZones = bubblesInBattlesOnly;
                            Plugin.Config.Save();
                        }
                }
                else
                {
                    ImGui.Checkbox("##null", ref nullcheck);
                }

                ImGui.SameLine();
                ImGui.Text("Only Enable Chat Bubbles in Battle Zones");

                // BubbleChatEnabled
                var bubbleChatEnabled = Plugin.Config.BubbleChatEnabled;
                if (ImGui.Checkbox("##bubbleChatEnabled", ref bubbleChatEnabled))
                {
                    Plugin.Config.BubbleChatEnabled = bubbleChatEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Print Bubbles in Chat");


                ImGui.Unindent(28 * ImGuiHelpers.GlobalScale);
                
                // Auto Advance Settings ----------------------------------------------

                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Auto-Advance Settings");
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                
                // TextAutoAdvanceEnabled
                var textAutoAdvanceEnabled = Plugin.Config.TextAutoAdvanceEnabled;
                if (ImGui.Checkbox("##TextAutoAdvanceEnabled", ref textAutoAdvanceEnabled))
                {
                    Plugin.Config.TextAutoAdvanceEnabled = textAutoAdvanceEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Enable Text Auto-Advance");
                
                // ExperimentalAutoAdvance
                var experimentalAutoAdvance = Plugin.Config.ExperimentalAutoAdvance;
                if (ImGui.Checkbox("##ExperimentalAutoAdvance", ref experimentalAutoAdvance))
                {
                    Plugin.Config.ExperimentalAutoAdvance = experimentalAutoAdvance;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("[Experimental] Use a more reliable Auto-Advance method");

                // TextAutoHideEnabled
                // var textAutoHideEnabled = Plugin.Config.TextAutoHideEnabled;
                // if (ImGui.Checkbox("##TextAutoHideEnabled", ref textAutoHideEnabled))
                // {
                //     Plugin.Config.TextAutoHideEnabled = textAutoHideEnabled;
                //
                //     if (!textAutoHideEnabled)
                //     {
                //         Plugin.Config.TextAutoHideOnlyInCutscenes = false;
                //     }
                //     
                //     Plugin.Config.Save();
                // };
                // ImGui.SameLine();
                // ImGui.Text("Enable Text Auto-Hide");
                //
                // var textAutoHideOnlyInCutscenes = Plugin.Config.TextAutoHideOnlyInCutscenes;
                // if (textAutoHideEnabled)
                // {
                //     ImGui.Indent(28 * ImGuiHelpers.GlobalScale);
                //     
                //     if (ImGui.Checkbox("##TextAutoHideOnlyInCutscenes", ref textAutoHideOnlyInCutscenes))
                //     {
                //         Plugin.Config.TextAutoHideOnlyInCutscenes = textAutoHideOnlyInCutscenes;
                //         Plugin.Config.Save();
                //     };
                //     ImGui.SameLine();
                //     ImGui.Text("Only Auto-Hide in Cutscenes");
                //     
                //     ImGui.Unindent(28 * ImGuiHelpers.GlobalScale);
                // }

                // 

                // Other Settings ----------------------------------------------

                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Other Settings");
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));

                // ReplaceVoicedARRCutscenes
                var replaceVoicedARRCutscenes = Plugin.Config.ReplaceVoicedARRCutscenes;
                if (ImGui.Checkbox("##replaceVoicedARRCutscenes", ref replaceVoicedARRCutscenes))
                {
                    Plugin.Config.ReplaceVoicedARRCutscenes = replaceVoicedARRCutscenes;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Replace ARR Cutscenes");

                // SkipEnabled
                var skipEnabled = Plugin.Config.SkipEnabled;
                if (ImGui.Checkbox("##interruptEnabled", ref skipEnabled))
                {
                    Plugin.Config.SkipEnabled = skipEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Dialogue Skip Enabled");
                // END

                ImGui.Columns(1);
            }
        }

        ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
    }

    private void AudioSettings()
    {
        ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child("ScrollingRegion", new Vector2(360 * ImGuiHelpers.GlobalScale, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (child)
            {
                ImGui.Columns(2, "ScrollingRegionColumns", false);
                ImGui.SetColumnWidth(0, 350 * ImGuiHelpers.GlobalScale);

                // START

                // Mute Button -----------------------------------------------

                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                var mute = Plugin.Config.Mute;
                if (ImGui.Checkbox("##mute", ref mute))
                {
                    Plugin.Config.Mute = mute;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Mute Enabled");

                // Lipsync Enabled -----------------------------------------------
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                var lipsyncEnabled = Plugin.Config.LipsyncEnabled;
                if (ImGui.Checkbox("##lipsyncEnabled", ref lipsyncEnabled))
                {
                    Plugin.Config.LipsyncEnabled = lipsyncEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Lipsync Enabled");

                // Volume Slider ---------------------------------------------

                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Volume Control");
                var volume = Plugin.Config.Volume;
                if (ImGui.SliderInt("##volumeSlider", ref volume, 0, 100, volume.ToString()))
                {
                    Plugin.Config.Volume = volume;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Volume");

                // Speed Slider ---------------------------------------------

                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Speed Control");
                var speed = Plugin.Config.Speed;
                if (ImGui.SliderInt("##speedSlider", ref speed, 75, 200, speed.ToString()))
                {
                    Plugin.Config.Speed = speed;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Speed");

                // Playback Engine  ---------------------------------------------
                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Playback Engine");
                var audioEngines = new[] { "DirectSound", "Wasapi", "WaveOut" };
                var currentEngine = Plugin.Config.AudioEngine - 1;

                using (var combo = ImRaii.Combo("##audioEngine", audioEngines[currentEngine]))
                {
                    if (combo)
                    {
                        for (int i = 0; i < audioEngines.Length; i++)
                        {
                            if (ImGui.Selectable(audioEngines[i]))
                            {
                                Plugin.Config.AudioEngine = i + 1;
                                Plugin.Config.Save();
                            }
                        }
                    }
                }

                ImGui.SameLine();
                ImGui.Text("Engine");


                // Speed Slider ---------------------------------------------

                ImGui.Dummy(new Vector2(0, 30 * ImGuiHelpers.GlobalScale));
                ImGui.Unindent(15 * ImGuiHelpers.GlobalScale);
                ImGui.Separator();
                ImGui.Indent(15 * ImGuiHelpers.GlobalScale);

                // Local AI Settings Settings ----------------------------------------------

                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                var localTTSEnabled = Plugin.Config.LocalTTSEnabled;
                if (ImGui.Checkbox("##localTTSEnabled", ref localTTSEnabled))
                {
                    Plugin.Config.LocalTTSEnabled = localTTSEnabled;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Local TTS Enabled");

                ImGui.Indent(20 * ImGuiHelpers.GlobalScale);
                ImGui.Dummy(new Vector2(0, 5));
                ImGui.Text("Local TTS Ungendered Voice:");
                ImGui.SameLine();
                var localTTSUngendered = Plugin.Config.LocalTTSUngendered;
                string[] genders = { "Male", "Female" };
                ImGui.SetNextItemWidth(129 * ImGuiHelpers.GlobalScale);

                using (var combo = ImRaii.Combo("##localTTSUngendered", genders[localTTSUngendered]))
                {
                    if (combo)
                    {
                        for (int i = 0; i < genders.Length; i++)
                        {
                            if (ImGui.Selectable(genders[i]))
                            {
                                Plugin.Config.LocalTTSUngendered = i;
                                Plugin.Config.Save();
                            }
                        }
                    }
                }

                // LocalTTS Volume Slider
                ImGui.Dummy(new Vector2(0, 5 * ImGuiHelpers.GlobalScale));
                ImGui.Text("Volume:");
                ImGui.SameLine();
                var localTTSVolume = Plugin.Config.LocalTTSVolume;
                if (ImGui.SliderInt("##localTTSVolumeSlider", ref localTTSVolume, 0, 100, localTTSVolume.ToString()))
                {
                    Plugin.Config.LocalTTSVolume = localTTSVolume;
                    Plugin.Config.Save();
                }

                // LocalTTS Speed Slider
                ImGui.Dummy(new Vector2(0, 5 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("Speed:");
                ImGui.SameLine();
                var localTTSSpeed = Plugin.Config.LocalTTSSpeed;
                if (ImGui.SliderInt("##localTTSSpeedSlider", ref localTTSSpeed, 75, 200, localTTSSpeed.ToString()))
                {
                    Plugin.Config.LocalTTSSpeed = localTTSSpeed;
                    Plugin.Config.Save();
                }

                ImGui.Dummy(new Vector2(0, 5 * ImGuiHelpers.GlobalScale));
                var localTTSPlayerSays = Plugin.Config.LocalTTSPlayerSays;
                if (ImGui.Checkbox("##localTTSPlayerSays", ref localTTSPlayerSays))
                {
                    Plugin.Config.LocalTTSPlayerSays = localTTSPlayerSays;
                    Plugin.Config.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Say Speaker Name in Chat");


                ImGui.Dummy(new Vector2(0, 5 * ImGuiHelpers.GlobalScale));
                var ignoreNarratorLines = Plugin.Config.IgnoreNarratorLines;
                if (ImGui.Checkbox("##ignoreNarratorLines", ref ignoreNarratorLines))
                {
                    Plugin.Config.IgnoreNarratorLines = ignoreNarratorLines;
                    Plugin.Config.Save();
                }

              
                ImGui.SameLine();
                ImGui.Text("Ignore Narrator Lines");
                ImGui.Unindent(20 * ImGuiHelpers.GlobalScale);


                // END

                ImGui.Columns(1);
            }
        }

        ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
    }

    private void LogsSettings()
    {
        if (!Plugin.Config.Active)
        {
            ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
            ImGui.TextWrapped("Xiv Voices is Disabled");
            ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
        }
        else
        {
            ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
            // Begin a scrollable region
            using (var child = ImRaii.Child("ScrollingRegion", new Vector2(360 * ImGuiHelpers.GlobalScale, -1), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                if (child)
                {
                    ImGui.Columns(2, "ScrollingRegionColumns", false);
                    ImGui.SetColumnWidth(0, 350 * ImGuiHelpers.GlobalScale);

                    var audioInfoStateCopy = PluginReference.audio.AudioInfoState.ToList();
                    foreach (var item in audioInfoStateCopy)
                    {
                        // Show Dialogue Details (Name: Sentence)
                        ImGui.TextWrapped($"{item.data.Speaker}: {item.data.TtsData.Message}");

                        // Show Player Progress Bar
                        var progressSize = 253;
                        var plotHistogramColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
                        bool shouldPushColor = false;
                        if (item.type == "xivv")
                        {
                            plotHistogramColor = new Vector4(0.0f, 0.7f, 0.0f, 1.0f); // RGBA: Full green
                            shouldPushColor = true;
                        }
                        else if (item.type == "empty")
                        {
                            plotHistogramColor = new Vector4(0.2f, 0.2f, 0.2f, 1.0f); // RGBA: Full green
                            shouldPushColor = true;
                            progressSize = 310;
                        }

                        if (XivEngine.Instance.Database.Access) progressSize -= 100;

                        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, plotHistogramColor, shouldPushColor))
                        {
                          ImGui.ProgressBar(item.percentage, new Vector2(progressSize * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale), $"{item.state}");
                        }

                        if (item.type != "empty")
                        {
                            // Show Play and Stop Buttons
                            ImGui.SameLine();
                            if (item.state == "playing")
                            {
                                if (ImGui.Button("Stop", new Vector2(50 * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale)))
                                    PluginReference.audio.StopAudio();
                            }
                            else
                            {
                                if (ImGui.Button($"Play##{item.id}", new Vector2(50 * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale)))
                                {
                                    PluginReference.audio.StopAudio();
                                    item.data.Network = "Local";
                                    PluginReference.xivEngine.AddToQueue(item.data);
                                }
                            }
                        }

                        ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                    }

                    ImGui.Columns(1);
                }
            }

            ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
        }
    }

    private void WineSettings()
    {
        if (!Dalamud.Utility.Util.IsWine())
        {
            ImGui.TextUnformatted("You are not using wine.");
            return;
        }

        ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child("ScrollingRegion", new Vector2(360 * ImGuiHelpers.GlobalScale, -1), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (child)
            {
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
                ImGui.TextWrapped("FFmpeg Settings");
                ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));

                var wineUseNativeFFmpeg = Plugin.Config.WineUseNativeFFmpeg;
                if (ImGui.Checkbox("##wineUseNativeFFmpeg", ref wineUseNativeFFmpeg))
                {
                    Plugin.Config.WineUseNativeFFmpeg = wineUseNativeFFmpeg;
                    if (wineUseNativeFFmpeg)
                    {
                      Plugin.FFmpegger.StartFFmpegWineProcess();
                    }
                    else
                    {
                      Plugin.FFmpegger.StopFFmpegWineProcess();
                    }
                    Plugin.Config.Save();
                }
                ImGui.SameLine();
                ImGui.Text("Use native FFmpeg");
                ImGui.Indent(16 * ImGuiHelpers.GlobalScale);
                ImGui.Bullet();
                ImGui.TextWrapped("Increases speed and prevents lag spikes on voices with effects (e.g. Dragons) and when using a playback speed other than 100.");
                ImGui.Unindent(16 * ImGuiHelpers.GlobalScale);
                
                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                if (wineUseNativeFFmpeg)
                {
                    using (var child2 = ImRaii.Child("##wineFFmpegState", new Vector2(345 * ImGuiHelpers.GlobalScale, 60 * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar))
                    {
                        if (child2)
                        {
                            ImGui.TextWrapped($"FFmpeg daemon state: {(Plugin.FFmpegger.isFFmpegWineProcessRunning ? "Running" : "Stopped")}");
                            if (ImGui.Button("Start"))
                            {
                                Plugin.FFmpegger.StartFFmpegWineProcess();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Stop"))
                            {
                                Plugin.FFmpegger.StopFFmpegWineProcess();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Refresh"))
                            {
                                Plugin.FFmpegger.RefreshFFmpegWineProcessState();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Copy Start Command"))
                            {
                                ImGui.SetClipboardText($"/usr/bin/env bash -c '/usr/bin/env nohup /usr/bin/env bash \"{Plugin.FFmpegger.FFmpegWineScriptPath}\" {Plugin.FFmpegger.FFmpegWineProcessPort} >/dev/null 2>&1' &");
                            }

                            if (ImGui.IsItemHovered())
                                using (ImRaii.Tooltip())
                                    ImGui.TextUnformatted("Copies the command to start the ffmpeg-wine daemon manually. Run this in your native linux/macos console.");
                        }
                    }

                    if (!Plugin.FFmpegger.isFFmpegWineProcessRunning)
                    {
                        if (Plugin.FFmpegger.IsWineDirty)
                        {
                          ImGui.TextWrapped("Warning: ffmpeg-wine might require wine to fully restart for registry changes to take effect.");
                          ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                        }
                        using (var child2 = ImRaii.Child("##wineFFmpegTroubleshooting", new Vector2(345 * ImGuiHelpers.GlobalScale, 285 * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar))
                        {
                            if (child2)
                            {
                                ImGui.TextWrapped("If the FFmpeg daemon fails to start, check the following:");
                                ImGui.Indent(4 * ImGuiHelpers.GlobalScale);
                                ImGui.Bullet();
                                ImGui.TextWrapped("'/usr/bin/env' exists");
                                ImGui.Bullet();
                                ImGui.TextWrapped("bash is installed system-wide as 'bash'");
                                ImGui.Bullet();
                                if (Plugin.FFmpegger.IsMac())
                                {
                                    ImGui.TextWrapped("netstat is installed system-wide as 'netstat'");
                                }
                                else
                                {
                                    ImGui.TextWrapped("ss is installed system-wide as 'ss'");
                                }
                                ImGui.Bullet();
                                ImGui.TextWrapped("ffmpeg is installed system-wide as 'ffmpeg'");
                                ImGui.Bullet();
                                ImGui.TextWrapped("pgrep is installed system-wide as 'pgrep'");
                                ImGui.Bullet();
                                ImGui.TextWrapped("grep is installed system-wide as 'grep'");
                                ImGui.Bullet();
                                ImGui.TextWrapped("ncat is installed system-wide as 'ncat'");
                                ImGui.Indent(16 * ImGuiHelpers.GlobalScale);
                                ImGui.Bullet();
                                ImGui.TextWrapped("Not 'netcat' nor 'nc'. ncat is usually part of the 'nmap' package");
                                ImGui.Unindent(16 * ImGuiHelpers.GlobalScale);
                                ImGui.Bullet();
                                ImGui.TextWrapped("wc is installed system-wide as 'wc'");
                                ImGui.Bullet();
                                ImGui.TextWrapped($"port {Plugin.FFmpegger.FFmpegWineProcessPort} is not in use");
                                ImGui.Unindent(4 * ImGuiHelpers.GlobalScale);
                            }
                        }
                    }
                }
            }
        }
        ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
    }

    private void Changelog()
    {
        ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child("ChangelogScrollingRegion", new Vector2(360 * ImGuiHelpers.GlobalScale, 592 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            if (child)
            {
                ImGui.Columns(2, "ChangelogColumns", false);
                ImGui.SetColumnWidth(0, 350 * ImGuiHelpers.GlobalScale);

                if (ImGui.CollapsingHeader("Version 0.3.4.1", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Fix TTS");
                }
                
                if (ImGui.CollapsingHeader("Version 0.3.4.0", ImGuiTreeNodeFlags.None))
                {
                  ImGui.Bullet();
                  ImGui.TextWrapped("Updated for 7.2/API12/NET9");
                  ImGui.Bullet();
                  ImGui.TextWrapped("FFmpeg now runs natively on Linux and MacOS");
                  ImGui.Bullet();
                  ImGui.TextWrapped("LocalTTS is now affected by 'Speed Control'");
                  ImGui.Bullet();
                  ImGui.TextWrapped("Voicelines now play on repeated NPC interactions again, unless 'Hide Talk Addon' is enabled.");
                }

                if (ImGui.CollapsingHeader("Version 0.3.3.1", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("More settings window refactoring - might've fixed the settings crash");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Added native ffmpeg support when using WINE. See '/xivv wine'. (Linux Only)");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Added '/xivv [settings|dialogue|audio|logs|wine]' to open those settings tabs directly");
                }

            if (ImGui.CollapsingHeader("Version 0.3.3.0", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Use Dalamud SDK instead of DalamudPackager");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Use Dalamud for loading/saving the configuration");
                    ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
                    ImGui.Bullet();
                    ImGui.TextWrapped("Any prior configs will be automatically migrated from their old location to APPDATA\\XIVLauncher\\pluginConfigs\\XivVoices.json");
                    ImGui.Unindent(8 * ImGuiHelpers.GlobalScale);
                    ImGui.Bullet();
                    ImGui.TextWrapped("Refactor the configuration window to hopefully improve stability");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Print mute/unmute command results to chat");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Allows auto-advance to function in duty cutscenes again");
                }
                
                if (ImGui.CollapsingHeader("Version 0.3.2.3", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Fix issue where 'ShB' in chat would be read as 'Shadow Bangers' lol.");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Fix issue where auto advance would trigger for unreported dialog while Mute is enabled.");
                }
                
                if (ImGui.CollapsingHeader("Version 0.3.2.2", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Disable text auto-hide feature due to problems with MSQ Roulette.");
                }

                if (ImGui.CollapsingHeader("Version 0.3.2.1", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Add text auto-hide feature.");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Fix issue where auto-advance would select a target in the open world.");
                }
                
                if (ImGui.CollapsingHeader("Version 0.3.2.0", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Add auto-advance feature.");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Add local reporting for future voice line creation.");
                }

                if (ImGui.CollapsingHeader("Version 0.3.1.3", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Change how the config saves in an attempt to prevent crashing.");
                }

                if (ImGui.CollapsingHeader("Version 0.3.1.2", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Disable option to save config with indentation to prevent crashing.");
                }

                if (ImGui.CollapsingHeader("Version 0.3.1.1", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("Add button to new Discord server in the sidebar.");
                }

                if (ImGui.CollapsingHeader("Version 0.3.1.0", ImGuiTreeNodeFlags.None))
                {
                    ImGui.Bullet();
                    ImGui.TextWrapped("XivVoices is back in development.");
                    ImGui.Bullet();
                    ImGui.TextWrapped("Added support for FFXIV 7.1.");
                }

                ImGui.Columns(1);
            }
        }

        ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
    }
}
