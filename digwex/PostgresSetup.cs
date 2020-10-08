using System;
using System.IO;
using Microsoft.Win32;

namespace Digwex
{
#pragma warning restore CS1591

  class PostgresSetup
  {
    private const string REG_KEY = @"SOFTWARE\PostgreSQL\Installations";
    private const string INSTALLER_RELATIVE_PATH = "..\\database";
    public const string USERNAME = "Digwex";
    public const string PASSWORD = "Digwexpass";

#if DEBUG
    public const string DEFAULT_PASSWORD = "anyany";
#else
    public const string DEFAULT_PASSWORD = "postgres";
#endif

    private readonly string _installerDir;
    private readonly string _installerPath;

    public PostgresSetup()
    {
      _installerDir = AppContext.BaseDirectory + "..\\database";
      _installerPath = _installerDir + "\\postgres.exe";
    }

    public bool Setup()
    {
      string postgresLocation = PostgresLocation();

      bool isInstall = false;
      if (postgresLocation == null) {
        isInstall = Install();
        if (!isInstall) return false;

        postgresLocation = PostgresLocation();

        if (postgresLocation == null) return false;
      }
      else {
        Log.Info($"Auto-detected PostgreSQL installation: " + postgresLocation);
      }

      return Configure(postgresLocation + "\\bin", isInstall);
    }

    private bool Install()
    {
      Log.Info($"Start install postgres");

      if (!File.Exists(_installerPath)) {
        Log.Info($"Not found installer file: {_installerPath}");
        return false;
      }

      bool ok = Program.CmdExec($"/C start /w postgres.exe --unattendedmodeui minimal --mode unattended --superpassword {DEFAULT_PASSWORD}", _installerDir);

      Log.Info($"{(ok ? "Success" : "Fail")} postgres install");

      return ok;
    }

    private bool Configure(string path, bool isInstall = false)
    {
      string command = $"/C set PGPASSWORD={PASSWORD}&& " +
        $"psql -c \"ALTER USER {USERNAME} WITH PASSWORD '{PASSWORD}'\" -U {USERNAME}";

      bool ok = Program.CmdExec(command, path);

      if (ok) {
        Log.Info($"Found user \"{USERNAME}\"");
        return true;
      }

      if (isInstall) {
        Log.Info("Create user after install postgres");
        ok = Program.CmdExec($"/C set PGPASSWORD={DEFAULT_PASSWORD}&& " +
          $"psql -c \"CREATE ROLE {USERNAME} PASSWORD '{PASSWORD}' NOSUPERUSER CREATEDB LOGIN\" -U postgres", path);

        Log.Info($"{(ok ? "Success" : "Fail")} create user \"{USERNAME}\"");

        return ok;
      }

      ok = false;

      Log.Info("Create user to an existing database");

      while (!ok) {

        Console.Write("\nEnter user from postgres database (default - \"postgres\"): ");

        string user = Console.ReadLine().Trim();

        if (string.IsNullOrEmpty(user)) {
          user = "postgres";
        }

        command = $"/C psql -c \"CREATE ROLE {USERNAME} PASSWORD '{PASSWORD}' NOSUPERUSER CREATEDB LOGIN\" -U {user} -W";

        ok = Program.CmdExec(command, path);
      }

      Log.Info($"{(ok ? "Success" : "Fail")} create user \"{USERNAME}\"");

      return ok;
    }

    private string PostgresLocation()
    {
      // check postgress installed
      string postgresDir;
      try {
        RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        RegistryKey skey = localMachine.OpenSubKey(REG_KEY);

        if(skey == null) {
          localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
          skey = localMachine.OpenSubKey(REG_KEY);
        }

        string[] keys = skey.GetSubKeyNames();

        string next = null;

        foreach (string key in keys) {
          if (key.Trim().ToLower().IndexOf("postgresql-") != -1) {
            next = key;
            break;
          }
        }

        if (next == null) return null;

        skey = skey.OpenSubKey(next);

        postgresDir = skey.GetValue("Base Directory").ToString();
      }
      catch (Exception ex) {
        Console.WriteLine(ex);
        return null;
      }

      return postgresDir;
    }
  }
}
