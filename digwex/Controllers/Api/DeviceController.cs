using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Digwex.Data;
using Digwex.Helpers;

namespace Digwex.Controllers.Api
{
  [ApiController]
  [Route("v4/[controller]")]
  [ApiExplorerSettings(IgnoreApi = true)]
  public class DeviceController : ControllerBase
  {
    private ApplicationDbContext _context;

    public DeviceController(ApplicationDbContext context)
    {
      _context = context;
      //Console.WriteLine("HASHD: " + _context.Model.GetHashCode());
    }

    [HttpGet("data")]
    public async Task<SynchronizeModel> Data()
    {
      if (!Request.Headers.TryGetValue("Authorization", out StringValues oauth)) {
        Response.StatusCode = 401;
        return null;
      }

      //Oauth <token>
      string[] values = oauth[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      if (values[0].ToLower() != "oauth") {
        Response.StatusCode = 400;
        return null;
      }

      long pin = Convert.ToInt64(values[1].Substring(32), 16);

      PlayerEntity player = await _context.Players
        .AsNoTracking()
        .Include(s => s.Calendar)
        .FirstOrDefaultAsync(s => s.PinPriv == pin);

      if (player == null) {
        Response.StatusCode = 404;
        return null;
      }

      if (player.Calendar == null) {
        return new SynchronizeModel {
          Id = Program.Rd.Next(15, int.MaxValue),
          ContentPackage = new ContentPackage {
            PlaybackItems = new PlaybackItem[0],
            Files = new FileModel[0],
            Triggers = new Trigger[0]
          }
        };
      }

      DateTime start = DateTime.UtcNow.Date;
      DateTime until = start.AddYears(100);


      CalendarPlaylistEntity[] calendarPlaylists = await _context.CalendarPlaylist
        .AsNoTracking()
        .Where(s => s.CalendarId == player.Calendar.Id)
        .Include(s => s.Playlist)
          .ThenInclude(s => s.PlaylistContents)
            .ThenInclude(s => s.Content)
        .ToArrayAsync();

      string storagePath = $"{Request.Scheme}://{Request.Host}/";

      var triggers = new List<Trigger>();
      var dictFiles = new Dictionary<long, int>();
      var files = new List<FileModel>();
      var playbackItems = new List<PlaybackItem>();

      int triggerId = 0;
      Trigger baseTrigger = null;
      foreach (var item in calendarPlaylists) {
        PlaylistContentEntity[] contentPlylists = item.Playlist.PlaylistContents.ToArray();
        int playlistId = item.Playlist.Id;
        var playbackItemsLocal = new List<int>();

        foreach (var pair in contentPlylists) {

          ContentEntity content = pair.Content;

          PlaybackItem playbackItem;

          var items = new List<ContentItem>();
          playbackItem = new PlaybackItem {
            Id = content.Id,
            ContentItems = items
          };

          long key = content.Id;
          key <<= 32;
          key |= content.Duration * 1L;

          int index = -1;
          ContentItem firstItem = null;

          switch (content.Type) {
            case "image":
              firstItem = AddImage(content, storagePath, pair.Duration, dictFiles, files, key, index);
              break;
            case "video":
              firstItem = AddVideo(content, storagePath, dictFiles, files, key, index);
              break;
            case "html":
              firstItem = AddHtml(content, storagePath, pair.Duration, dictFiles, files, key, index);
              break;
            case "url":
              firstItem = AddUrl(content, pair.Duration);
              break;
            default:
              Response.StatusCode = 502;
              return null;
          }

          items.Add(firstItem);
          playbackItemsLocal.Add(playbackItems.Count);
          playbackItems.Add(playbackItem);
        }

        if (playbackItemsLocal.Count == 0) continue;

        Trigger trigger = new Trigger {
          Id = triggerId++,
          Type = "schedule",
          PlaybackItems = playbackItemsLocal.ToArray(),
          TriggerData = new TriggerData {
            Rrule = new Rrule {

            }
          }
        };

        if (item.Base) {
          baseTrigger = trigger;
          continue;
        }

        Dictionary<int, StEndModel> intervals = item.Intervals.ToObject<Dictionary<int, StEndModel>>();
        Rrule rrule = trigger.TriggerData.Rrule;

        if (intervals.Count > 0) {
          List<int> days = new List<int>();
          foreach (var pair in intervals) {
            days.Add(pair.Key);
          }
          StEndModel interval = intervals.Values.ElementAt(0);

          rrule.Dtstart = start.AddMinutes(interval.st);
          rrule.Until = until.AddMinutes(interval.end);
          rrule.Byweekday = days.ToArray();
        }

        triggers.Add(trigger);
      }

      if (baseTrigger != null)
        triggers.Add(baseTrigger);

      return new SynchronizeModel {
        Id = Program.Rd.Next(15, int.MaxValue),
        ContentPackage = new ContentPackage {
          PlaybackItems = playbackItems.ToArray(),
          Files = files.ToArray(),
          Triggers = triggers.ToArray()
        }
      };
    }

    private ContentItem AddImage(ContentEntity content,
      string storagePath,
      int duration,
      Dictionary<long, int> cache,
      List<FileModel> files,
      long key,
      int index = -1)
    {
      if (index == -1) {

        index = files.Count;
        cache[key] = files.Count;

        files.Add(new FileModel {
          Id = "ContetFile-" + content.Id,
          Size = content.Size,
          Md5 = content.Md5.ToLower(),
          Url = storagePath + content.Url
        });
      }

      return new ContentItem {
        Type = content.Type,
        ContentItemData = new ContentItemData {
          Duration = duration.ToString(),
        },
        Files = new int[] { index }
      };
    }

    [HttpPost("/v3/device/screenshot")]
    public async Task<IActionResult> UploadScreen(IFormFile file)
    {
      if (!Request.Headers.TryGetValue("Authorization", out StringValues oauth)) {
        return StatusCode(401);
      }

      //Oauth <token>
      string[] values = oauth[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      if (values[0].ToLower() != "oauth") {
        return BadRequest();
      }

      long pin = Convert.ToInt64(values[1].Substring(32), 16);

      PlayerEntity player = await _context.Players
        .FirstOrDefaultAsync(s => s.PinPriv == pin);

      if (player == null) {
        return NotFound();
      }

      string unix = DateTime.UtcNow.ToUnix().ToString("x"); //lower
      string guid = Guid.NewGuid().ToString("N");
      string prefix = guid[0] + "/" + guid[1] + "/" + guid[2];  // 0/a/f 
      string sufix = guid + unix; // guidunix
      string prefsuf = prefix + '/' + sufix; // 0/a/f/guidunix
      string dir = Startup.STORAGE_PATH + "/files/" + prefix;

      if (!await FileUtils.TryExistOrCreateAsync(dir))
        return StatusCode(500);

      string path = dir + "/" + sufix + ".jpg";

      using (var streamReader = new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite)) {
        await file.CopyToAsync(streamReader);
      }

      player.LastScreen = new ResponseFile {
        url = "/storage/" + prefsuf + ".jpg",
        createdAt = DateTime.UtcNow
      };

      await _context.SaveChangesAsync();

      return Ok();
    }

    [HttpPost("/v3/device/log")]
    public async Task<IActionResult> UploadLog(IFormFile file)
    {
      if (!Request.Headers.TryGetValue("Authorization", out StringValues oauth)) {
        return StatusCode(401);
      }

      //Oauth <token>
      string[] values = oauth[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      if (values[0].ToLower() != "oauth") {
        return BadRequest();
      }

      long pin = Convert.ToInt64(values[1].Substring(32), 16);

      PlayerEntity player = await _context.Players
        .FirstOrDefaultAsync(s => s.PinPriv == pin);

      if (player == null) {
        return NotFound();
      }

      string unix = DateTime.UtcNow.ToUnix().ToString("x"); //lower
      string guid = Guid.NewGuid().ToString("N");
      string prefix = guid[0] + "/" + guid[1] + "/" + guid[2];  // 0/a/f 
      string sufix = guid + unix; // guidunix
      string prefsuf = prefix + '/' + sufix; // 0/a/f/guidunix
      string dir = Startup.STORAGE_PATH + "/files/" + prefix;

      if (!await FileUtils.TryExistOrCreateAsync(dir))
        return StatusCode(500);

      string path = dir + "/" + sufix + ".zip";

      using (var streamReader = new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite)) {
        await file.CopyToAsync(streamReader);
      }

      player.LastLog = new ResponseFile {
        url = "/storage/" + prefsuf + ".zip",
        createdAt = DateTime.UtcNow
      };

      await _context.SaveChangesAsync();

      return Ok();
    }

    private ContentItem AddUrl(ContentEntity content, int duration)
    {
      return new ContentItem {
        Type = "html",
        ContentItemData = new ContentItemData {
          Url = content.Url,
          Duration = duration.ToString(),
        }
      };
    }

    private ContentItem AddHtml(ContentEntity content,
      string storagePath,
      int duration,
      Dictionary<long, int> cache,
      List<FileModel> files,
      long key,
      int index = -1)
    {
      if (index == -1) {

        index = files.Count;
        cache[key] = files.Count;

        files.Add(new FileModel {
          Id = "ContetFile-" + content.Id,
          Size = content.Size,
          Md5 = content.Md5.ToLower(),
          Url = storagePath + content.Url
        });
      }

      return new ContentItem {
        Type = content.Type,
        ContentItemData = new ContentItemData {
          Duration = duration.ToString(),
        },
        Files = new int[] { index }
      };
    }

    private ContentItem AddVideo(ContentEntity content,
      string storagePath,
      Dictionary<long, int> cache,
      List<FileModel> files,
      long key,
      int index = -1)
    {

      if (index == -1) {

        index = files.Count;
        cache[key] = files.Count;

        files.Add(new FileModel {
          Id = "ContetFile-" + content.Id,
          Size = content.Size,
          Md5 = content.Md5.ToLower(),
          Url = storagePath + content.Url,
          Data = new FileData {
            Duration = content.Duration
          }
        });
      }

      return new ContentItem {
        Type = content.Type,
        ContentItemData = new ContentItemData {
          Duration = content.Duration.ToString(),
        },
        Files = new int[] { index }
      };
    }


    #region v3

    [HttpGet("/v3/device/data")]
    public async Task<SyncModel> DataV3()
    {
      if (!Request.Headers.TryGetValue("Authorization", out StringValues oauth)) {
        Response.StatusCode = 401;
        return null;
      }

      //Oauth <token>
      string[] values = oauth[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      if (values[0].ToLower() != "oauth") {
        Response.StatusCode = 400;
        return null;
      }

      long pin = Convert.ToInt64(values[1].Substring(32), 16);

      PlayerEntity player = await _context.Players
        .AsNoTracking()
        .Include(s => s.Calendar)
        .FirstOrDefaultAsync(s => s.PinPriv == pin);

      if (player == null) {
        Response.StatusCode = 404;
        return null;
      }

      SyncModel model = new SyncModel {
        id = Program.Rd.Next(15, int.MaxValue),
        configuration = new Configuration {
          orientation = player.Landscape ? "landspace" : "portrait",
          //orientation = device.Orientation == OrientationScreen.Landspace ? "landspace" : "portrait",
          timezone = player.Timezone
        },
        content_package = new Content_Package()
      };

      if (player.Calendar == null) {
        return model;
      }

      CalendarPlaylistEntity[] events = await _context.CalendarPlaylist
        .AsNoTracking()
        .Where(s => s.CalendarId == player.Calendar.Id)
        .Include(s => s.Playlist)
          .ThenInclude(s => s.PlaylistContents)
            .ThenInclude(s => s.Content)
        .ToArrayAsync();

      string storagePath = $"{Request.Scheme}://{Request.Host}/";
      var triggers = new List<TriggerV3>(); //null, null
      int triggerId = 0;
      DateTime start = DateTime.UtcNow.Date;
      DateTime until = start.AddYears(100);
      TriggerV3 baseTrigger = null;

      foreach (var item in events) {
        PlaylistContentEntity[] playlistContents = item.Playlist.PlaylistContents.ToArray();
        int playlistId = item.Playlist.Id;

        var playbackItemsLocal = new List<Playback_Item>();

        foreach (var pair in playlistContents) {

          ContentEntity content = pair.Content;

          Playback_Item playbackItem;

          var items = new List<Content_Item>();

          Content_Item firstItem = null;

          switch (content.Type) {
            case "image":
              firstItem = Image(content, storagePath, pair.Duration);
              break;
            case "video":
              firstItem = Video(content, storagePath);
              break;
            case "html":
              firstItem = Html(content, storagePath, pair.Duration);
              break;
            case "url":
              firstItem = AddUrlV3(content, pair.Duration);
              break;
            default:
              Response.StatusCode = 502;
              return null;
          }

          items.Add(firstItem);
          playbackItem = new Playback_Item {
            id = content.Id,
            content_items = items.ToArray()
          };
          playbackItemsLocal.Add(playbackItem);
        }

        if (playbackItemsLocal.Count == 0) continue;


        var trigger = new TriggerV3 {
          data = new TriggerData {
            Rrule = new Rrule {
            }
          },

          id = triggerId++,
          type = "schedule",
          playback_items = playbackItemsLocal.ToArray() // null null null
        };

        if (item.Base) {
          baseTrigger = trigger;
          continue;
        }

        Dictionary<int, StEndModel> intervals = item.Intervals.ToObject<Dictionary<int, StEndModel>>();
        Rrule rrule = trigger.data.Rrule;

        if (intervals.Count > 0) {
          List<int> days = new List<int>();
          foreach (var pair in intervals) {
            days.Add(pair.Key);
          }
          StEndModel interval = intervals.Values.ElementAt(0);

          rrule.Dtstart = start.AddMinutes(interval.st);
          rrule.Until = until.AddMinutes(interval.end);
          rrule.Byweekday = days.ToArray();
        }

        triggers.Add(trigger);
      }

      if (baseTrigger != null)
        triggers.Add(baseTrigger);

      model.content_package.triggers = triggers.ToArray();

      return model;
    }

    public Content_Item Image(ContentEntity content, string storagePath, int duration)
    {
      return new Content_Item {
        type = content.Type,
        content_item_data = new Content_Item_Data {

          default_duration = duration,
          duration = content.Duration,
          //orientation = content.Orientation == OrientationScreen.Landspace ? "lanspace" : "portrait",
        },
        files = new File[]
                 {
                  new File
                  {
                    id = "ContetFile-" + content.Id,
                    size = content.Size,
                    md5 = content.Md5.ToLower(),
                    url = storagePath + content.Url,
                    data = new Files_Data
                    {
                      width = content.Width,
                      height = content.Height
                    }
                   }
                 }

      };
    }

    public Content_Item AddUrlV3(ContentEntity content, int duration)
    {
      return new Content_Item {
        type = "html",
        content_item_data = new Content_Item_Data {
          url = content.Url,
          default_duration = 5,
          duration = duration,
          //orientation = content.Orientation == OrientationScreen.Landspace ? "lanspace" : "portrait",
          context = "fullscreen"
        }
      };
    }

    public Content_Item Html(ContentEntity content, string storagePath, int duration)
    {
      return new Content_Item {
        type = content.Type,
        content_item_data = new Content_Item_Data {

          default_duration = 5,
          duration = duration,
          //orientation = content.Orientation == OrientationScreen.Landspace ? "lanspace" : "portrait",
          context = "fullscreen"
        },
        files = new File[]
                 {
                  new File
                  {
                    id = "ContetFile-" + content.Id,
                    size = content.Size,
                    md5 = content.Md5.ToLower(),
                    url = storagePath + content.Url,
                  }
                 }

      };
    }

    public Content_Item Video(ContentEntity content, string storagePath)
    {
      return new Content_Item {
        type = content.Type,
        content_item_data = new Content_Item_Data {
          duration = content.Duration,
          //orientation = content.Orientation == OrientationScreen.Landspace ? "lanspace" : "portrait"
        },
        files = new File[]
        {
                  new File
                  {
                    id = "ContetFile-" + content.Id,
                    size = content.Size,
                    md5 = content.Md5.ToLower(),
                    url = storagePath + content.Url,
                    data = new Files_Data
                    {
                      duration = content.Duration,
                      width = content.Width,
                      height = content.Height
                    }
                  }
                 }

      };
    }

    #endregion
  }

  public class ContentPackage
  {
    [JsonProperty("triggers")]
    public Trigger[] Triggers { get; set; } = new Trigger[0]; // null

    [JsonProperty("fillers")]
    public int[] Fillers { get; set; } = new int[0];

    [JsonProperty("playback_items")]
    public PlaybackItem[] PlaybackItems { get; set; } = new PlaybackItem[0];

    [JsonProperty("audio_playback_items")]
    public PlaybackItem[] Audios { get; set; } = new PlaybackItem[0];

    [JsonProperty("files")]
    public FileModel[] Files { get; set; } = new FileModel[0];
  }

  public class PlaybackItem
  {
    [JsonProperty("content_items")]
    public List<ContentItem> ContentItems { get; set; }

    [JsonProperty("duration")]
    public double Duration { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("play_audio")]
    public bool PlayAudio { get; set; }
  }

  public class Filler
  {
    [JsonProperty("programmatic")]
    public bool Programmatic { get; set; }

    [JsonProperty("programmatic_platform")]
    public string ProgrammaticPlatform { get; set; } = "";

    //[JsonProperty("programmatic_media_id")]
    //[JsonConverter(typeof(MediaIdConverter))]
    //public string ProgrammaticMediaId { get; set; }
  }

  public class Trigger : TriggerBase
  {
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("data")]
    public TriggerData TriggerData { get; set; }

    [JsonProperty("playback_items")]
    public int[] PlaybackItems { get; set; }

    [JsonProperty("audio_playback_items")]
    public int[] AudioPlaybackItem { get; set; }
  }

