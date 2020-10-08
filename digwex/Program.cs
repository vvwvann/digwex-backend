using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Digwex
{
#pragma warning disable CS1591
  public class Program
  {
    public static Random Rd = new Random();

    public static bool SERVICE_MODE;
    public static bool DEVELOP_MODE;
    public static bool INIT_MODE;


    public static async Task Main(string[] args)
    {
      Log.Instance.Init();
      foreach (var arg in args) {
        switch (arg.Trim()) {
          case "--service":
            SERVICE_MODE = true;
            break;
          case "--dev":
            DEVELOP_MODE = true;
            break;
          case "--init":
            INIT_MODE = true;
            break;
        }
      }

#if DEBUG
      if (!SERVICE_MODE) {
        try {
          Console.OutputEncoding = Encoding.UTF8;
        }
        catch { }
      }
#endif

      Log.Info("Start server");

      if (INIT_MODE) {
        if (Setup()) {
          Log.Info("Server setup success");
        }
        else {
          Log.Warn("Server setup fail");
        }
        return;
      }

      await CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
      var host = Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder => {
          webBuilder.UseStartup<Startup>();
          webBuilder.UseSentry(options => {
            options.BeforeSend = @event => {
              return @event;
            };
          });
        });

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        host.UseWindowsService();
      }

      return host;
    }

    public static bool Setup()
    {
      bool ok;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        ok = new PostgresSetup().Setup();

        if (!ok) return false;
      }

      ok = false;

      try {
        ServiceController sc = new ServiceController("DigwexService");

        if (sc.Status == ServiceControllerStatus.Running) {
          Log.Info("Service found");
          ok = CmdExec("/C sc stop DigwexService");

          if (!ok) {
            Log.Info("Unable to stop service");
            return false;
          }
        }
        ok = true;
      }
      catch {

      }

      if (!ok) {
        Log.Info("Service not found. Try create service");

        ok = CmdExec($"/C sc create DigwexService binPath= \"\"{AppContext.BaseDirectory}\\Digwex.exe\" --service\"", AppContext.BaseDirectory)
          && CmdExec("/C sc failure DigwexService actions= restart/15000 reset= 86400");

        if (!ok) {
          Log.Info("Unable to create service");
          return false;
        }
      }

      ok = CmdExec("/C sc start DigwexService")
          && CmdExec("/C sc config DigwexService start=auto");

      if (!ok) {
        Log.Info("Unable to start service");
        return false;
      }

      return ok;
    }

    public static bool CmdExec(string command, string path = null)
    {
      ProcessStartInfo info = new ProcessStartInfo {
        FileName = "cmd",
        Arguments = command,
        WorkingDirectory = path,
        UseShellExecute = false
      };

      Process cmd = new Process {
        StartInfo = info
      };
      cmd.Start();

      if (!cmd.HasExited) {
        cmd.WaitForExit();
      }

      return cmd.ExitCode == 0;
    }
  }
}
