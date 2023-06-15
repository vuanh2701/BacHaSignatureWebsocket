using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using WSSign.Constans;
using WSSign.Websocket;

namespace WSSign.Controllers
{
    [Route("api/[controller]")]
    public class SignController : Controller
    {
        public IWebsocketHandler _websocketHandler { get; }
        public SignController(IWebsocketHandler websocketHandler)
        {
            _websocketHandler = websocketHandler;
        }
        [HttpGet]
        public async Task Get()
        {
            var context = ControllerContext.HttpContext;
            var isSocketRequest = context.WebSockets.IsWebSocketRequest;
            if (isSocketRequest)
            {
                WebSocket websocket = await context.WebSockets.AcceptWebSocketAsync();
                await _websocketHandler.Handle(Guid.NewGuid(), GetUserIP(), GetAgentType(), websocket);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        }
        public string GetUserIP()
        {
            string ipaddress = HttpContext.Request.HttpContext.Connection.RemoteIpAddress.ToString();
            if (string.IsNullOrEmpty(ipaddress))
            {
                ipaddress = Request.Headers["HTTP_X_FORWARDED_FOR"];
                if (string.IsNullOrEmpty(ipaddress))
                {
                    ipaddress = Request.Headers["REMOTE_ADDR"];
                }
            }
            Console.WriteLine("IP connect:" + ipaddress);
            return ipaddress;
         }
        public string GetAgentType()
        {
            var agent = Request.Headers["User-Agent"];
            if(string.IsNullOrEmpty(agent))
            {
                return TypeConnect.AGENT;
            }else
            {
                return TypeConnect.BROWSER;
            }    
        }
    }
}
