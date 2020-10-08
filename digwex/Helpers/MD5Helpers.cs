using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Digwex.Helpers
{
  public static class MD5Helpers
  {
    public static string ToMD5(byte[] bytes)
    {
      using (MD5 md5 = MD5.Create()) {
        bytes = md5.ComputeHash(bytes);
      }

      var sb = new StringBuilder();

      for (int i = 0; i < bytes.Length; i++) {
        sb.Append(bytes[i].ToString("X2"));
      }
      return sb.ToString();
    }

    public static string ToMD5(string s) // NoNull
    {
      return ToMD5(Encoding.UTF8.GetBytes(s));
    }

    public static string ToMD5(Stream stream)
    {
      byte[] hash = null;
      using (MD5 md5 = MD5.Create()) {
        hash = md5.ComputeHash(stream);
      }

      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < hash.Length; i++) {
        sb.Append(hash[i].ToString("X2"));
      }
      return sb.ToString();
    }

    public static async Task<string> ToMD5Async(Stream stream)
    {
      return await Task.Run(() => {
        byte[] hash = null;
        using (MD5 md5 = MD5.Create()) {
          hash = md5.ComputeHash(stream);
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < hash.Length; i++) {
          sb.Append(hash[i].ToString("X2"));
        }
        return sb.ToString();
      });
    }

    public static string ToMD5File(string path)
    {
      using (Stream stream = File.OpenRead(path)) {
        return ToMD5(stream);
      }
    }
  }

}
