using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Sockets;
using System.Linq;
using System.IO;
using System;
using XivVoices.Engine;
using Microsoft.Win32;

namespace XivVoices;

public class FFmpeg : IDisposable
{
  public Plugin PluginReference { get; internal set; }

  public bool isFFmpegWineProcessRunning = false;
  private Process ffmpegWineProcess = null;
  public int FFmpegWineProcessPort = 1469;

  public FFmpeg()
  {
  }

  public async Task Initialize()
  {
    if (Dalamud.Utility.Util.IsWine())
    {
      SetWineRegistry();

      isFFmpegWineProcessRunning = await SendFFmpegWineCommand("");
      if (Plugin.Config.WineUseNativeFFmpeg)
      {
        StartFFmpegWineProcess();
      }
      else
      {
        StopFFmpegWineProcess();
      }
    }
  }

  public void Dispose()
  {
    StopFFmpegWineProcess();
  }

  // https://gitlab.winehq.org/wine/wine/-/wikis/FAQ#how-do-i-launch-native-applications-from-a-windows-application
  private void SetWineRegistry()
  {
    string regPath = "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Environment";
    string valueName = "PATHEXT";

    try
    {
      using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
      {
        if (key == null)
        {
          Plugin.PluginLog.Error($"Error in SetWineRegistry: key is null");
          return;
        }

        string currentValue = key.GetValue(valueName) as string;
        if (currentValue == null)
        {
          Plugin.PluginLog.Error($"Error in SetWineRegistry: PATHEXT value not found.");
          return;
        }

        string[] extensions = currentValue.Split(";", StringSplitOptions.RemoveEmptyEntries);

        if (!extensions.Contains("."))
        {
          string newValue = string.Join(";", extensions.Append("."));
          key.SetValue(valueName, newValue);
          Plugin.PluginLog.Information("SetWineRegistry: successfully updated registry");
        }
        else
        {
          Plugin.PluginLog.Information("SetWineRegistry: registry already updated");
        }
      }
    }
    catch (Exception ex)
    {
      Plugin.PluginLog.Error($"Error in SetWineRegistry: {ex}");
    }
  }

  public bool IsMac()
  {
    return Plugin.Interface.ConfigDirectory.ToString().Replace("\\", "/").Contains("Mac");  // because of 'XIV on Mac'
  }

  // Doesn't actually get a WINE path but the XIV_Voices path within the winepath on the host
  private string GetWineXIVVPath()
  {
    string configDirectory = Plugin.Interface.ConfigDirectory.ToString().Replace("\\", "/");
    bool isMac = IsMac();
    string baseDirectory = ""; // directory containing "wineprefix"
    if (isMac) baseDirectory = configDirectory.Replace("/pluginConfigs/XivVoices", ""); // XIVonMac
    else baseDirectory = configDirectory.Replace("/pluginConfigs/XivVoices", ""); // XIVLauncher
    string xivvDirectory = baseDirectory += "/wineprefix/drive_c/XIV_Voices"; // seems to always be this
    xivvDirectory = xivvDirectory.Substring(2); // strip Z: or whatever drive may be used
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

  private async Task ExecuteFFmpegCommandWindows(string arguments)
  {
    string ffmpegDirectoryPath = Path.Combine(XivEngine.Instance.Database.ToolsPath);
    Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegDirectoryPath);
    Xabe.FFmpeg.IConversion conversion = Xabe.FFmpeg.FFmpeg.Conversions.New().AddParameter(arguments);
    await conversion.Start();
  }

  private async Task<bool> SendFFmpegWineCommand(string command)
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
