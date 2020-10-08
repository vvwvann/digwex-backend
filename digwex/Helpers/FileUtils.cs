using System.IO;
using System.Threading.Tasks;

namespace Digwex.Helpers
{
  public static class FileUtils
  {
    public static bool TryExistOrCreate(string path)
    {
      try {
        DirectoryInfo di = new DirectoryInfo(path);
        if (di.Exists) return true;
        else di.Create();
      }
      catch {
        return false;
      }
      return true;
    }

    public static async Task<bool> TryExistOrCreateAsync(string path)
    {
      return await Task.Run(() => {
        try {
          DirectoryInfo di = new DirectoryInfo(path);
          if (di.Exists) return true;
          else di.Create();
        }
        catch {
          return false;
        }
        return true;
      });
    }

    public static async Task<Stream> OpenReadAsync(string path)
    {
      FileStream fileStream = null;
      MemoryStream memoryStream = null;
      byte[] buffer = new byte[4096];
      try {
        fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        int size;
        memoryStream = new MemoryStream();
        while ((size = await fileStream.ReadAsync(buffer, 0, 4096)) > 0) {
          await memoryStream.WriteAsync(buffer, 0, size);
        }
        memoryStream.Position = 0;
      }
      catch { }
      finally {
        fileStream?.Dispose();
      }
      return memoryStream;
    }

    public static Stream OpenRead(string path)
    {
      FileStream fileStream = null;
      MemoryStream memoryStream = null;
      byte[] buffer = new byte[4096];
      try {
        fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        int size;
        memoryStream = new MemoryStream();
        while ((size = fileStream.Read(buffer, 0, 4096)) > 0) {
          memoryStream.Write(buffer, 0, size);
        }
        memoryStream.Position = 0;
      }
      catch { }
      finally {
        fileStream?.Dispose();
      }
      return memoryStream;
    }
  }
}
