using System;
using System.IO;
using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace XivVoices;

[Serializable]
public class Configuration : IPluginConfiguration
{
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
    public bool TextAutoHideEnabled { get; set; } = false;
    public bool TextAutoHideOnlyInCutscenes { get; set; } = false;
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

    public void Initialize()
    {
      // Disabled until bug is fixed.
      TextAutoHideEnabled = false;
      TextAutoHideOnlyInCutscenes = false;
    }

    public void Save() =>
      Plugin.Interface.SavePluginConfig(this);
}
