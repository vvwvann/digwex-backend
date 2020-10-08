using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Digwex.Data;
using Digwex.Services;

namespace Digwex.Controllers.Api
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  public class PlayerController : DefaultController
  {
    //private readonly DeviceService _deviceManager;
    private readonly WebSocketService _ws;
    private readonly DeviceService _service;

    public PlayerController(ApplicationDbContext context, DeviceService service, WebSocketService ws) : base(context)
    {
      //_deviceManager = deviceManager;
      _ws = ws;
      _service = service;
    }

    // GET: Devices
    [HttpGet("/api/players")]
    public async Task<PlayerEntity[]> All()
    {
      var user = await UserAsync();
      PlayerEntity[] players = await _context.Players
        .Where(s => s.User == user)
        .Include(s => s.Calendar)
        .OrderByDescending(s => s.AddTime)
        .ToArrayAsync();

      foreach (var item in players) {
        item.Online = _ws.Get(item.Id) != null;
        item.LastOnlineP = item.LastOnline;
        if (item.Online) {
          item.LastOnlineP = null;
        }
        if (item.Calendar != null) {
          item.Calendar.Players = null;
        }
      }

      return players;
    }

    [HttpGet("{id}")]
    public async Task<PlayerEntity> Player([FromRoute]int id)
    {
      var user = await UserAsync();
      PlayerEntity player = await _context.Players
        .Include(s => s.Calendar)
        .SingleOrDefaultAsync(s => s.User == user && s.Id == id);

      if (player == null) {
        Response.StatusCode = 404;
        return null;
      }

      if (player.Calendar != null) {
        player.Calendar.Players = null;
      }

      player.Online = _ws.Get(id) != null;

      player.LastOnlineP = player.LastOnline;
      if (player.Online) {
        player.LastOnlineP = null;
      }
      return player;
    }

    [HttpPost("add")]
    public async Task<PlayerEntity> Add([FromBody] AddPlayerModel model)
    {
      var user = await UserAsync();
      int calendId = model.calendarId ?? 0;

      CalendarEntity calendarEntity = null;

      if (calendId > 0) {
        calendarEntity = await _context.Calendars.SingleOrDefaultAsync(s => s.User == user && s.Id == calendId);

        if (calendarEntity == null) {
          Response.StatusCode = 400;
          return null;
        }
      }

      var player = new PlayerEntity {
        Name = model.name,
        Landscape = true,//model.landscape,
        Calendar = calendarEntity,
        Timezone = model.timezone,
        User = user
      };

      while (true) {
        Generate(player);
        _context.Add(player);

        try {
          await _context.SaveChangesAsync();
          break;
        }
        catch (DbUpdateException ex) {
          //var sqlException = ex.InnerException as System.Data.SqlClient.SqlException;
          //if (sqlException.Number == 2601) {
          //  _context.Remove(deviceEntity);
          //}
        }
      }

      player.Calendar = calendarEntity;
      await _context.SaveChangesAsync();

      if (calendarEntity != null) {
        calendarEntity.Players = null;
        calendarEntity.Playlists = null;
      }

      return player;
    }

    [HttpPost("{id}/edit")]
    public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] EditPlayerRequest model)
    {
      var user = await UserAsync();
      PlayerEntity device = await _context.Players
       .SingleOrDefaultAsync(s => s.User == user && s.Id == id);

      if (device == null)
        return NotFound();

      CalendarEntity calendarEntity = null;
      int calendarId = model.CalendarId ?? 0;

      if (calendarId > 0) {
        calendarEntity = await _context.Calendars.SingleOrDefaultAsync(s => s.User == user && s.Id == calendarId);

        if (calendarEntity == null) {
          Response.StatusCode = 400;
          return null;
        }
      }

      device.Calendar = calendarEntity;
      device.Timezone = model.Timezone;
      device.Name = model.Name;

      await _context.SaveChangesAsync();

      _service.AddToSync(id);

      return Ok();
    }

    [HttpPost("{id}/clone")]
    public async Task<IActionResult> Clone([FromRoute] int id, [FromBody]PlayerEntity clone)
    {
      var user = await UserAsync();
      PlayerEntity player = await _context.Players
       .AsNoTracking()
       .Include(s => s.Calendar)
       .FirstOrDefaultAsync(s => s.User == user && s.Id == id);
      if (player == null)
        return NotFound();

      JObject json = JObject.FromObject(clone);
      json.Remove("Orientation");
      JObject obj = JObject.FromObject(player);
      obj.Merge(json);
      player = obj.ToObject<PlayerEntity>();
      player.Id = 0;
      player.IsActivate = false;
      player.User = user;

      while (true) {
        Generate(player);
        _context.Add(player);

        try {
          await _context.SaveChangesAsync();
          return Ok();
        }
        catch (DbUpdateException ex) {
          _context.Remove(player);
        }
      }
    }

    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> Deactivate([FromRoute] int id)
    {
      var user = await UserAsync();
      PlayerEntity player = await _context.Players
        .FirstOrDefaultAsync(s => s.User == user && s.Id == id);

      player.IsActivate = false;
      player.Platform = "";
      player.Percent = -1;
      player.ProblemSync = false;
      player.Version = null;
      player.StatusScreen = false;

      while (true) {
        Generate(player);

        try {
          await _context.SaveChangesAsync();
          break;
        }
        catch (DbUpdateException ex) {
          //var sqlException = ex.InnerException as System.Data.SqlClient.SqlException;
          //if (sqlException.Number == 2601) {
          //  _context.Remove(deviceEntity);
          //}
          _context.Remove(player);
        }
      }

      return Ok();
    }

    [HttpPut("{id}/sync")]
    public async Task<IActionResult> Sync([FromRoute] int id)
    {
      await _service.SyncAsync(id);

      return Ok();
    }

    /// <summary>
    /// Запросить скриншот устройства
    /// </summary>
    /// <param name="playerId">Id плеера</param>
    [HttpPost("{playerId}/screenshot")]
    public async Task<IActionResult> Screenshot([FromRoute] int playerId)
    {
      PlayerEntity pl = await _context.Players.FirstOrDefaultAsync(s => s.Id == playerId);

      if (pl == null) return NotFound();

      CommandEntity command = new CommandEntity {
        Command = "take-screenshot",
      };

      await _service.SendCommandAsync(playerId, command);

      return Ok();
    }

    /// <summary>
    /// Запросить логи устройства
    /// </summary>
    /// <param name="playerId">Id плеера</param>
    [HttpPost("{playerId}/logs")]
    public async Task<IActionResult> Logs([FromRoute] int playerId)
    {
      PlayerEntity pl = await _context.Players.FirstOrDefaultAsync(s => s.Id == playerId);

      if (pl == null) return NotFound();

      CommandEntity command = new CommandEntity {
        Command = "upload-logs",
      };

      await _service.SendCommandAsync(playerId, command);

      return Ok();
    }

    /// <summary>
    /// Получить последние достпные скриншоты
    /// </summary>
    /// <param name="playerId">Id плеера</param>
    [HttpGet("{playerId}/screenshot")]
    public async Task<ResponseFile> LastScreenshot([FromRoute] int playerId)
    {
      PlayerEntity player = await _context.Players
        .FirstOrDefaultAsync(s => s.Id == playerId);
      return player.LastScreen;
    }

    /// <summary>
    /// Получить последние достпные логи
    /// </summary>
    /// <param name="playerId">Id плеера</param>
    [HttpGet("{playerId}/logs")]
    public async Task<ResponseFile> LastLogs([FromRoute] int playerId)
    {
      PlayerEntity player = await _context.Players
        .FirstOrDefaultAsync(s => s.Id == playerId);
      return player.LastLog;
    }

    [HttpPut("{playerId}/calendar/{calendarId}")]
    public async Task<IActionResult> BindeCalendar([FromRoute] int playerId, [FromRoute] int calendarId)
    {
      var user = await UserAsync();
      PlayerEntity player = await _context.Players
        .FirstOrDefaultAsync(s => s.User == user && s.Id == playerId);

      if (player == null) return NotFound();

      CalendarEntity calendar = await _context.Calendars
       .FirstOrDefaultAsync(s => s.User == user && s.Id == calendarId);

      if (calendar == null) return NotFound();


      player.Calendar = calendar;

      await _context.SaveChangesAsync();

      _service.AddToSync(player.Id);

      return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
      PlayerEntity deviceEntity = new PlayerEntity { Id = id };
      _context.Players.Remove(deviceEntity);
      await _context.SaveChangesAsync();
      return Ok();
    }

    private void Generate(PlayerEntity playerEntity)
    {
      long pin = (int)1e9 + Program.Rd.Next(1, (int)1e8);
      Guid gu1 = Guid.NewGuid();

      playerEntity.Token = $"{gu1:N}{pin:x4}";
      playerEntity.PinPriv = pin;
      char[] charArray = (pin + "").ToCharArray();
      Array.Reverse(charArray);
      playerEntity.Pin = new string(charArray);
    }
  }


  // Models - hello swaggers:)

  public class ResponseFile
  {
    public DateTime createdAt { get; set; }

    public string url { get; set; }
  }

  public class AddPlayerModel
  {
    [Required]
    public string name { get; set; }
    public int? calendarId { get; set; }
    public bool landscape { get; set; } = true;
    public string address { get; set; }
    public string timezone { get; set; }
  }

  public class ConfigurationActivate
  {

    [JsonProperty("device_id")]
    public int DeviceId { get; set; }

    [JsonProperty("backend_url")]
    public string BackendUrl { get; set; }

    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("timezone")]
    public string Timezone { get; set; }
  }

  public class ActivateModel
  {

    [JsonProperty("configuration")]
    public ConfigurationActivate Configuration { get; set; }
  }

  public class ActivateReqModel
  {
    public string pin { get; set; }

    public string platform { get; set; }
  }


  public class EditPlayerRequest
  {
    [SwaggerSchema("Id календаря")]
    public int? CalendarId { get; set; }

    [SwaggerSchema("Название плеера")]
    public string Name { get; set; }

    [SwaggerSchema("Часовой пояс устройства")]
    public string Timezone { get; set; }
  }
}