  public class TriggerData
  {
    [JsonProperty("rrule")]
    public Rrule Rrule { get; set; }

    [JsonProperty("events")]
    public Event[] Events { get; set; }
  }

  public class Event
  {
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("data")]
    public string Data { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
  }

  public class Rrule
  {
    [JsonProperty("dtstart")]
    public DateTime? Dtstart { get; set; }

    [JsonProperty("until")]
    public DateTime? Until { get; set; }

    [JsonProperty("interval")]
    public int? Interval { get; set; }

    [JsonProperty("count")]
    public int? Count { get; set; }

    [JsonProperty("byhour")]
    public int[] Byhour { get; set; }

    [JsonProperty("byweekday")]
    public int[] Byweekday { get; set; }

    [JsonProperty("byweekno")]
    public int[] Byweekno { get; set; }

    [JsonProperty("byyearday")]
    public int[] Byyearday { get; set; }

    [JsonProperty("bymonthday")]
    public int[] Bymonthday { get; set; }

    [JsonProperty("bymonth")]
    public int[] Bymonth { get; set; }

    [JsonIgnore]
    public bool Today { get; set; }

    [JsonIgnore]
    public int Increment { get; set; }

    [JsonIgnore]
    public DateTime NextTime { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool IsEnableNextTime { get; set; } = false;
  }

