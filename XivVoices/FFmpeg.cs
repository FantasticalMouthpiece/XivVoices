using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Sockets;
using System.IO;
using System;
using XivVoices.Engine;

namespace XivVoices;

public class FFmpeg : IDisposable
{
  public Plugin PluginReference { get; internal set; }

  public bool isFFmpegWineProcessRunning = false;
  private Process ffmpegWineProcess = null;

  public FFmpeg()
  {
  }

  public async Task Initialize()
  {
    if (Dalamud.Utility.Util.IsWine())
    {
      isFFmpegWineProcessRunning = await SendFFmpegWineCommand("");
      if (!isFFmpegWineProcessRunning)
      {
        StartFFmpegWineProcess();
      }
    }
  }

  public void Dispose()
  {
    StopFFmpegWineProcess();
  }

  public async Task ExecuteFFmpegCommand(string arguments)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    if (Dalamud.Utility.Util.IsWine() && Plugin.Config.WineUseNativeFFmpeg)
    {
      string xlcorePath = Plugin.Interface.ConfigDirectory.ToString().Replace("\\", "/").Replace("Z:/", "/").Replace("/pluginConfigs/XivVoices", "");
      string _arguments = arguments.Replace("\\", "/").Replace(Plugin.Config.WorkingDirectory, $"{xlcorePath}/wineprefix/drive_c/XIV_Voices");
      Plugin.PluginLog.Information($"ExecuteFFmpegCommand: {_arguments}");
      bool success = await SendFFmpegWineCommand($"ffmpeg {_arguments}");
      if (!success)
      {
        PluginReference.Chat.Print("[XIVV] Failed to run ffmpeg natively. See '/xivv wine' for more information.");
        await ExecuteFFmpegCommandWindows(arguments);
      }
    } else {
      await ExecuteFFmpegCommandWindows(arguments);
    }
    stopwatch.Stop();
    Plugin.PluginLog.Information($"ExecuteFFmpegCommand took {stopwatch.ElapsedMilliseconds} ms.");
  }

  private static async Task ExecuteFFmpegCommandWindows(string arguments)
  {
    string ffmpegDirectoryPath = Path.Combine(XivEngine.Instance.Database.ToolsPath);
    Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegDirectoryPath);
    Xabe.FFmpeg.IConversion conversion = Xabe.FFmpeg.FFmpeg.Conversions.New().AddParameter(arguments);
    await conversion.Start();
  }

  private static async Task<bool> SendFFmpegWineCommand(string command)
  {
    try
    {
      using (TcpClient client = new TcpClient("127.0.0.1", 6914))
      using (NetworkStream stream = client.GetStream())
      using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
      using (StreamReader reader = new StreamReader(stream))
      {
        await writer.WriteLineAsync($"{command}\n");
        await reader.ReadLineAsync();
        return true;
      }
    }
    catch (Exception ex)
    {
      return false;
    }
  }

  public void StartFFmpegWineProcess()
  {
    if (isFFmpegWineProcessRunning) return;
    isFFmpegWineProcessRunning = true;
    try {
      ffmpegWineProcess = new Process();
      ffmpegWineProcess.StartInfo.FileName = "/usr/bin/env";
      ffmpegWineProcess.StartInfo.Arguments = $"bash {Path.Combine(Plugin.Interface.AssemblyLocation.Directory?.FullName!, "ffmpeg-wine.sh").Replace("\\", "/").Replace("Z:/", "/")}";
      Plugin.PluginLog.Information(ffmpegWineProcess.StartInfo.Arguments);
      ffmpegWineProcess.StartInfo.UseShellExecute = false;
      ffmpegWineProcess.Start();
      _ = Task.Run(async () =>
      {
        await Task.Delay(500);
        isFFmpegWineProcessRunning = await SendFFmpegWineCommand("");
        if (!isFFmpegWineProcessRunning)
        {
          PluginReference?.Chat.Print("[XIVV] Failed to run ffmpeg natively. See '/xivv wine' for more information.");
        }
      });
    } catch (Exception ex) {
      Plugin.PluginLog.Error($"StartFFmpegWineProcess failed: {ex}");
      PluginReference.Chat.Print("[XIVV] Failed to run ffmpeg natively. See '/xivv wine' for more information.");
      isFFmpegWineProcessRunning = false;
    }
  }

  public void StopFFmpegWineProcess()
  {
    if (!isFFmpegWineProcessRunning) return;
    isFFmpegWineProcessRunning = false;
    SendFFmpegWineCommand("exit").GetAwaiter().GetResult();
    if (ffmpegWineProcess != null)
    {
      ffmpegWineProcess.Dispose();
      ffmpegWineProcess = null;
    }
  }
}
