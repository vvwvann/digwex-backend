using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Digwex.Services;

namespace Digwex.Controllers.Api
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  public class GlobalController : ControllerBase
  {
    private DeviceService _service;

    public GlobalController(DeviceService service)
    {
      _service = service;
    }
    /// <summary>
    /// Отправить команду синхронизации на устройства
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
      await _service.SyncAllAsync();
      return Ok();
    }

    /// <summary>
    /// Необходимость команды синхронизации
    /// </summary>
    /// <response code="200">Есть плееры, на которые нужно отправить команду синхронизации</response>
    /// <response code="204">Нету плееров, на которые нужно отправить команду синхронизации</response>
    [HttpGet("sync")]
    public IActionResult SyncRequire()
    {
      if (DeviceService.ExistToSync()) {
        return Ok();
      }
      else {
        return NoContent();
      }
    }
  }
}