  public class ContentItem
  {
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("content_item_data")]
    public ContentItemData ContentItemData { get; set; }

    [JsonProperty("playback_item_data")]
    public PlaybackItemData PlaybackItemData { get; set; }

    [JsonProperty("files")]
    public int[] Files { get; set; }
  }

  public class ContentItemData
  {
    [JsonProperty("duration")]
    public string Duration { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("actions")]
    public JToken Actions { get; set; }

    [JsonProperty("data")]
    public JToken Data { get; set; }

    [JsonProperty("fillers")]
    public int[] Fillers { get; set; }

    [JsonProperty("play_audio")]
    public bool PlayAudio { get; set; }

    [JsonProperty("position")]
    public int[] Position { get; set; }
  }

  public class PlaybackItemData
  {
    [JsonProperty("position")]
    public Position Position { get; set; }
  }

  public class FileModel
  {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("size")]
    public long? Size { get; set; }

    [JsonProperty("md5")]
    public string Md5 { get; set; }

    [JsonProperty("data")]
    public FileData Data;

    [JsonProperty("url")]
    public string Url { get; set; }
  }

  public class FileData
  {
    [JsonProperty("duration")]
    public double Duration { get; set; }
  }

  public class Position
  {
    [JsonProperty("x")]
    public int X { get; set; }

