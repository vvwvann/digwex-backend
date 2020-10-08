using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Digwex.Data;
using Digwex.Models;

namespace Digwex.Controllers.Api
{
  [Route("api/[controller]")]
  [ApiController]
  public class SetupController : ControllerBase
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public SetupController(ApplicationDbContext context,
      UserManager<ApplicationUser> userManager)
    {
      _userManager = userManager;
      _context = context;
    }

    /// <summary>
    /// Получить текущею конфигурацию начальных настроек
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<SetupEntity> All()
    {
      SetupEntity entity = await _context.Setup.Include(s => s.User).FirstAsync(s => s.Id == 1);
      if (entity.Company != null && entity.User!=null) {
        entity.Company.Email = entity.User.Email;
      }

      return entity;
    }

    /// <summary>
    /// Редактировать настройки компании. Этот запрос можно игнорировать в случае если данные есть и пользователь их не редактировал
    /// </summary>
    [HttpPut("company")]
    public async Task<IActionResult> MainEdit([FromBody] SetupMainJson model)
    {
      SetupEntity entity = await _context.Setup.Include(s => s.User).FirstAsync(s => s.Id == 1);

      entity.Company ??= new CompanyJson();

      if (string.IsNullOrEmpty(model.companyName)
        || string.IsNullOrEmpty(model.password))
        return BadRequest();

      entity.Company.Name = model.companyName;
      entity.Language = model.language;


      ApplicationUser user = null;


      if (entity.User?.NormalizedEmail == _userManager.NormalizeEmail(model.email)) {
        user = entity.User;
        Console.WriteLine("use prev email");
      }

      IdentityResult result;

      if (user != null) {
        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        result = await _userManager.ResetPasswordAsync(user, token, model.password);
      }
      else {
        user = new ApplicationUser { UserName = model.email, Email = model.email, Owner = true };
        result = await _userManager.CreateAsync(user, model.password);
      }

      if (!result.Succeeded) {
        return BadRequest(result.Errors);
      }

      entity.User = user;

      await _context.SaveChangesAsync();

      return Ok();
    }

    /// <summary>
    /// Редактировать настройки сервера. Этот запрос можно игнорировать в случае если данные есть и пользователь их не редактировал
    /// </summary>
    [HttpPut("server")]
    public async Task<IActionResult> ServerEdit([FromBody] SetupServerJson model)
    {
      SetupEntity entity = await _context.Setup.FirstAsync(s => s.Id == 1);

      ServerJson json = entity.Server;

      json ??= new ServerJson();

      json.Timezone = model.timezone ?? "Europe/Moscow";
      json.Url = model.url ?? "http://*:5000";

      await _context.SaveChangesAsync();

      return Ok();
    }

    /// <summary>
    /// Редактировать настройки SMTP сервера. Этот запрос можно игнорировать в случае если данные есть и пользователь их не редактировал
    /// </summary>
    [HttpPut("smtp")]
    public async Task<IActionResult> SmtpServerEdit([FromBody] SetupSmtpJson model)
    {
      SetupEntity entity = await _context.Setup.FirstAsync(s => s.Id == 1);

      SmtpJson json = entity.Smtp;

      json ??= new SmtpJson();

      json.Login = model.login;
      json.Url = model.url;
      json.Password = model.password;
      json.Port = model.port;

      await _context.SaveChangesAsync();

      Startup.SetupModel = entity;

      System.IO.File.WriteAllText(AppContext.BaseDirectory + "../setup.txt", DateTime.Now.ToString());

      return Ok();
    }

    /// <summary>
    /// Загрузить сертификат SSL
    /// </summary>
    /// <param name="server">Принимает значение 1 - сертификат для сервера, 2 сертификат для SMTP. По умолчанию принимает значение 1</param>
    [HttpPost("cetificate/upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] int server)
    {
      return Ok();
    }

    /// <summary>
    /// Отправка естового письма
    /// </summary>
    [HttpPost("smtp/check")]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
      return Ok();
    }
  }

  public class SetupMainJson
  {
    [Required]
    [MinLength(2)]
    [MaxLength(2)]
    [SwaggerSchema("Язык в формате **ISO 639-1**")]
    public string language { get; set; }

    [Required]
    [SwaggerSchema("Название компании")]
    public string companyName { get; set; }

    [Required]
    [EmailAddress]
    [SwaggerSchema("Email владельца")]
    public string email { get; set; }

    [Required]
    [SwaggerSchema("Пароль владельца")]
    public string password { get; set; }

    [SwaggerSchema("Список доступных сотрудников")]
    public string[] users { get; set; }
  }

  public class SetupServerJson
  {
    [Required]
    [SwaggerSchema("Часовой пояс для устройств")]
    public string timezone { get; set; }

    [SwaggerSchema("Ip адрес или домен сервера")]
    public string url { get; set; }

    [SwaggerSchema("SSL")]
    public bool ssl { get; set; }
  }

  public class SetupSmtpJson
  {
    [SwaggerSchema("Ip адрес или домен SMTP сервера")]
    public string url { get; set; }

    [SwaggerSchema("Логин SMTP сервера")]
    public string login { get; set; }

    [SwaggerSchema("Пароль SMTP сервера")]
    public string password { get; set; }

    [SwaggerSchema("Порт SMTP сервера")]
    public int port { get; set; }

    [SwaggerSchema("SSL")]
    public bool ssl { get; set; }
  }
}