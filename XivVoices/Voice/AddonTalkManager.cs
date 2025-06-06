﻿using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace XivVoices.Voice {
    public class AddonTalkManager : AddonManager {
        public AddonTalkManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) : base(
            framework, clientState, condition, gui, "Talk") {
        }

        public unsafe AddonTalkText? ReadText() {
            var addonTalk = GetAddonTalk();
            return addonTalk == null ? null : TalkUtils.ReadTalkAddon(addonTalk);
        }

        public unsafe AddonTalkText? ReadTextBattle() {
            var addonTalk = GetAddonTalkBattle();
            return addonTalk == null ? null : TalkUtils.ReadTalkAddon(addonTalk);
        }

        public unsafe bool IsVisible() {
            var addonTalk = GetAddonTalk();
            return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
        }

        public unsafe bool Hide()
        {
            var addonTalk = GetAddonTalk();
            return addonTalk != null && (addonTalk->AtkUnitBase.IsVisible = false) == false;
        }

        public unsafe bool Show()
        {
            var addonTalk = GetAddonTalk();
            return addonTalk != null && (addonTalk->AtkUnitBase.IsVisible = true) == true;
        }

        public unsafe AddonTalk* GetAddonTalk() {
            return (AddonTalk*)Address.ToPointer();
        }

        private unsafe AddonBattleTalk* GetAddonTalkBattle() {
            return (AddonBattleTalk*)Address.ToPointer();
        }
    }
}
