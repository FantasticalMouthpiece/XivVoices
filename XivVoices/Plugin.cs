#region Usings

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using Lumina.Excel.Sheets;
using XivVoices.Attributes;
using XivVoices.Engine;
using XivVoices.Services;
using XivVoices.Voice;
//using FFXIVClientStructs.FFXIV.Client.Game.Housing;
using ICharacter = Dalamud.Game.ClientState.Objects.Types.ICharacter;

#endregion

namespace XivVoices;

public class Plugin : IDalamudPlugin
{
    #region Fields

    private const string KeyboardStateSignature = "48 8D 0C 85 ?? ?? ?? ?? 8B 04 31 85 C2 0F 85";
    private const string KeyboardStateIndexArray = "0F B6 94 33 ?? ?? ?? ?? 84 D2";

    private readonly IObjectTable _objectTable;
    private IToastGui _toast;
    private IGameConfig _gameConfig;
    private readonly IFramework _framework;
    private bool texturesLoaded;

    private readonly PluginCommandManager<Plugin> commandManager;

    //public XIVVWebSocketServer webSocketServer;
    private readonly WindowSystem windowSystem;
    private PluginWindow _window { get; }
    private Filter _filter;

    private bool disposed;

    private Stopwatch _messageTimer = new();
    private readonly Dictionary<string, string> _scdReplacements = new();
    private ConcurrentDictionary<string, List<KeyValuePair<string, bool>>> _papSorting = new();
    private ConcurrentDictionary<string, List<KeyValuePair<string, bool>>> _mdlSorting = new();

    private ConcurrentDictionary<string, string> _animationMods = new();
    private Dictionary<string, List<string>> _modelMods = new();

    private bool _hasBeenInitialized;
    private uint LockCode = 0x6D617265;
    private AddonTalkManager _addonTalkManager;
    private readonly ICondition _condition;
    private readonly IGameGui _gameGui;
    private int _recentCFPop;
    private unsafe Camera* _camera;

    public ISharedImmediateTexture Logo;
    public ISharedImmediateTexture Icon;
    public ISharedImmediateTexture GeneralSettings;
    public ISharedImmediateTexture GeneralSettingsActive;
    public ISharedImmediateTexture DialogueSettings;
    public ISharedImmediateTexture DialogueSettingsActive;
    public ISharedImmediateTexture AudioSettings;
    public ISharedImmediateTexture AudioSettingsActive;
    public ISharedImmediateTexture Archive;
    public ISharedImmediateTexture ArchiveActive;
    public ISharedImmediateTexture Discord;
    public ISharedImmediateTexture KoFi;
    public ISharedImmediateTexture Changelog;
    public ISharedImmediateTexture ChangelogActive;
    public string Name => "XivVoices Plugin";

    public ISigScanner SigScanner { get; set; }

    internal Filter Filter
    {
        get
        {
            if (_filter == null)
            {
                _filter = new Filter(this);
                _filter.Enable();
            }

            return _filter;
        }
        set => _filter = value;
    }

    public IGameInteropProvider InteropProvider { get; set; }

    public Configuration Config { get; }

    public PluginWindow Window => _window;

    public MediaCameraObject PlayerCamera { get; private set; }

    public IDalamudPluginInterface Interface { get; }

    public IChatGui Chat { get; }

    public IClientState ClientState { get; }

    public ITextureProvider TextureProvider { get; }

    public AddonTalkHandler AddonTalkHandler { get; set; }

    public static IPluginLog PluginLog { get; set; }

    public Updater updater;
    public Database database;
    public Audio audio;
    public XivEngine xivEngine;

    #endregion

    #region Plugin Initiialization

