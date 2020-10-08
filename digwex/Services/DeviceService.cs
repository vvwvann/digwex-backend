using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Digwex.Data;
using Digwex.Extensions;

namespace Digwex.Services
{
  public class DeviceService : WebSocketHandler
  {

    private readonly ApplicationDbContext _context;

    // Key - deviceId
    private static readonly ConcurrentDictionary<int, bool> _syncPlayers = new ConcurrentDictionary<int, bool>();
    public static ConcurrentDictionary<int, bool> _sendCancel = new ConcurrentDictionary<int, bool>();

    public DeviceService(WebSocketService webSocketConnectionManager, ApplicationDbContext context) :
      base(webSocketConnectionManager)
    {
      _context = context;
    }

    public void AddToSync(int id)
    {
      _syncPlayers[id] = true;
    }

    public static bool ExistToSync()
    {
      return _syncPlayers.Count > 0;
    }

    public async Task ReceiveAsync(WebSocket socket, int id, string message)
    {
      Console.WriteLine($"receive: {message}");
      CommunicateRequestModel modelReq = message.ToJsonObject<CommunicateRequestModel>();
      if (modelReq == null) return;

      int[] commands_acknowledge = modelReq.commands_acknowledge ?? new int[0];

      var removes = new List<CommandEntity>();

      foreach (var i in commands_acknowledge) {
        CommandEntity entity = await _context.Commands.FirstOrDefaultAsync(s => s.Id == i);

        if (entity != null) {
          Console.WriteLine("remove: " + entity.Id);
          removes.Add(entity);
        }
      }

      _context.RemoveRange(removes);
      await _context.SaveChangesAsync();

      PlayerEntity player = await _context.Players.FirstOrDefaultAsync(s => s.Id == id);
      player.LastOnline = DateTime.UtcNow;
      Telemetry telemetry = modelReq.telemetry;
      if (telemetry != null) {
        player.Version = telemetry.version;
        player.DeviceTime = telemetry.time;

        if (telemetry.disk.data != null) {
          player.Data = (int)(telemetry.disk.data.available / (double)telemetry.disk.data.total * 100);
        }
      }
      if (modelReq.ulogs != null) {
        foreach (var item in modelReq.ulogs) {
          if (item.type == "device_data") {

            player.LastSync = item.datetime;
            player.Percent = -1;
          }
        }
      }
      if (modelReq.synchronization == null) {
        player.Percent = -1;
      }
      else {
        player.Percent = (int)(modelReq.synchronization.progress * 100);
      }
      await _context.SaveChangesAsync();

      await Task.Delay(7000);

      if (_sendCancel.TryRemove(id, out _)) return;

      if (_syncPlayers.TryRemove(id, out _)) {
        await SyncAsync(id);
        return;
      }

      CommunicateResponseModel modelRes = new CommunicateResponseModel {
        commands = await _context.Commands.Where(s => s.DeviceId == id)
          .Select(s => new Command {
            id = s.Id,
            command = s.Command
          }).ToArrayAsync()
      };

      string res = modelRes.JsonToString();

      Console.WriteLine($"send to player from queue: {res}");

      await SendMessageAsync(socket, res);
    }

    public async Task SyncAsync(int id)
    {
      _syncPlayers.TryRemove(id, out _);

      CommandEntity command = new CommandEntity {
        Command = "synchronize",
      };

      await SendCommandAsync(id, command);
    }

    public async Task SyncAllAsync()
    {
      foreach (var item in _syncPlayers) {
        await SyncAsync(item.Key);
      }
    }

    public async Task SendCommandAsync(int deviceId, CommandEntity command)
    {
      command.DeviceId = deviceId;

      var entity = await _context.Commands.AsNoTracking()
        .FirstOrDefaultAsync(s => s.DeviceId == deviceId
          && s.Command == command.Command);

      if (entity == null) {
        await _context.Commands.AddAsync(command);
        await _context.SaveChangesAsync();
      }

      WebSocket socket = _webSocketConnectionManager.Get(deviceId);
      if (socket == null) {
        Console.WriteLine("player offline: " + deviceId);
        return;
      }

      _sendCancel[deviceId] = true;

      CommunicateResponseModel modelRes = new CommunicateResponseModel {
        commands = await _context.Commands.Where(s => s.DeviceId == deviceId)
          .Select(s => new Command {
            id = s.Id,
            command = s.Command
          }).ToArrayAsync()
      };

      string res = modelRes.JsonToString();

      Console.WriteLine($"send to player now: {res}");

      await SendMessageAsync(socket, res);
    }

    public async Task<PlayerEntity> TryOauthAutorizationAsync(string token)
    {
      if (token == null) return null;
      try {
        PlayerEntity device = await _context.Players
         .SingleOrDefaultAsync(s => s.Token == token);
        if (device == null) return null;
        Console.WriteLine(new { token, device.Token });
        if (device.Token != token)
          device = null;
        return device;
      }
      catch (Exception ex) { Console.WriteLine(ex.Message); }
      return null;
    }

    public async Task SendPackAsync(int id)
    {
      //  USE _CONTEXT
      // generate

      // IModel

      await SendMessageAsync(id, new byte[] { 0, 1 });
    }

