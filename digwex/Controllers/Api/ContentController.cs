using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Digwex.Data;
using Digwex.Helpers;
using FFMpegCore;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.AspNetCore.Authorization;

namespace Digwex.Controllers.Api
{
  //[Authorize]
  [ApiController]
  [Route("api/[controller]")]
  [Produces("application/json")]
  public class ContentController : DefaultController
  {
    private readonly ThumbCreator _thumbler;
    private static readonly FormOptions _defaultFormOptions = new FormOptions();
    public readonly static Dictionary<string, string[]> _supportMimeTypes
      = new Dictionary<string, string[]>() {
        ["image/jpeg"] = new string[] { ".jpg", "image" },
        ["video/mp4"] = new string[] { ".mp4", "video" },
        ["application/zip"] = new string[] { ".zip", "html" },
        ["application/x-zip-compressed"] = new string[] { ".zip", "html" }
      };

    public ContentController(ApplicationDbContext context) : base(context)
    {
      _thumbler = ThumbCreator.Instance;
    }

    [HttpGet("/api/[controller]s")]
    public async Task<ContentEntity[]> All()
    {
      var user = await UserAsync();
      return await _context.Contents
        .Where(s => s.User == user)
        .OrderByDescending(s => s.AddTime)
        .ToArrayAsync();
    }

    //// GET: Contents/Details/5
    //public async Task<IActionResult> Details(int? id)
    //{
    //  if (id == null) {
    //    return NotFound();
    //  }

    //  var contentEntity = await _context.Contents
    //      .FirstOrDefaultAsync(m => m.Id == id);
    //  if (contentEntity == null) {
    //    return NotFound();
    //  }

    //  return View(contentEntity);
    //}

    [HttpPost("url/add")]
    public async Task<ContentEntity> AddUrl(UrlModel model)
    {
      UriBuilder builder = new UriBuilder(model.Url);

      ContentEntity content = new ContentEntity {
        Url = builder.Uri.AbsoluteUri,
        Name = model.Name,
        Duration = model.Duration > 0 ? model.Duration : 5,
        Type = "url",
        User = await UserAsync()
      };

      _context.Add(content);
      await _context.SaveChangesAsync();
      return content;
    }

    [ProducesResponseType(typeof(ContentEntity), 201)]
    [HttpPost("/api/[controller]s/upload", Name = "upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
      if (!_supportMimeTypes.TryGetValue(file.ContentType, out string[] value))
        return StatusCode(415);

      string unix = DateTime.UtcNow.ToUnix().ToString("x"); //lower
      string guid = Guid.NewGuid().ToString("N");
      string prefix = guid[0] + "/" + guid[1] + "/" + guid[2];  // 0/a/f 
      string sufix = guid + unix; // guidunix
      string prefsuf = prefix + '/' + sufix; // 0/a/f/guidunix
      string dir = Startup.STORAGE_PATH + "/files/" + prefix;
      string dirThumb = Startup.STORAGE_PATH + "/thumb/" + prefix;

      if (!await FileUtils.TryExistOrCreateAsync(dir)
        || !await FileUtils.TryExistOrCreateAsync(dirThumb))
        return StatusCode(500);

      string path = dir + "/" + sufix + value[0];

      Console.WriteLine(path);
      string md5 = "";
      using (var streamReader = new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite)) {
        await file.CopyToAsync(streamReader);
        streamReader.Position = 0;
        md5 = await MD5Helpers.ToMD5Async(streamReader);
      }

      dirThumb += ('/' + sufix);

      ThumbParam[] thumbParams = new[]
           {
              new ThumbParam(dirThumb + "_s.jpg" , 150, 200),
              new ThumbParam(dirThumb + "_l.jpg", 960, 540)
            };
      int duration = 5;
      bool audio = false;

      int width;
      int height;
      switch (value[1]) {
        case "image":
          if (!_thumbler.SaveFromFile(path, thumbParams, out width, out height))
            return StatusCode(500);
          break;
        case "video":
          string tmp = $"{Startup.TMP_PATH}/{prefix}";

          if (!await FileUtils.TryExistOrCreateAsync(tmp))
            return StatusCode(500);

          try {
            var source = await FFProbe.AnalyseAsync(path);
            duration = (int)source.Duration.TotalSeconds;
            tmp += '/' + guid + unix + ".jpg";
            if (!await _thumbler.SaveFromVideo(source, tmp, thumbParams))
              return StatusCode(500);
            width = source.PrimaryVideoStream.Width;
            height = source.PrimaryVideoStream.Height;
          }
          catch {
            return StatusCode(500);
          }
          break;

        default: return StatusCode(500);
      }

      ContentEntity content = new ContentEntity {
        Md5 = md5,
        Ext = value[0],
        Duration = duration,
        Name = sufix,
        Url = "content/" + prefsuf + value[0],
        Type = value[1],
        Width = width,
        Height = height,
        //Orientation = width >= height ? OrientationScreen.Landspace : OrientationScreen.Portrait,
        Size = file.Length,
        Audio = audio,
        SmallThumb = "thumb/" + prefsuf + "_s.jpg",
        LargeThumb = "thumb/" + prefsuf + "_l.jpg",
        User = await UserAsync()
      };

      await _context.AddAsync(content);
      await _context.SaveChangesAsync();

      return StatusCode(201, content);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
      _context.Contents.Remove(new ContentEntity {
        Id = id
      });
      await _context.SaveChangesAsync();
      return Ok();
    }
  }

  public class UrlModel
  {
    public string Url { get; set; }
    public string Name { get; set; }
    public int Duration { get; set; }
  }


  public class ThumbCreator
  {
    private static readonly ThumbCreator _instance = new ThumbCreator();

