using System;

namespace Digwex.Helpers
{
  public static class DateTimeExtensions
  {
    public static long ToUnix(this DateTime dateTime)
    {
      return (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }
  }
}
