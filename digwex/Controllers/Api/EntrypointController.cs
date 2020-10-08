using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Digwex.Data;

namespace Digwex.Controllers.Api
{
  [ApiController]
  [Route("v3/[controller]")]
  [ApiExplorerSettings(IgnoreApi = true)]
  public class EntrypointController : ControllerBase
  {

    private static readonly object TEST_CONTENT = new {
      url = "https://d3dtpln0j3ciqn.cloudfront.net/content/2017-03-26/izFIW0UVF2WFwwtYEzvUNbFZnDIzmTmA",
      md5 = "0c104999b6554ba3e1e34d236f368693"
    };

    private readonly ApplicationDbContext _context;

    public EntrypointController(ApplicationDbContext context)
    {
      _context = context;
    }

    [HttpPost("activate")]
    public async Task<ActivateModel> Activate(JObject obj)
    {
      string pin = (string)obj["pin"];
      string platform = (string)obj["platform"];

      if (pin == null || platform == null) {
        Response.StatusCode = 400;
        return null;
      }

      PlayerEntity device = await _context.Players
        .FirstOrDefaultAsync(s => s.Pin == pin);

      if (device == null) {
        Response.StatusCode = 422;
        return null;
      }

      if (device.IsActivate) {
        Response.StatusCode = 400;
        return null;
      }

      device.Platform = platform;
      device.IsActivate = true;
      device.Pin = null;

      await _context.SaveChangesAsync();

      if (string.IsNullOrEmpty(device.Timezone)) {
        device.Timezone = "Europe/Moscow";
      }

      return new ActivateModel {
        Configuration = new ConfigurationActivate {
          Timezone = device.Timezone,
          DeviceId = device.Id,
          AccessToken = device.Token,
          BackendUrl = $"{Request.Scheme}://{Request.Host.Value}"
        }
      };
    }

    [HttpGet("time")]
    public string Time()
    {
      return DateTime.UtcNow.ToString();
    }

    [HttpGet("ping")]
    public string Ping()
    {
      return "pong";
    }

    [HttpGet("test_content")]
    public object TestContent()
    {
      return TEST_CONTENT;
    }
  }
}
