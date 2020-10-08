using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using Digwex.Data;
using Digwex.Models;

namespace Digwex.Controllers.Api
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  public class SettingsController : ControllerBase
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context,
      UserManager<ApplicationUser> userManager)
    {
      _userManager = userManager;
      _context = context;
    }

    [HttpGet]
    public async Task<DefaultSettingResponse> All()
    {
      ApplicationUser user = await CurrentUserAsync();

      SettingsEntity entity = await _context.Settings
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.UserId == user.Id);

      var res = new DefaultSettingResponse {
        Email = user.Email,
      };

      if (entity != null) {
        res.FirstWeekDay = entity.FirstWeekDay;
        res.Language = entity.Language;
        res.TimeFormat = entity.TimeFormat;
      }

      return res;
    }

    /// <summary>
    /// Редактировнаие основных настроек. Можно отправлять те данные, которые изменились
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("main")]
    public async Task<IActionResult> MainChange([FromBody] MainChangeRequest model)
    {
      ApplicationUser user = await CurrentUserAsync();

      SettingsEntity entity = await _context.Settings.FirstOrDefaultAsync(s => s.UserId == user.Id);

      if(entity == null) {
        entity = new SettingsEntity();
        await _context.AddAsync(entity);
      }

      entity.User ??= user;

      if (!string.IsNullOrEmpty(model.Language))
        entity.Language = model.Language;

      if (model.FirstWeekDay != null)
        entity.FirstWeekDay = model.FirstWeekDay.Value;

      if (model.TimeFormat != null)
        entity.TimeFormat = model.TimeFormat.Value;

      await _context.SaveChangesAsync();

      return Ok();
    }

    /// <summary>
    /// Изменить пароль текущего пользователя
    /// </summary>
    /// <response code="200">Пароль успешно изменен</response>
    /// <response code="400">Не удалось изменить пароль</response>
    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword([FromBody] PasswordRequest model)
    {
      ApplicationUser user = await CurrentUserAsync();
      //_userManager.ChangeEmailAsync(user, model.email, )
      //var result = await _userManager.ChangePasswordAsync(user, model.email, model.newP);

      //if (result.Succeeded) {
      //  return Ok();
      //}
      //else {
      //  return BadRequest(result.Errors);
      //}
      return Ok();
    }

    private Task<ApplicationUser> CurrentUserAsync()
    {
      return _context.Users.FirstOrDefaultAsync(s => s.Id == User.Identity.Name);
    }
  }

  public class DefaultSettingResponse
  {
    [MinLength(2)]
    [MaxLength(2)]
    [SwaggerSchema("Язык в формате **ISO 639-1**")]
    public string Language { get; set; } = "RU";

    [SwaggerSchema("Занчение `true` - 12-часовой формат. По умолчанию используется 24 часовой формат времени")]
    public bool? TimeFormat { get; set; }

    [Range(0, 6)]
    [SwaggerSchema("День недели `0` - понедельник, `6` - воскресенье")]
    public int? FirstWeekDay { get; set; }

    public string CompanyName { get; set; } = Startup.SetupModel?.Company?.Name;

    public string Email { get; set; }
  }

  public class PasswordRequest
  {
    [Required]
    public string email { get; set; }

    [Required]
    public string password { get; set; }
  }

  public class MainChangeRequest
  {
    [MinLength(2)]
    [MaxLength(2)]
    [SwaggerSchema("Язык в формате **ISO 639-1**")]
    public string Language { get; set; }

    [SwaggerSchema("Занчение `true` - 12-часовой формат. По умолчанию используется 24 часовой формат времени")]
    public bool? TimeFormat { get; set; }

    [Range(0, 6)]
    [SwaggerSchema("День недели `0` - понедельник, `6` - воскресенье")]
    public int? FirstWeekDay { get; set; }

    //[MaxLength(40)]
    //[SwaggerSchema("Название компании")]
    //public string CompanyName { get; set; }
  }
}