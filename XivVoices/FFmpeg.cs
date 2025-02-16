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
  private static int FFmpegWineProcessPort = 1469;

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

  // Doesn't actually get a WINE path but the XIV_Voices path within the winepath on the host
  private string GetWineXIVVPath()
  {
    string configDirectory = Plugin.Interface.ConfigDirectory.ToString().Replace("\\", "/");
    bool isMac = configDirectory.Contains("Mac"); // because of 'XIV on Mac'

    string baseDirectory = ""; // directory containing "wineprefix"
    if (isMac)
    {
      // XIVonMac
      baseDirectory = configDirectory.Replace("/pluginConfigs/XivVoices", "");
    }
    else
    {
      // XIVLauncher
      baseDirectory = configDirectory.Replace("/pluginConfigs/XivVoices", "");
    }

    string xivvDirectory = baseDirectory += "/wineprefix/drive_c/XIV_Voices";
    
    // strip Z: or whatever drive may be used
    xivvDirectory = xivvDirectory.Substring(2);

    return xivvDirectory;
  }

  public async Task ExecuteFFmpegCommand(string arguments)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    if (Dalamud.Utility.Util.IsWine() && Plugin.Config.WineUseNativeFFmpeg)
    {
      string _arguments = arguments.Replace("\\", "/").Replace(Plugin.Config.WorkingDirectory, GetWineXIVVPath());
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
      using (TcpClient client = new TcpClient("127.0.0.1", FFmpegWineProcessPort))
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
      string ffmpegWineShPath = Path.Combine(Plugin.Interface.AssemblyLocation.Directory?.FullName!, "ffmpeg-wine.sh").Replace("\\", "/");
      ffmpegWineShPath = ffmpegWineShPath.Substring(2); // strip Z: or whatever drive may be used
      ffmpegWineProcess.StartInfo.Arguments = $"bash \"{ffmpegWineShPath}\" {FFmpegWineProcessPort}";
      Plugin.PluginLog.Information($"ffmpegWineProcess.StartInfo.Arguments: {ffmpegWineProcess.StartInfo.Arguments}");
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
