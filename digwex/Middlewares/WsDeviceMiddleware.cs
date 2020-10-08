using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Digwex.Data;
using Digwex.Services;

namespace Digwex.Middlewares
{
  public class WsDeviceMiddleware
  {
    private readonly RequestDelegate _next;

    public WsDeviceMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task Invoke(HttpContext context, DeviceService deviceService)
    {
      if (!context.WebSockets.IsWebSocketRequest) {
        await _next.Invoke(context);
        return;
      }

      Console.WriteLine("CONNECT");

      if (!context.Request.Query.TryGetValue("authorization", out StringValues values)) return;

      string[] query = values.ToArray();
      query = query[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      PlayerEntity device = await deviceService.TryOauthAutorizationAsync(query[1]);

      if (device == null) {
        Console.WriteLine("Token not found!");
        return;
      }

      WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

      await deviceService.OnConnectedAsync(socket, device.Id);

      await Receive(socket, device.Id, deviceService);
    }

    private async Task Receive(WebSocket socket, int id, DeviceService deviceService) // id - player
    {
      byte[] buffer = new byte[4096];
      StringBuilder all = new StringBuilder();

      while (socket.State == WebSocketState.Open) {
        try {
          WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

          if (result.MessageType == WebSocketMessageType.Text) {
            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            all.Append(message);
            if (result.EndOfMessage) {
              //deviceService = serviceProvider.GetService<DeviceService>();
              //Console.WriteLine("Service1: " + deviceService.GetHashCode());
              await deviceService.ReceiveAsync(socket, id, all.ToString());
              all.Clear();
            }
          }
        }
        catch (WebSocketException e) {
          if (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
            break;
          }
        }
      }

      //deviceService = serviceProvider.GetService<DeviceService>();
      //Console.WriteLine("Service2: " + deviceService.GetHashCode());
      await deviceService.OnDisconnectedAsync(id);
    }
  }
}
