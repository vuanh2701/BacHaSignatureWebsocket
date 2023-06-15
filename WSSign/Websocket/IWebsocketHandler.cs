using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace WSSign.Websocket
{
    public interface IWebsocketHandler
    {
        Task Handle(Guid id, string ip,string agentType, WebSocket websocket);
    }
}