    public Plugin(
        IDalamudPluginInterface pi,
        ICommandManager commands,
        IChatGui chat,
        IClientState clientState,
        ISigScanner scanner,
        IObjectTable objectTable,
        IToastGui toast,
        IDataManager dataManager,
        IGameConfig gameConfig,
        IFramework framework,
        IGameInteropProvider interopProvider,
        ICondition condition,
        IGameGui gameGui,
        IDragDropManager dragDrop,
        IPluginLog pluginLog,
        ITextureProvider textureProvider)
    {
        PluginLog = pluginLog;
        Chat = chat;

        #region Constructor

        try
        {
            Service.DataManager = dataManager;
            Service.SigScanner = scanner;
            Service.GameInteropProvider = interopProvider;
            Service.ChatGui = chat;
            Service.ClientState = clientState;
            Service.ObjectTable = objectTable;
            Interface = pi;
            ClientState = clientState;
            // Get or create a configuration object
            Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(Interface);
            //webSocketServer = new XIVVWebSocketServer(this.config, this);
            // Initialize the UI
            windowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);
            _window = Interface.Create<PluginWindow>();
            Interface.UiBuilder.DisableAutomaticUiHide = true;
            Interface.UiBuilder.DisableGposeUiHide = true;
            _window.ClientState = ClientState;
            _window.Configuration = Config;
            _window.PluginInterface = Interface;
            _window.PluginReference = this;
            if (_window is not null) windowSystem.AddWindow(_window);
            Interface.UiBuilder.Draw += UiBuilder_Draw;
            Interface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;

            // Load all of our commands
            commandManager = new PluginCommandManager<Plugin>(this, commands);
            _toast = toast;
            _gameConfig = gameConfig;
            SigScanner = scanner;
            TextureProvider = textureProvider;
            InteropProvider = interopProvider;
            _objectTable = objectTable;
            _framework = framework;
            _framework.Update += framework_Update;
            _condition = condition;
            _gameGui = gameGui;
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, e.Message);
            PrintError("[XivVoices] Fatal Error, the plugin did not initialize correctly!\n" + e.Message);
        }

        #endregion
    }

    private async void InitializeEverything()
    {
        try
        {
            InitializeAddonTalk();
            InitializeCamera();
            Chat.ChatMessage += Chat_ChatMessage;
            ClientState.Login += _clientState_Login;
            ClientState.Logout += _clientState_Logout;
            ClientState.TerritoryChanged += _clientState_TerritoryChanged;
            Filter = new Filter(this);
            Filter.Enable();
            Filter.OnSoundIntercepted += _filter_OnSoundIntercepted;
            if (ClientState.IsLoggedIn) Print("XivVoices is live.");
            database = new Database(Interface, this);
            updater = new Updater();
            audio = new Audio(this);
            xivEngine = new XivEngine(database, audio, updater);
        }
        catch (Exception e)
        {
            PluginLog.Warning("InitializeEverything: " + e, e.Message);
            PrintError("[XivVoicesInitializer] Fatal Error, the plugin did not initialize correctly!\n" + e.Message);
        }
    }


    public unsafe void InitializeCamera()
    {
        try
        {
            PluginLog.Information("Initializing Camera");
            _camera = CameraManager.Instance()->GetActiveCamera();
            PlayerCamera = new MediaCameraObject(_camera);
        }
        catch (Exception e)
        {
            PluginLog.Warning("InitializeCamera: " + e, e.Message);
            PrintError("[XivVoices] Fatal Error, the Camera did not initialize correctly!\n" + e.Message);
        }
    }

    public void InitializeAddonTalk()
    {
        try
        {
            PluginLog.Information("Initializing AddonTalk");
            _addonTalkManager = new AddonTalkManager(_framework, ClientState, _condition, _gameGui);
            AddonTalkHandler = new AddonTalkHandler(_addonTalkManager, _framework, _objectTable, ClientState, this,
                Chat, SigScanner);
        }
        catch (Exception e)
        {
            PluginLog.Warning("AddonTalk: " + e, e.Message);
            PrintError("[XivVoices] Fatal Error, AddonTalk did not initialize correctly!\n" + e.Message);
        }
    }

    private void _clientState_CfPop(ContentFinderCondition obj)
    {
        _recentCFPop = 1;
    }

    #endregion Plugin Initiialization

    #region Debugging

    public void Print(string text)
    {
        Chat.Print(new XivChatEntry
        {
            Message = text,
            Type = XivChatType.CustomEmote
        });
    }

    public void PrintError(string text)
    {
        Chat.Print(new XivChatEntry
        {
            Message = text,
            Type = XivChatType.Urgent
        });
    }

    public void Log(string text)
    {
        if (Config.FrameworkActive)
            Chat.Print(new XivChatEntry
            {
                Message = text,
                Type = XivChatType.CustomEmote
            });
    }

    public void LogError(string text)
    {
        if (Config.FrameworkActive)
            Chat.Print(new XivChatEntry
            {
                Message = text,
                Type = XivChatType.Urgent
            });
    }

    #endregion

    #region Sound Management

    private void framework_Update(IFramework framework)
    {
        if (!disposed)
            if (!_hasBeenInitialized && ClientState.LocalPlayer != null)
            {
                InitializeEverything();
                _hasBeenInitialized = true;
            }
    }


    private void Chat_ChatMessage(XivChatType type, int senderId, ref SeString sender, ref SeString message,
        ref bool isHandled)
    {
        if (!disposed)
        {
            if (!Config.Active || !Config.Initialized) return;

            var playerName = "";
            try
            {
                foreach (var item in sender.Payloads)
                {
                    var player = item as PlayerPayload;
                    var text = item as TextPayload;
                    if (player != null)
                    {
                        playerName = player.PlayerName;
                        break;
                    }

                    if (text != null)
                    {
                        playerName = text.Text;
                        break;
                    }
                }
            }
            catch
            {
            }

            if (type == XivChatType.NPCDialogue)
            {
                //string correctedMessage = _addonTalkHandler.StripPlayerNameFromNPCDialogue(_addonTalkHandler.ConvertRomanNumberals(message.TextValue.TrimStart('.')));
                //webSocketServer.BroadcastMessage($"=====> lastNPCDialogue [{_addonTalkHandler.lastNPCDialogue}]\n=========> current [{playerName + cleanedMessage}]");
                // Check for Cancel
                var cleanedMessage = AddonTalkHandler.CleanSentence(message.TextValue);
                if (AddonTalkHandler.lastNPCDialogue == playerName + cleanedMessage)
                    if (Config.SkipEnabled)
                        ChatText(playerName, cleanedMessage, type, senderId, true);
                return;
            }

            if (type == XivChatType.NPCDialogueAnnouncements)
            {
                HandleNPCDialogueAnnouncements(playerName, type, senderId, message.TextValue);
                return;
            }

            switch (type)
            {
                case XivChatType.Say:
                    if (Config.SayEnabled) ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case XivChatType.TellIncoming:
                    if (Config.TellEnabled)
                        ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case XivChatType.TellOutgoing:
                    break;
                case XivChatType.Shout:
                case XivChatType.Yell:
                    if (Config.ShoutEnabled)
                        ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case XivChatType.CustomEmote:
                    break;
                case XivChatType.Party:
                case XivChatType.CrossParty:
                    if (Config.PartyEnabled)
                        ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case XivChatType.Alliance:
                    if (Config.AllianceEnabled)
                        ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case XivChatType.FreeCompany:
                    if (Config.FreeCompanyEnabled)
                        ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case XivChatType.NPCDialogue:
                    break;
                case XivChatType.NPCDialogueAnnouncements:
                    break;
                case XivChatType.CrossLinkShell1:
                case XivChatType.CrossLinkShell2:
                case XivChatType.CrossLinkShell3:
                case XivChatType.CrossLinkShell4:
                case XivChatType.CrossLinkShell5:
                case XivChatType.CrossLinkShell6:
                case XivChatType.CrossLinkShell7:
                case XivChatType.CrossLinkShell8:
                case XivChatType.Ls1:
                case XivChatType.Ls2:
                case XivChatType.Ls3:
                case XivChatType.Ls4:
                case XivChatType.Ls5:
                case XivChatType.Ls6:
                case XivChatType.Ls7:
                case XivChatType.Ls8:
                    if (Config.LinkshellEnabled)
                        ChatText(playerName, message.TextValue, type, senderId);
                    break;
                case (XivChatType)2729:
                case (XivChatType)2091:
                case (XivChatType)2234:
                case (XivChatType)2730:
                case (XivChatType)2219:
                case (XivChatType)2859:
                case (XivChatType)2731:
                case (XivChatType)2106:
                case (XivChatType)10409:
                case (XivChatType)8235:
                case (XivChatType)9001:
                case (XivChatType)4139:
                    break;
            }
        }
    }

    private async void HandleNPCDialogueAnnouncements(string playerName, XivChatType type, int senderId, string message)
    {
        if (!Config.Active || !Config.Initialized) return;

        await Task.Delay(250);
        var cleanedMessage = AddonTalkHandler.CleanSentence(message);

        if (AddonTalkHandler.lastBubbleDialogue == cleanedMessage)
            //webSocketServer.SendMessage($"NPCDialogueAnnouncement blocked: {cleanedMessage}");
            return;

        if (Config.BattleDialoguesEnabled)
        {
            ChatText(playerName, cleanedMessage, type, senderId);
            AddonTalkHandler.lastBattleDialogue = cleanedMessage;
        }
    }

    private void ChatText(string sender, SeString message, XivChatType type, int senderId, bool cancel = false)
    {
        try
        {
            if (!Config.Active || !Config.Initialized) return;

            if (sender.Length == 1)
                sender = ClientState.LocalPlayer.Name.TextValue;

            var stringtype = type.ToString();
            var correctSender = AddonTalkHandler.CleanSender(sender);
            var user = $"{ClientState.LocalPlayer.Name}@{ClientState.LocalPlayer.HomeWorld.Value.Name}";

            if (cancel)
            {
                stringtype = "Cancel";
                XivEngine.Instance.Process(stringtype, correctSender, "-1", "-1", message.ToString(), "-1", "-1", "-1",
                    "-1", "-1", ClientState.ClientLanguage.ToString(), new Vector3(-99), null, user);
                return;
            }

            // Default Parameters
            ICharacter character = null;
            var id = "-1";
            var skeleton = "-1";
            var body = "-1";
            var gender = "default";
            var race = "-1";
            var tribe = "-1";
            var eyes = "-1";

            // Get Character Data
            if (sender.Contains(ClientState.LocalPlayer.Name.TextValue))
                character = ClientState.LocalPlayer;
            else
                character = AddonTalkHandler.GetCharacterFromName(sender);

            // Fill the Parameters
            if (character == null)
            {
                if (database.PlayerData != null && database.PlayerData.ContainsKey(sender))
                {
                    body = database.PlayerData[sender].Body;
                    gender = database.PlayerData[sender].Gender;
                    race = database.PlayerData[sender].Race;
                    tribe = database.PlayerData[sender].Tribe;
                    eyes = database.PlayerData[sender].EyeShape;
                }
            }
            else
            {
                id = character.DataId.ToString();
                body = character.Customize[(int)CustomizeIndex.ModelType].ToString();
                gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]) ? "Female" : "Male";
                race = character.Customize[(int)CustomizeIndex.Race].ToString();
                tribe = character.Customize[(int)CustomizeIndex.Tribe].ToString();
                eyes = character.Customize[(int)CustomizeIndex.EyeShape].ToString();

                if (type == XivChatType.TellIncoming || type == XivChatType.Party || type == XivChatType.Alliance ||
                    type == XivChatType.FreeCompany)
                {
                    var playerCharacter = new PlayerCharacter
                    {
                        Body = character.Customize[(int)CustomizeIndex.ModelType].ToString(),
                        Gender = Convert.ToBoolean(character.Customize[(int)CustomizeIndex.Gender]) ? "Female" : "Male",
                        Race = character.Customize[(int)CustomizeIndex.Race].ToString(),
                        Tribe = character.Customize[(int)CustomizeIndex.Tribe].ToString(),
                        EyeShape = character.Customize[(int)CustomizeIndex.EyeShape].ToString()
                    };
                    database.UpdateAndSavePlayerData(sender, playerCharacter);
                }
            }

            //Chat.Print($"{correctSender}: id[{id}] skeleton[{skeleton}] body[{body}] gender[{gender}] race[{race}] tribe[{tribe}] eyes[{eyes}]");
            XivEngine.Instance.Process(stringtype, correctSender, id, skeleton, message.ToString(), body, gender, race,
                tribe, eyes, ClientState.ClientLanguage.ToString(), new Vector3(-99), character, user);
        }
        catch (Exception ex)
        {
            LogError("Error in ChatText method. " + ex);
            PluginLog.Error($"ChatText ---> Exception: {ex}");
        }
    }


    public void TriggerLipSync(ICharacter character, string length)
    {
        if (Config.LipsyncEnabled && character != null)
            AddonTalkHandler.TriggerLipSync(character, length);
    }

    public void StopLipSync(ICharacter character)
    {
        if (Config.LipsyncEnabled && character != null)
            AddonTalkHandler.StopLipSync(character);
    }

    public void ClickTalk()
    {
        if (Config.TextAutoAdvanceEnabled)
            SetKeyValue(VirtualKey.NUMPAD0, KeyStateFlags.Pressed);
    }

    private static unsafe void SetKeyValue(VirtualKey virtualKey, KeyStateFlags keyStateFlag)
    {
        (*(int*)(Service.SigScanner.Module.BaseAddress +
                 Marshal.ReadInt32(Service.SigScanner.ScanText(KeyboardStateSignature) + 0x4) + 4 *
                 *(byte*)(Service.SigScanner.Module.BaseAddress +
                          Marshal.ReadInt32(Service.SigScanner.ScanText(KeyboardStateIndexArray) + 0x4) +
                          (int)virtualKey))) = (int)keyStateFlag;
    }

    public int GetNumberFromString(string value)
    {
        try
        {
            return int.Parse(value.Split('.')[1]) - 1;
        }
        catch
        {
            return -1;
        }
    }


    private void _filter_OnSoundIntercepted(object sender, InterceptedSound e)
    {
        if (_scdReplacements.ContainsKey(e.SoundPath))
            if (!e.SoundPath.Contains("se_vfx_monster"))
            {
                PluginLog.Info("Sound Mod Intercepted");
#if DEBUG
                        _chat.Print("Sound Mod Intercepted");
#endif
            }
    }


    private void _clientState_TerritoryChanged(ushort e)
    {
#if DEBUG
            _chat.Print("Territory is " + e);
#endif
    }
    //private unsafe bool IsResidential() {
    //    return HousingManager.Instance()->IsInside() || HousingManager.Instance()->OutdoorTerritory != null;
    //}

    private void _clientState_Logout(int type, int code)
    {
    }

    private void _clientState_Login()
    {
    }

    #endregion

    #region String Sanitization

    public string RemoveActionPhrases(string value)
    {
        return value.Replace("Direct hit ", null)
            .Replace("Critical direct hit ", null)
            .Replace("Critical ", null)
            .Replace("Direct ", null)
            .Replace("direct ", null);
    }

    public static string CleanSenderName(string senderName)
    {
        var senderStrings = SplitCamelCase(RemoveSpecialSymbols(senderName)).Split(" ");
        var playerSender = senderStrings.Length == 1 ? senderStrings[0] :
            senderStrings.Length == 2 ? senderStrings[0] + " " + senderStrings[1] :
            senderStrings[0] + " " + senderStrings[2];
        return playerSender;
    }

    public static string SplitCamelCase(string input)
    {
        return Regex.Replace(input, "([A-Z])", " $1",
            RegexOptions.Compiled).Trim();
    }

    public static string RemoveSpecialSymbols(string value)
    {
        var rgx = new Regex(@"[^a-zA-Z:/._\ -]");
        return rgx.Replace(value, "");
    }

    #endregion

    #region UI Management

    private void UiBuilder_Draw()
    {
        if (!texturesLoaded)
            try
            {
                var imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!, "logo.png"));
                Logo = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!, "icon.png"));
                Icon = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "generalSettings.png"));
                GeneralSettings = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "generalSettingsActive.png"));
                GeneralSettingsActive = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "dialogueSettings.png"));
                DialogueSettings = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "dialogueSettingsActive.png"));
                DialogueSettingsActive = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "audioSettings.png"));
                AudioSettings = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "audioSettingsActive.png"));
                AudioSettingsActive = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!, "archive.png"));
                Archive = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "archiveActive.png"));
                ArchiveActive = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!, "discord.png"));
                Discord = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!, "ko-fi.png"));
                KoFi = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "changelog.png"));
                Changelog = TextureProvider.GetFromFile(imagePath);
                imagePath = new FileInfo(Path.Combine(Interface.AssemblyLocation.Directory?.FullName!,
                    "changelogActive.png"));
                ChangelogActive = TextureProvider.GetFromFile(imagePath);
                if (Logo != null)
                {
                    _window.InitializeImageHandles();
                    texturesLoaded = true; // Set the flag if the logo is successfully loaded
                }
            }
            catch (Exception e)
            {
                PrintError("Failed to load textures: " + e.Message);
            }

        windowSystem.Draw();
    }

    private void UiBuilder_OpenConfigUi()
    {
        _window.Toggle();
    }

    #endregion

    #region Chat Commands

    [Command("/xivv")]
    [HelpMessage("OpenConfig")]
    public void ExecuteCommandA(string command, string args)
    {
        OpenConfig(command, args);
    }

    public void OpenConfig(string command, string args)
    {
        if (!disposed)
        {
            var splitArgs = args.Split(' ');
            if (splitArgs.Length > 0)
                switch (splitArgs[0].ToLower())
                {
                    case "help":
                        Chat.Print("Xiv Voices Commands:\r\n" +
                                   "on (Enable Xiv Voices)\r\n" +
                                   "off (Disable Xiv Voices)\r\n" +
                                   "mute (Mute/Unmute Volume)\r\n" +
                                   "skip (Skips currently playing dialogue)\r\n" +
                                   "volup (Increases volume by 10%)\r\n" +
                                   "voldown (Decreases volume by 10%)");
                        break;
                    case "on":
                        Config.Active = true;
                        _window.Configuration = Config;
                        Interface.SavePluginConfig(Config);
                        Config.Active = true;
                        break;
                    case "off":
                        Config.Active = false;
                        _window.Configuration = Config;
                        Interface.SavePluginConfig(Config);
                        Config.Active = false;
                        break;
                    case "mute":
                        if (!Config.Mute)
                        {
                            Config.Mute = true;
                            _window.Configuration = Config;
                            Interface.SavePluginConfig(Config);
                            Config.Mute = true;
                        }
                        else
                        {
                            Config.Mute = false;
                            _window.Configuration = Config;
                            Interface.SavePluginConfig(Config);
                            Config.Mute = false;
                        }

                        break;
                    case "skip":
                        audio.StopAudio();
                        break;
                    case "volup":
                        Config.Volume = Math.Clamp(Config.Volume + 10, 0, 100);
                        Config.LocalTTSVolume = Math.Clamp(Config.LocalTTSVolume + 10, 0, 100);
                        Interface.SavePluginConfig(Config);
                        break;
                    case "voldown":
                        Config.Volume = Math.Clamp(Config.Volume - 10, 0, 100);
                        Config.LocalTTSVolume = Math.Clamp(Config.LocalTTSVolume - 10, 0, 100);
                        Interface.SavePluginConfig(Config);
                        break;
                    default:
                        _window.Toggle();
                        break;
                }
        }
    }

    #endregion

    #region IDisposable Support

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        try
        {
            disposed = true;
            Config.Save();

            Chat.ChatMessage -= Chat_ChatMessage;
            Interface.UiBuilder.Draw -= UiBuilder_Draw;
            Interface.UiBuilder.OpenConfigUi -= UiBuilder_OpenConfigUi;
            windowSystem.RemoveAllWindows();
            commandManager?.Dispose();
            if (_filter != null) _filter.OnSoundIntercepted -= _filter_OnSoundIntercepted;
            try
            {
                ClientState.Login -= _clientState_Login;
                ClientState.Logout -= _clientState_Logout;
                ClientState.TerritoryChanged -= _clientState_TerritoryChanged;
            }
            catch (Exception e)
            {
                PluginLog.Warning(e, e.Message);
            }

            try
            {
                _framework.Update -= framework_Update;
            }
            catch (Exception e)
            {
                PluginLog.Warning(e, e.Message);
            }

            Filter?.Dispose();
            AddonTalkHandler?.Dispose();

            updater?.Dispose();
            database?.Dispose();
            audio?.Dispose();
            xivEngine?.Dispose();
            _window.Dispose();
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, e.Message);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}