    [JsonProperty("y")]
    public int Y { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }
  }

  public class TriggerBase
  {
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("audios")]
    public Audio[] Audios { get; set; }

    [JsonIgnore]
    public LinkedList<Uri> AudioItems { get; set; } = new LinkedList<Uri>();
  }

  public class External : TriggerBase
  {
    [JsonProperty("events")]
    public Event[] Events { get; set; }
  }

  public class Audio
  {
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("index")]
    public int Index { get; set; }
  }

  public class Schedule : TriggerBase
  {
    [JsonProperty("rrule")]
    public Rrule Rrule { get; set; }
  }

  public class SynchronizeModel
  {
    [JsonRequired]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("settings")]
    public SettingsModel Settings { get; set; }

    [JsonProperty("content_package")]
    public ContentPackage ContentPackage { get; set; }
  }

  public class SyncModel
  {
    public Configuration configuration { get; set; } //configuration
    public int id { get; set; }
    public Content_Package content_package { get; set; }
  }

  public class Configuration
  {
    public string timezone { get; set; }
    public string orientation { get; set; }
  }

  public class Content_Package
  {
    public TriggerV3[] triggers { get; set; }
  }

  public class Playback_Item
  {
    public Data_Orientation data { get; set; }
    public int id { get; set; }
    public Content_Item[] content_items { get; set; }
  }

  public class Data_Orientation
  {
    public string orientation { get; set; }
  }

  public class Content_Item
  {
    public string type { get; set; }
    public File[] files { get; set; } = new File[0];
    public Playback_Item_Data playback_item_data { get; set; }
    public Content_Item_Data content_item_data { get; set; }
  }

  public class Playback_Item_Data
  {
    public Position position { get; set; }
  }

  public class Content_Item_Data
  {
    public string orientation { get; set; }
    public int duration { get; set; }
    public string context { get; set; }
    public string url { get; set; }
    public int default_duration { get; set; }
  }

  public class File
  {
    public Files_Data data { get; set; }
    public string id { get; set; }
    public bool blank { get; set; }
    public long size { get; set; }
    public string url { get; set; }
    public string md5 { get; set; }
  }

  public class Files_Data
  {
    public int width { get; set; }
    public int height { get; set; }
    //public int? framerate { get; set; }
    public float? duration { get; set; }
    //public int? frame_count { get; set; }
  }

  public class TriggerV3
  {
    public string type { get; set; }
    public int id { get; set; }
    public Playback_Item[] playback_items { get; set; }
    public TriggerData data { get; set; }
  }
}