    //private readonly ChromeOptions _options;

    public static ThumbCreator Instance => _instance;

    private ThumbCreator()
    {
      //_options = new ChromeOptions();
      //_options.AddArgument("--headless");
      //_options.AddArgument("--window-size=1280,720");
    }

    public async Task<bool> SaveFromVideo(MediaAnalysis info, string path, ThumbParam[] thumbParams)
    {

      double seconds = info.Duration.TotalSeconds;
      TimeSpan captureTime = TimeSpan.FromSeconds(seconds > 60
           ? 10 : seconds / 3);

      Bitmap bmp = null;
      await Task.Run(() => {

        bmp = FFMpeg.Snapshot(
             info,
             new Size(1280, 720),
             captureTime);
      });
      if (bmp == null) return false;
      return SaveFromBitmap(bmp, thumbParams);
    }

    public bool SaveFromBitmap(Bitmap bmp, ThumbParam[] thumbParams)
    {
      try {
        foreach (var item in thumbParams) {
          SaveToFile(item.Path, bmp, item.Width, item.Height);
        }
        return true;
      }
      catch {
        return false;
      }
    }

    public bool SaveFromFile(string url, ThumbParam[] thumbParams, out int width, out int height)
    {
      Bitmap bmp = null;
      width = 0;
      height = 0;
      try {
        bmp = new Bitmap(url);
        width = bmp.Width;
        height = bmp.Height;
        foreach (var item in thumbParams) {
          SaveToFile(item.Path, bmp, item.Width, item.Height);
        }
        return true;
      }
      catch (Exception ex) {
        Console.WriteLine(ex);
        return false;
      }
    }

    public async Task<List<Stream>> ThumbFromStream(Stream stream, ThumbParam[] thumbParams)
    {
      var list = new List<Stream>();
      try {
        stream.Position = 0;
        Bitmap bmp = new Bitmap(stream);
        foreach (var item in thumbParams) {
          Stream tmp = BitmapToStream(bmp, item.Width, item.Height);

          using var fileStream = new FileStream(item.Path, FileMode.Create);
          await tmp.CopyToAsync(fileStream);
          tmp.Position = 0;
          list.Add(tmp);
        }
        return list;
      }
      catch {
        return null;
      }
    }

    public bool SaveFromStream(Stream stream, ThumbParam[] thumbParams)
    {
      Bitmap bmp = null;
      try {
        bmp = new Bitmap(stream);
        foreach (var item in thumbParams) {
          SaveToFile(item.Path, bmp, item.Width, item.Height);
        }
        return true;
      }
      catch {
        return false;
      }
    }

    public bool SaveFromHtml(string url, ThumbParam[] thumbParams)
    {
      Bitmap bmp = ThumbHtml(url);
      if (bmp == null) return false;
      try {
        foreach (var item in thumbParams) {
          SaveToFile(item.Path, bmp, item.Width, item.Height);
        }
      }
      catch {
        return false;
      }
      finally {
        bmp.Dispose();
      }
      return true;
    }

    private void SaveToFile(string path, Bitmap bmp, int width, int height)
    {
      double w = bmp.Width;
      double h = bmp.Height;
      if (w < width)
        width = (int)w;
      if (h < height)
        height = (int)h;

      double nW = width;
      double nH = width / w * h;
      if (nH > height) {
        nH = height;
        nW = height / h * w;
      }

      Bitmap target = null;
      Graphics graphics = null;
      try {
        target = new Bitmap((int)nW, (int)(nH));
        graphics = Graphics.FromImage(target);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(bmp, 0, 0, target.Width, target.Height);
        target.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
      }
      finally {
        graphics?.Dispose();
        target?.Dispose();
      }
    }

    private Stream BitmapToStream(Bitmap bmp, int width, int height)
    {
      double w = bmp.Width;
      double h = bmp.Height;
      if (w < width)
        width = (int)w;
      if (h < height)
        height = (int)h;

      double nW = width;
      double nH = width / w * h;
      if (nH > height) {
        nH = height;
        nW = height / h * w;
      }

      Bitmap target = null;
      Graphics graphics = null;
      try {
        target = new Bitmap((int)nW, (int)(nH));
        graphics = Graphics.FromImage(target);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(bmp, 0, 0, target.Width, target.Height);
        var stream = new MemoryStream();
        target.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
        stream.Position = 0;
        return stream;
      }
      finally {
        graphics?.Dispose();
        target?.Dispose();
      }
    }

    public Bitmap ThumbHtml(string url)
    {
      //ChromeDriver driver = new ChromeDriver(Startup.CromeDriverPath, _options);
      //driver.Navigate().GoToUrl(url);
      //Screenshot screenshot = driver.GetScreenshot();
      //driver.Quit();
      //byte[] bytes = screenshot.AsByteArray;

      //Stream stream = new MemoryStream(bytes);
      //try {
      //  stream.Position = 0;
      //  return new Bitmap(stream);
      //}
      //catch {
      //  stream.Dispose();
      //}
      return null;
    }
  }

  public class ScreenHelper
  {
    public string Path { get; set; }

    public ThumbParam[] ThumbParams { get; set; }

    public MediaAnalysis VideoInfo { get; set; }

    public string Type { get; set; }

    public int ContentId { get; set; } = -1;

    public int ResourceId { get; set; } = -1;

    public string LargeThumb { get; set; }

    public string SmallThumb { get; set; }
  }

  public struct ThumbParam
  {
    public ThumbParam(string path, int width, int height)
    {
      Path = path;
      Width = width;
      Height = height;
    }
    public string Path;
    public int Width;
    public int Height;
  }
}
