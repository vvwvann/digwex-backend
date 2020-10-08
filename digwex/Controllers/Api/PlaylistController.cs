using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Digwex.Data;
using Digwex.Services;

namespace Digwex.Controllers.Api
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  [Produces("application/json")]
  public class PlaylistController : DefaultController
  {
    private readonly DeviceService _service;

    public PlaylistController(ApplicationDbContext context, DeviceService service) : base(context)
    {
      _service = service;
    }

    [HttpGet("/api/[controller]s")]
    public async Task<PlaylistEntity[]> All()
    {
      var user = await UserAsync();
      return await _context.Playlists
        .Where(s => s.User == user)
        .OrderByDescending(s => s.AddTime)
        .ToArrayAsync();
    }

    [HttpGet("{id}")]
    public async Task<PlaylistEntity> Details([FromRoute] int id)
    {
      var user = await UserAsync();
      PlaylistEntity playlist = await _context.Playlists.SingleOrDefaultAsync(s => s.User == user && s.Id == id);

      if (playlist == null) {
        Response.StatusCode = 404;
        return null;
      }

      return playlist;
    }

    /// <summary>
    /// Сохранение контента плейлиста
    /// </summary>
    [HttpPost("{id}/contents")]
    public async Task<IActionResult> AddContent([FromRoute] int id, [FromBody] AddContentModel[] contents)
    {
      var user = await UserAsync();
      List<PlaylistContentEntity> playlistContent = await _context.PlaylistContent
        .Where(s => s.User == user && s.PlaylistId == id)
        .ToListAsync();

      if (playlistContent.Count > 0) {
        _context.RemoveRange(playlistContent);
      }

      playlistContent = new List<PlaylistContentEntity>();

      int index = 0;
      foreach (var item in contents) {
        playlistContent.Add(new PlaylistContentEntity {
          Index = index++,
          ContentId = item.Id,
          Duration = item.Duration,
          PlaylistId = id,
          User = user,
        });
      }

      _context.AddRange(playlistContent);

      await _context.SaveChangesAsync();

      await AddPlayersToSync(id);

      return Ok();
    }

    //[HttpDelete("{playlistId}/content/{contentId}")]
    //public async Task<IActionResult> DeleteContent([FromRoute] int playlistId, [FromRoute] int contentId)
    //{
    //  PlaylistContentEntity item = await _context.PlaylistContent.FirstOrDefaultAsync(s => s.PlaylistId == playlistId && s.ContentId == contentId);

    //  if (item == null) return NoContent();
    //  _context.Remove(item);
    //  await _context.SaveChangesAsync();
    //  return Ok();
    //}

    [HttpGet("{id}/contents")]
    public async Task<List<ContentEntity>> Contentns([FromRoute] int id)
    {
      var user = await UserAsync();
      List<PlaylistContentEntity> playlistContent = await _context.PlaylistContent
        .Where(s => s.User == user && s.PlaylistId == id)
        .Include(c => c.Content)
        .ToListAsync();

      if (playlistContent == null) {
        Response.StatusCode = 404;
        return null;
      }

      if (playlistContent.Count == 0) return new List<ContentEntity>();

      playlistContent.Sort((x, y) => {
        if (x.Index > y.Index) return 1;
        else if (x.Index == y.Index) return 0;
        return -1;
      });

      var contents = new List<ContentEntity>();

      foreach (var item in playlistContent) {
        ContentEntity content = item.Content;
        contents.Add(new ContentEntity {
          Id = content.Id,
          Duration = item.Duration,
          Url = content.Url,
          LargeThumb = content.LargeThumb,
          SmallThumb = content.SmallThumb,
          Name = content.Name,
          Type = content.Type
        });
      }

      return contents;
    }

    [HttpPost("add")]
    public async Task<PlaylistEntity> Add(AddPlaylistModel model)
    {

      var playlist = new PlaylistEntity {
        Name = model.name,
        Color = model.color,
        User = await UserAsync()
      };

      _context.Add(playlist);
      await _context.SaveChangesAsync();


      return playlist;
    }

    [HttpPost("{id}/copy")]
    public async Task<PlaylistEntity> Copy([FromRoute] int id, [FromBody] AddPlaylistModel model)
    {
      var user = await UserAsync();
      List<PlaylistContentEntity> contents = await _context.PlaylistContent
        .Where(s => s.User == user && s.PlaylistId == id)
        .AsNoTracking()
        .ToListAsync();

      var playlist = new PlaylistEntity {
        Name = model.name,
        Color = model.color,
        PlaylistContents = contents,
        User = user
      };

      foreach (var item in contents) {
        item.Playlist = playlist;
        item.PlaylistId = 0;
      }

      _context.Add(playlist);
      await _context.SaveChangesAsync();

      return playlist;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Rename([FromRoute] int id, [FromBody] AddPlaylistModel model)
    {
      var user = await UserAsync();
      PlaylistEntity entity = await _context.Playlists.FirstOrDefaultAsync(s => s.User == user && s.Id == id);
      if (entity == null) return NotFound();
      entity.Name = model.name;
      entity.Color = model.color;
      await _context.SaveChangesAsync();
      return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
      _context.Remove(new PlaylistEntity { Id = id });

      await _context.SaveChangesAsync();
      await AddPlayersToSync(id);
      return Ok();
    }

    private async Task AddPlayersToSync(int playlistId)
    {
      var user = await UserAsync();
      var calendars = await _context.CalendarPlaylist.Where(s => s.User == user && s.PlaylistId == playlistId)
        .AsNoTracking()
        .Include(s => s.Calendar)
        .ThenInclude(s => s.Players)
        .Select(s => s.Calendar)
        .ToArrayAsync();

      foreach (var item in calendars) {
        if (item.Players != null) {
          foreach (var player in item.Players) {
            _service.AddToSync(player.Id);
          }
        }
      }
    }


    // Models - hello swaggers:)
    public class AddPlaylistModel
    {
      public string name { get; set; }

      public string color { get; set; }
    }

    public class AddContentModel
    {
      /// <summary>
      /// Id добавляемого контента
      /// </summary>
      [JsonRequired]
      public int Id { get; set; }

      /// <summary>
      /// Значение в секундах. Этот параметр нужен для контента, в которого есть возможность настраивать время проигрывания изображения, html.
      /// </summary>
      public int Duration { get; set; }
    }
  }
}
