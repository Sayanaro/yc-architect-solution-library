﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.IO;

using YC.SpeechKit.Streaming.Asr;
using yc_scale_2022.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace yc_scale_2022.Controllers
{
    public class SpeechKitAsrController : ControllerBase
    {
        public event EventHandler<AudioDataEventArgs> AudioBinaryRecived;

        private readonly ILogger log;
        private readonly IConfiguration configuration;

        public SpeechKitAsrController(ILogger<SpeechKitAsrController> logger, IConfiguration configuration)
        {
            this.log = logger;
            this.configuration = configuration;

        }

        [HttpGet("/ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await AudioStreaming(HttpContext, webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private async Task AudioStreaming(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 10];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            String jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);

            try
            {
                var initMessage = JsonSerializer.Deserialize<AudioStreamFormat>(jsonString);
                AsrProcessor processor = new AsrProcessor( initMessage, this.configuration);
                
                var payload = new WssPayload { type = WssPayload.MSG_TYPE_CONNECT, data = "ok" };
                string jsonReplay = JsonSerializer.Serialize(payload);
                byte[] bytePayload = Encoding.UTF8.GetBytes(jsonReplay);

                // handshake confirmed
                await webSocket.SendAsync(new ArraySegment<byte>(bytePayload, 0, bytePayload.Length),
                                WebSocketMessageType.Text, true, CancellationToken.None);


                 processor.Init(this, webSocket);
  //              using (FileStream fs = System.IO.File.Open("C:\\\\tmp\\wss_out.wav", FileMode.Create, FileAccess.Write ))
  //              {
                    while (!result.CloseStatus.HasValue)
                    {                      
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        //fs.Write(buffer, 0, result.Count);
                        AudioBinaryRecived?.Invoke(this, AudioDataEventArgs.FromByateArray(buffer, result.Count));
                    }
                //             }
                processor.Dispose();
            }
            catch(JsonException ex)
            {
                log.LogError($"Error parsing json {jsonString} /n {ex}");
            }
            catch (Exception ex)
            {
                log.LogError($"Unknown error {ex}");
            }
            finally
            {
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
        }
    }
}