    public override async Task ReceiveAsync(WebSocket socket, string message)
    {
      //throw new NotImplementedException();
    }

    public async Task Deactivate(int id)
    {
      WebSocket socket = _webSocketConnectionManager.Remove(id);
      try {
        await socket?.CloseAsync(WebSocketCloseStatus.EndpointUnavailable,
          "away", CancellationToken.None);
      }
      catch { }
    }

    public override async Task OnDisconnectedAsync(int id)
    {
      _webSocketConnectionManager.Remove(id);
      DateTime time = DateTime.UtcNow;
      try {
        PlayerEntity device = await _context.Players.FirstOrDefaultAsync(s => s.Id == id);
        device.LastOnline = time;
        await _context.SaveChangesAsync();
      }
      catch { }
    }

    public override async Task OnConnectedAsync(WebSocket socket, int id)
    {
      try {
        PlayerEntity device = await _context.Players.FirstOrDefaultAsync(s => s.Id == id);
      }
      catch { }
      _webSocketConnectionManager.Add(socket, id);
    }
  }

  public abstract class WebSocketHandler
  {
    protected readonly WebSocketService _webSocketConnectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketHandler"/> class.
    /// </summary>
    /// <param name="webSocketConnectionManager">The web socket connection manager.</param>
    /// <param name="methodInvocationStrategy">The method invocation strategy used for incoming requests.</param>
    public WebSocketHandler(WebSocketService webSocketConnectionManager)
    {
      _webSocketConnectionManager = webSocketConnectionManager;
    }

    /// <summary>
    /// Called when a client has connected to the server.
    /// </summary>
    /// <param name="socket">The web-socket of the client.</param>
    /// <returns>Awaitable Task.</returns>
    public abstract Task OnConnectedAsync(WebSocket socket, int id);


    /// <summary>
    /// Called when a client has disconnected from the server.
    /// </summary>
    /// <param name="socket">The web-socket of the client.</param>
    /// <returns>Awaitable Task.</returns>
    public abstract Task OnDisconnectedAsync(int id);

    public async Task SendMessageAsync(WebSocket socket, string message)
    {
      if (socket.State != WebSocketState.Open)
        return;

      byte[] arr = Encoding.UTF8.GetBytes(message);

      await socket.SendAsync(buffer: new ArraySegment<byte>(arr, 0, arr.Length),
                             messageType: WebSocketMessageType.Text,
                             endOfMessage: true,
                             cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(WebSocket socket, byte[] data)
    {
      if (socket.State != WebSocketState.Open)
        return;

      await socket.SendAsync(buffer: new ArraySegment<byte>(data, 0,
                                                            data.Length),
                             messageType: WebSocketMessageType.Text,
                             endOfMessage: true,
                             cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(int id, byte[] data)
    {
      //if (socket.State != WebSocketState.Open)
      //  return;

      //await socket.SendAsync(buffer: new ArraySegment<byte>(data, 0,
      //                                                      data.Length),
      //                       messageType: WebSocketMessageType.Text,
      //                       endOfMessage: true,
      //                       cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(int socketId, string message)
    {
      await SendMessageAsync(_webSocketConnectionManager.Get(socketId), message);
    }

    public abstract Task ReceiveAsync(WebSocket socket, string message);
  }

  public class WebSocketService
  {
    private ConcurrentDictionary<int, WebSocket> _sockets = new ConcurrentDictionary<int, WebSocket>();

    public WebSocket Get(int id)
    {
      _sockets.TryGetValue(id, out WebSocket socket);
      return socket;
    }

    public void Add(WebSocket socket, int id)
    {
      Console.WriteLine("player connected: " + id);
      _sockets[id] = socket;
    }

    public WebSocket Remove(int id)
    {
      Console.WriteLine("player disconnect: " + id);
      _sockets.TryRemove(id, out WebSocket socket);
      return socket;
    }
  }

  public class CommunicateResponseModel
  {
    public Command[] commands { get; set; }
  }

  public class Command
  {
    public int id { get; set; }
    public string command { get; set; }
  }

  public class CommunicateRequestModel
  {
    public int[] commands_acknowledge { get; set; }
    public bool power { get; set; }
    public Synchronization synchronization { get; set; } // null
    public Telemetry telemetry { get; set; }
    public Ulog[] ulogs { get; set; }
  }

  public class Synchronization
  {
    public int device_data_id { get; set; }
    public double progress { get; set; }
  }

  public class Telemetry
  {
    public string version { get; set; }
    public DateTime time { get; set; }
    public Disk disk { get; set; }
    public string hardware_model { get; set; }
    public string hardware_serial { get; set; }
    public string software_version { get; set; }
    public int[] resolution { get; set; }
    public string network_mac { get; set; }
    public string network_ip { get; set; }
  }

  public class Disk
  {
    public InfoSize root { get; set; }
    public InfoSize data { get; set; }
    public InfoSize tmp { get; set; }
  }

  public class InfoSize
  {
    public long available { get; set; }
    public long total { get; set; }
  }


  public class Ulog
  {
    public DateTime datetime { get; set; }
    public string type { get; set; }
    //public JObject data { get; set; }
  }


}
