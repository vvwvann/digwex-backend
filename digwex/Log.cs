using NLog;
using NLog.Web;
using System;

namespace Digwex
{

  public sealed class Log
  {
    private static readonly Lazy<Log> _instance =
        new Lazy<Log>(() => new Log());

    private static Logger _log;

    private Log()
    {
      _log = NLogBuilder.ConfigureNLog(AppContext.BaseDirectory + "nlog.config").GetCurrentClassLogger();
    }

    public static Log Instance => _instance.Value;

    public void Init() { }

    public static void Info(string message)
    {
      _log.Info(message);
    }

    public static void Warn(string message)
    {
      _log.Warn(message);
    }

    public static void Warn(Exception exception, string message)
    {
      _log.Warn(exception, message);
    }

    public static void Warn(Exception exception)
    {
      _log.Warn(exception);
    }

    public static void Fatal(Exception exception)
    {
      _log.Fatal(exception.ToString());
      //try {
      //  _sentry.Fatal(exception);
      //}
      //catch { }
    }

    public static void WarnSentry(Exception ex)
    {
      _log.Warn(ex.ToString());
      //try {
      //  _sentry.Warning(ex);
      //}
      //catch { }
    }

    public static void Debug(string message)
    {
      _log.Debug(message);
    }

    public static void Error(string message)
    {
      _log.Error(message);
    }

    public static void Error(Exception exception, string message)
    {
      _log.Error(exception, message);
    }
  }
}
