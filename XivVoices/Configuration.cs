using System;
using System.IO;
using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace XivVoices;

[Serializable]
public class Configuration : IPluginConfiguration
{
    private const string ConfigPath = "C:/XIV_Voices/Tools/config.json";

    // the below exist just to make saving less cumbersome
    [NonSerialized] private IDalamudPluginInterface? PluginInterface;

    public bool Active { get; set; } = true;
    public bool Initialized { get; set; }
    public string WorkingDirectory { get; set; } = "C:/XIV_Voices";
    public bool Reports { get; set; }
    public bool AnnounceReports { get; set; } = true;
    public bool OnlineRequests { get; set; }
    public bool ReplaceVoicedARRCutscenes { get; set; } = true;
    public bool LipsyncEnabled { get; set; } = true;
    public bool SkipEnabled { get; set; } = true;
    public bool TextAutoAdvanceEnabled { get; set; }

    // Chat Settings
    public bool SayEnabled { get; set; } = true;
    public bool TellEnabled { get; set; } = true;
    public bool ShoutEnabled { get; set; } = true;
    public bool PartyEnabled { get; set; } = true;
    public bool AllianceEnabled { get; set; } = true;
    public bool FreeCompanyEnabled { get; set; } = true;
    public bool LinkshellEnabled { get; set; } = true;
    public bool BattleDialoguesEnabled { get; set; } = true;
    public bool RetainersEnabled { get; set; } = true;
    public bool BubblesEnabled { get; set; } = true;
    public bool BubblesEverywhere { get; set; } = true;
    public bool BubblesInSafeZones { get; set; }
    public bool BubblesInBattleZones { get; set; }
    public bool BubbleChatEnabled { get; set; } = true;

    // Engine Settings
    public bool Mute { get; set; }
    public int Volume { get; set; } = 100;
    public int Speed { get; set; } = 100;
    public int AudioEngine { get; set; } = 1;
    public bool PollyEnabled { get; set; }
    public bool LocalTTSEnabled { get; set; }
    public string LocalTTSMale { get; set; } = "en-gb-northern_english_male-medium";
    public string LocalTTSFemale { get; set; } = "en-gb-jenny_dioco-medium";
    public int LocalTTSUngendered { get; set; } = 1;
    public int LocalTTSVolume { get; set; } = 100;
    public bool LocalTTSPlayerSays { get; set; }
    public bool IgnoreNarratorLines { get; set; }

    // Framework Settings
    public bool FrameworkActive { get; set; }
    public bool FrameworkOnline { get; set; }
    public int Version { get; set; }

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;

            // Read and deserialize the JSON file
            var jsonString = File.ReadAllText(ConfigPath);
            var loadedConfig = JsonSerializer.Deserialize<Configuration>(jsonString);

            if (loadedConfig == null) return;

            // Update the current instance with loaded data
            Active = loadedConfig.Active;
            Initialized = loadedConfig.Initialized;
            WorkingDirectory = loadedConfig.WorkingDirectory;
            Reports = loadedConfig.Reports;
            AnnounceReports = loadedConfig.AnnounceReports;
            OnlineRequests = loadedConfig.OnlineRequests;
            ReplaceVoicedARRCutscenes = loadedConfig.ReplaceVoicedARRCutscenes;
            LipsyncEnabled = loadedConfig.LipsyncEnabled;
            SkipEnabled = loadedConfig.SkipEnabled;
            TextAutoAdvanceEnabled = loadedConfig.TextAutoAdvanceEnabled;
            SayEnabled = loadedConfig.SayEnabled;
            TellEnabled = loadedConfig.TellEnabled;
            ShoutEnabled = loadedConfig.ShoutEnabled;
            PartyEnabled = loadedConfig.PartyEnabled;
            AllianceEnabled = loadedConfig.AllianceEnabled;
            FreeCompanyEnabled = loadedConfig.FreeCompanyEnabled;
            LinkshellEnabled = loadedConfig.LinkshellEnabled;
            BattleDialoguesEnabled = loadedConfig.BattleDialoguesEnabled;
            RetainersEnabled = loadedConfig.RetainersEnabled;
            BubblesEnabled = loadedConfig.BubblesEnabled;
            BubblesEverywhere = loadedConfig.BubblesEverywhere;
            BubblesInSafeZones = loadedConfig.BubblesInSafeZones;
            BubblesInBattleZones = loadedConfig.BubblesInBattleZones;
            BubbleChatEnabled = loadedConfig.BubbleChatEnabled;
            Mute = loadedConfig.Mute;
            Volume = loadedConfig.Volume;
            Speed = loadedConfig.Speed;
            AudioEngine = loadedConfig.AudioEngine;
            PollyEnabled = loadedConfig.PollyEnabled;
            LocalTTSEnabled = loadedConfig.LocalTTSEnabled;
            LocalTTSMale = loadedConfig.LocalTTSMale;
            LocalTTSFemale = loadedConfig.LocalTTSFemale;
            LocalTTSUngendered = loadedConfig.LocalTTSUngendered;
            LocalTTSVolume = loadedConfig.LocalTTSVolume;
            LocalTTSPlayerSays = loadedConfig.LocalTTSPlayerSays;
            IgnoreNarratorLines = loadedConfig.IgnoreNarratorLines;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            // Ensure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));

            // Serialize the current configuration to JSON
            var jsonString = JsonSerializer.Serialize(this);
            File.WriteAllText(ConfigPath, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }
}