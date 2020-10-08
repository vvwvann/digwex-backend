using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Digwex.Data;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using System;
using Digwex.Services;

namespace Digwex.Controllers.Api
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  [Produces("application/json")]
  public class CalendarController : DefaultController
  {
    private readonly DeviceService _service;

    public CalendarController(ApplicationDbContext context, DeviceService service) : base(context)
    {
      _service = service;
    }

    [HttpGet("/api/[controller]s")]
    public async Task<CalendarEntity[]> All()
    {
      var user = await UserAsync();
      return await _context.Calendars.Where(s => s.User == user)
        //.OrderByDescending(s => s.AddTime)
        .ToArrayAsync();
    }

    [HttpGet("{id}")]
    public async Task<CalendarEntity> Detail([FromRoute] int id)
    {
      var user = await UserAsync();
      CalendarEntity calendar = await _context.Calendars
        .Include(s => s.Players)
        .SingleOrDefaultAsync(s => s.User == user && s.Id == id);

      if (calendar == null) {
        Response.StatusCode = 404;
        return null;
      }

      if (calendar.Players != null) {
        foreach (var item in calendar.Players) {
          item.Calendar = null;
        }
      }

      CalendarPlaylistEntity[] all = await _context.CalendarPlaylist
        .Where(s => s.CalendarId == id)
        .Include(s => s.Playlist)
        .ToArrayAsync();


      var result = new List<PlaylistEntity>();

      foreach (var item in all) {

        result.Add(new PlaylistEntity {
          Id = item.PlaylistId,
          Name = item.Playlist.Name,
          Guid = item.Guid,
          Base = item.Base,
          AddTime = item.Playlist.AddTime,
          Intervals = item.Intervals,
          User = user
        });
      }

      calendar.Playlists = result;

      return calendar;
    }

    /// <summary>
    /// Добавление календаря
    /// </summary>
    [HttpPost]
    public async Task<CalendarEntity> Create([FromBody] AddCalendarModel model)
    {
      var entity = new CalendarEntity {
        Name = model.name,
        User = await UserAsync()
      };
      _context.Calendars.Add(entity);
      await _context.SaveChangesAsync();
      return entity;
    }

    /// <summary>
    /// Удаление календаря
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
      var user = await UserAsync();
      CalendarEntity entity = await _context.Calendars
        .Include(s => s.Players)
        .SingleOrDefaultAsync(s => s.User == user && s.Id == id);

      if (entity == null) return NotFound();

      CalendarPlaylistEntity[] playlistRelation = await _context.CalendarPlaylist
        .Where(s => s.CalendarId == id)
        .ToArrayAsync();

      _context.RemoveRange(playlistRelation);
      _context.Calendars.Remove(entity);

      await _context.SaveChangesAsync();

      if (entity.Players != null) {
        foreach (var item in entity.Players) {
          _service.AddToSync(item.Id);
        }
      }

      return Ok();
    }

    /// <summary>
    /// Добавление плееров в календарь
    /// </summary>
    /// <remarks>Нужно передавать массив, содержащий id добавляемых плееров, даже если добавляется только 1 плеер.</remarks>
    [HttpPost("{calendarId}/players")]
    public async Task<IActionResult> AddPlayer([FromRoute] int calendarId, [FromBody] int[] players)
    {
      var user = await UserAsync();
      CalendarEntity calendarEntity = await _context.Calendars.SingleOrDefaultAsync(s => s.User == user && s.Id == calendarId);

      if (calendarEntity == null)
        return NotFound();

      int[] ids = await _context.Players.Where(s => s.Calendar == calendarEntity).Select(s => s.Id).ToArrayAsync();

      var set = new HashSet<int>(ids);

      foreach (int id in players) {
        PlayerEntity playerEntity = await _context.Players.FirstOrDefaultAsync(s => s.Id == id);
        if (playerEntity == null) {
          continue;
        }
        set.Add(id);
        playerEntity.Calendar = calendarEntity;
        playerEntity.User = user;
      }
      await _context.SaveChangesAsync();


      foreach (var id in set) {
        _service.AddToSync(id);
      }

      return Ok();
    }

    /// <summary>
    /// Удаление плеера с календаря
    /// </summary>
    [HttpDelete("{calendarId}/player/{playerId}")]
    public async Task<IActionResult> DeletePlayer([FromRoute] int calendarId, [FromRoute] int playerId)
    {
      var user = await UserAsync();
      CalendarEntity calendarEntity = await _context.Calendars.SingleOrDefaultAsync(s => s.User == user && s.Id == calendarId);

      if (calendarEntity == null)
        return NotFound();

      PlayerEntity playerEntity = await _context.Players.SingleOrDefaultAsync(s => s.User == user && s.Id == playerId);

      if (playerEntity == null)
        return NotFound();

      playerEntity.Calendar = null;

      await _context.SaveChangesAsync();

      _service.AddToSync(playerId);

      return Ok();
    }

    /// <summary>
    /// Добавление плейлиста
    /// </summary>
    /// <remarks>
    /// Если в запросе нету тела, тогда это запрос на добавление базового плейлиста. В json ключ - это день в **ISO** стандарте, 0 - воскресенье, 6- суббота, например:
    ///
    ///```
    ///{
    ///  "0": {
    ///    "st": 100,
    ///    "end": 450
    ///  },
    ///  "1": {
    ///    "st": 20,
    ///    "end": 170
    ///  }
    ///}
    ///```
    ///
    /// Важный момент в предлах одного дня не должно быть пересечний между парами `st` и `end`
    ///
    /// </remarks>
    [HttpPost("{calendarId}/playlist/{playlistId}")]
    public async Task<CalendarPlaylistEntity> AddPlaylist(
      [FromRoute] int calendarId,
      [FromRoute] int playlistId,
      [FromBody] Dictionary<int, StEndModel> intervals = null)
    {

      var user = await UserAsync();
      CalendarEntity calendar = await _context.Calendars
        .Include(s => s.Players)
        .SingleOrDefaultAsync(s => s.User == user && s.Id == calendarId);

      if (calendar == null) {
        Response.StatusCode = 400;
        return null;
      }

      PlaylistEntity playlist = await _context.Playlists.SingleOrDefaultAsync(s => s.User == user && s.Id == playlistId);

      if (playlist == null) {
        Response.StatusCode = 400;
        return null;
      }

      if (intervals == null || intervals.Count == 0)
        return await AddBasePlaylist(calendar, playlistId);

      //CalendarPlaylistEntity bos = await _context.CalendarPlaylist
      //  .Where(s => s.CalendarId == calendarId && s.PlaylistId == playlistId && !s.Base)
      //  .SingleOrDefaultAsync();

      var bos = new CalendarPlaylistEntity {
        CalendarId = calendarId,
        PlaylistId = playlistId,
        Intervals = JToken.FromObject(intervals),
        User = user
      };
      _context.Add(bos);

      await _context.SaveChangesAsync();

      if (calendar.Players != null) {
        foreach (var item in calendar.Players) {
          _service.AddToSync(item.Id);
        }
      }

      if (bos.Calendar != null) {
        bos.Calendar.Players = null;
      }

      return bos;
    }


    /// <summary>
    /// Изменение не базового плейлиста в календаре
    /// </summary>
    /// <remarks>
    /// Если в запросе нету тела, тогда это запрос на добавление базового плейлиста. В json ключ - это день в **ISO** стандарте, 0 - воскресенье, 6- суббота, например:
    ///
    ///```
    ///{
    ///  "0": {
    ///    "st": 100,
    ///    "end": 450
    ///  },
    ///  "1": {
    ///    "st": 20,
    ///    "end": 170
    ///  }
    ///}
    ///```
    ///
    /// Важный момент в предлах одного дня не должно быть пересечний между парами `st` и `end`
    ///
    /// </remarks>
    /// <param name="calendarId">Id календаря</param>
    /// <param name="guid">Guid элемента в календаре</param>
    [HttpPut("{calendarId}/playlist/{guid}")]
    public async Task<IActionResult> UpdatePlaylist(
      [FromRoute] int calendarId,
      [FromRoute] Guid guid,
      [FromBody] EditPlaylistRequest model)
    {
      var user = await UserAsync();
      CalendarPlaylistEntity entity = await _context.CalendarPlaylist
        .Include(s => s.Calendar)
      .Where(s => s.User == user && s.CalendarId == calendarId && s.Guid == guid)
      .SingleOrDefaultAsync();

      if (entity == null) return BadRequest();

      if (model.playlistId != null && entity.PlaylistId != model.playlistId) {
        entity.PlaylistId = model.playlistId.Value;
      }

      entity.Intervals = JToken.FromObject(model.Intervals);

      await _context.SaveChangesAsync();

      int[] ids = entity.Calendar.Players?.Select(s => s.Id).ToArray() ?? new int[0];

      foreach (var id in ids) {
        _service.AddToSync(id);
      }

      return Ok();
    }


    /// <summary>
    /// Удаление плейлиста с календаря
    /// </summary>
    [HttpDelete("{calendarId}/playlist/{guid}")]
    public async Task<IActionResult> DeletePlaylist([FromRoute] int calendarId, [FromRoute] Guid guid)
    {
      var user = await UserAsync();
      CalendarPlaylistEntity entity = await _context.CalendarPlaylist
        .Where(s => s.User == user && s.CalendarId == calendarId && s.Guid == guid)
        .Include(s => s.Calendar)
          .ThenInclude(s => s.Players)
        .SingleOrDefaultAsync();

      if (entity == null) return NotFound();

      int[] ids = entity.Calendar.Players?.Select(s => s.Id).ToArray() ?? new int[0];

      _context.Remove(entity);

      await _context.SaveChangesAsync();

      foreach (var id in ids) {
        _service.AddToSync(id);
      }

      return Ok();
    }

    [HttpPut("{id}/rename")]
    public async Task<IActionResult> Rename([FromRoute] int id, [FromQuery] string name)
    {
      CalendarEntity callendar = await _context.Calendars.FirstOrDefaultAsync(s => s.Id == id);
      if (callendar == null) return NotFound();
      callendar.Name = name;
      await _context.SaveChangesAsync();
      return Ok();
    }

    private async Task<CalendarPlaylistEntity> AddBasePlaylist(CalendarEntity calendar, int playlistId)
    {
      var user = await UserAsync();
      CalendarPlaylistEntity bos = await _context.CalendarPlaylist
        .Where(s => s.User == user && s.CalendarId == calendar.Id && s.Base == true)
        .SingleOrDefaultAsync();

      if (bos != null) {
        _context.Remove(bos);
      }

      bos = new CalendarPlaylistEntity {
        CalendarId = calendar.Id,
        PlaylistId = playlistId,
        Base = true,
        Calendar = null,
        User = user
      };

      _context.Add(bos);

      await _context.SaveChangesAsync();

      if (calendar.Players != null) {
        foreach (var item in calendar.Players) {
          _service.AddToSync(item.Id);
        }
      }

      if (bos.Calendar != null) {
        bos.Calendar.Players = null;
      }

      return bos;
    }
  }

  public class AddPlModel
  {
    public int playlistId { get; set; }

    /// <summary>
    /// Ключ это день в ISO стандарте, 0 - воскресенье, 6- суббота. Значение содержит "с" и "до" в минутах относительно дня
    /// </summary>
    public Dictionary<int, StEndModel> Intervals { get; set; }
  }

  public class StEndModel
  {
    /// <summary>
    /// Значение э минутах относительно дня
    /// </summary>
    [Required]
    public int st { get; set; }

    /// <summary>
    /// Значение э минутах относительно дня
    /// </summary>
    [Required]
    public int end { get; set; }
  }

  // Models - hello swaggers:)

  public class AddCalendarModel
  {
    [Required]
    public string name { get; set; }
  }

  public class EditPlaylistRequest
  {
    /// <summary>
    /// Можно использовать при необходимости заменить плейлист
    /// </summary>
    public int? playlistId { get; set; }

    /// <summary>
    /// Ключ это день в ISO стандарте, 0 - понедельник, 6- воскресенье. Значение содержит "с" и "до" в минутах относительно дня
    /// </summary>
    [Required]
    public Dictionary<int, StEndModel> Intervals { get; set; }
  }
}
