using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;

namespace XivVoices;

public static class Lipsync {
  // stripped
  public enum CharacterMode : byte {
    None = 0,
    EmoteLoop = 3,
  }

  [StructLayout(LayoutKind.Explicit)]
  public unsafe struct ActorMemory
  {
    [FieldOffset(0x0A20)] public AnimationMemory Animation;
    [FieldOffset(0x2354)] public byte CharacterMode;
  }

  [StructLayout(LayoutKind.Explicit)]
  public unsafe struct AnimationMemory
  {
      [FieldOffset(0x2E8)] public ushort LipsOverride;
  }

  public static unsafe void SetLipsOverride(IntPtr characterAddress, ushort newLipsOverride)
  {
    ActorMemory* actorMemory = (ActorMemory*)characterAddress;
    if (actorMemory == null) return;
    AnimationMemory* animationMemory = (AnimationMemory*)Unsafe.AsPointer(ref actorMemory->Animation);
    if (animationMemory == null) return;
    animationMemory->LipsOverride = newLipsOverride;
  }

  public static unsafe CharacterMode GetCharacterMode(IntPtr characterAddress)
  {
    ActorMemory* actorMemory = (ActorMemory*)characterAddress;
    if (actorMemory == null) return CharacterMode.None;
    return (CharacterMode)actorMemory->CharacterMode;
  }

  public static unsafe void SetCharacterMode(IntPtr characterAddress, CharacterMode characterMode)
  {
    ActorMemory* actorMemory = (ActorMemory*)characterAddress;
    if (actorMemory == null) return;
    actorMemory->CharacterMode = (byte)characterMode;
  }
}
