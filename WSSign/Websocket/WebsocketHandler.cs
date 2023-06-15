
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WSSign.Constans;
using WSSign.Models;

namespace WSSign.Websocket
{
    public class WebsocketHandler : IWebsocketHandler
    {
        public List<SocketConnection> websocketConnections = new List<SocketConnection>();

        public WebsocketHandler()
        {
            SetupCleanUpTask();
        }

        public async Task Handle(Guid id,string ip, string agentType, WebSocket webSocket)
        {
            lock (websocketConnections)
            {
                if(websocketConnections.Count > 0)
                {
                    if(agentType == TypeConnect.AGENT)
                    {
                        var agent = websocketConnections.FirstOrDefault(x => x.Ip == ip && x.AgentType == TypeConnect.AGENT);
                        if(agent != null)
                        {
                            websocketConnections.Remove(agent);
                        }else
                        {
                            Console.WriteLine($"Tạo kết nối thành công client Id:{id} - Ip: {ip} - Agent: {agentType}");
                            websocketConnections.Add(new SocketConnection
                            {
                                Id = id,
                                Ip = ip,
                                AgentType = agentType,
                                WebSocket = webSocket
                            });
                        }
                    }
                    else
                    {
                        websocketConnections.Add(new SocketConnection
                        {
                            Id = id,
                            Ip = ip,
                            AgentType = agentType,
                            WebSocket = webSocket
                        });
                    }
                }else
                {
                    websocketConnections.Add(new SocketConnection
                    {
                        Id = id,
                        Ip = ip,
                        AgentType = agentType,
                        WebSocket = webSocket
                    });
                }    
            }
            // thong bao khi ket noi thanh cong
            MessageRequest messageRequest = new MessageRequest
            {
                Id = id,
                Ip = ip,
                AgentType = agentType,
                StatusCode = "00",
                Message = $"Kết nối thành công với id: {id} - ip: {ip} - loại: {agentType}",
                Data = "",
                Cmd = ""
            };
            string message = JsonConvert.SerializeObject(messageRequest);
            await SendMessageToSockets(message, id, ip, agentType);
            // nhan thong tin 
            while (webSocket.State == WebSocketState.Open)
            {
                await ReceiveMessage(id, ip,agentType, webSocket);
             }
        }

        private async Task<string> ReceiveMessage(Guid id,string ip,string agentType, WebSocket webSocket)
        {  
            var arraySegment = new ArraySegment<byte>(new byte[4096]);
            var receivedMessage = await webSocket.ReceiveAsync(arraySegment, CancellationToken.None);
            if (receivedMessage.MessageType == WebSocketMessageType.Text)
            {
                string json = Encoding.UTF8.GetString(arraySegment.Take(receivedMessage.Count).ToArray());
                if(TryParseJSON(json))
                {
                    var message = JsonConvert.DeserializeObject<MessageRequest>(json);
                    if(message.Cmd == "PING")
                    {
                        MessageRequest msg = new MessageRequest();
                        msg.Id = id;
                        msg.Ip = ip;
                        msg.AgentType = agentType;
                        msg.Cmd = "PONG";
                        string data = JsonConvert.SerializeObject(msg);
                        await SendMessageToSockets(data, msg.Id, msg.Ip, TypeConnect.AGENT);
                        Console.Write("PONG");
                    }
                    else
                    {
                        if (agentType == TypeConnect.BROWSER)
                        {
                            message.Id = id;
                            message.Ip = ip;
                            message.AgentType = agentType;
                            string data = JsonConvert.SerializeObject(message);
                            await SendMessageToSockets(data, id, ip, TypeConnect.AGENT);
                        }
                        else
                        {
                            await SendMessageToSockets(json, message.Id, message.Ip, TypeConnect.BROWSER);
                        }
                    }
                }
                else
                {
                    MessageRequest msg = new MessageRequest();
                    msg.Id = id;
                    msg.Ip = ip;
                    msg.AgentType = agentType;
                    string data = JsonConvert.SerializeObject(msg);
                    await SendMessageToSockets(data, msg.Id, msg.Ip, TypeConnect.BROWSER);
                    Console.Write("Convert Json to object bị lỗi. Vui lòng xem lại.");
                }
            }
            return null;
        }

        private async Task SendMessageToSockets(string message,Guid id , string ip, string agentType)
         {
           // gui ket qua từ agent toi client
            if (agentType == TypeConnect.AGENT)
            {
                SocketConnection connection = null;
                lock (websocketConnections)
                {
                    connection = websocketConnections.FirstOrDefault(x => x.AgentType == TypeConnect.AGENT && x.Ip == ip && x.WebSocket.State == WebSocketState.Open);
                }
                MessageRequest req = new MessageRequest();
                req.Id = id;
                req.Ip = ip;
                if (connection != null)
                {
                    // gui du lieu toi agent de xu ly
                    var bytes = Encoding.UTF8.GetBytes(message);
                    var arraySegment = new ArraySegment<byte>(bytes);
                    await connection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    req.StatusCode = "05";
                    req.Message = $"Không có connect được tới Agent.Vui lòng cài đặt hoặt bật lại ứng dụng.";
                    req.Data = "";
                    req.Cmd = "SIGN";
                    req.AgentType = "AGENT";
                    // tim client đã gửi xuống 
                    SocketConnection curentClient = null;
                    lock (websocketConnections)
                    {
                        curentClient = websocketConnections.Where(x => x.AgentType == TypeConnect.BROWSER && x.Id == id && x.Ip == ip && x.WebSocket.State == WebSocketState.Open).FirstOrDefault();
                    }
                    if (curentClient != null)
                    {
                        var dataReq = JsonConvert.SerializeObject(req);
                        var bytes = Encoding.UTF8.GetBytes(dataReq);
                        var arraySegment = new ArraySegment<byte>(bytes);
                        await curentClient.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                        Console.WriteLine($"Gửi dữ liệu tới client Id:{id} - Ip: {ip} thành công");
                    }
                    else
                    {
                        Console.WriteLine($"Không tìm được client Id:{id} - Ip: {ip} với trạng thái open. Vui lòng refesh client để kết nối lại.");
                    }
                }
            }
            else
            {
                SocketConnection curentClient = null;
                lock (websocketConnections)
                {
                    curentClient = websocketConnections.Where(x => x.AgentType == TypeConnect.BROWSER && x.Id == id && x.Ip == ip && x.WebSocket.State == WebSocketState.Open).FirstOrDefault();
                }
                if (curentClient != null)
                {
                   // var dataReq = JsonConvert.SerializeObject(message);
                   // var bytes = Encoding.UTF8.GetBytes(dataReq);
                    //var arraySegment = new ArraySegment<byte>(bytes);
                    var encoded = Encoding.UTF8.GetBytes(message);
                    var arraySegment = new ArraySegment<Byte>(encoded, 0, encoded.Length);
                    await curentClient.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"Gửi dữ liệu tới client Id:{id} - Ip: {ip} thành công");
                }
                else
                {
                    Console.WriteLine($"Không tìm được client Id:{id} - Ip: {ip} với trạng thái open. Vui lòng refesh client để kết nối lại.");
                }

            }
        }

        private void SetupCleanUpTask()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    IEnumerable<SocketConnection> openSockets;
                    IEnumerable<SocketConnection> closedSockets;

                    lock (websocketConnections)
                    {
                        openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
                        closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting);

                        websocketConnections = openSockets.ToList();
                    }

                    //foreach (var closedWebsocketConnection in closedSockets)
                    //{
                    //    MessageRequest messageRequest = new MessageRequest
                    //    {
                    //        StatusCode = "00",
                    //        Message = $"Đóng kết nối với id: {closedWebsocketConnection.Id} - ip: {closedWebsocketConnection.Ip} - loại: {closedWebsocketConnection.AgentType}",
                    //        Data = "",
                    //        Cmd = ""
                    //    };
                    //}
                    await Task.Delay(50000);
              }

            });
        }
        private static bool TryParseJSON(string json, out JObject jObject)
        {
            try
            {
                jObject = JObject.Parse(json);
                return true;
            }
            catch
            {
                jObject = null;
                return false;
            }
        }
        private static bool TryParseJSON(string json)
        {
            try
            {
                if ((json.StartsWith("{") && json.EndsWith("}")) ||
                    (json.StartsWith("[") && json.EndsWith("]")))
                {
                    var jObject = JObject.Parse(json);
                    return true;
                }
            }
            catch
            {
               // Log.Verbose(json);
               return false;
            }
            return false;
        }
        public void CloseAgent(string ip)
        {
            lock (websocketConnections)
            {
                if (websocketConnections.Count > 0)
                {
                    var agent = websocketConnections.FirstOrDefault(x => x.Ip == ip && x.AgentType == TypeConnect.AGENT);
                    if (agent != null)
                    {

                        agent.WebSocket.Abort();
                        agent.WebSocket.Dispose();
                    }
                }
            }
        }

    }

    public class SocketConnection
    {
        public Guid Id { get; set; }
        public string Ip { get; set; }
        public string AgentType { get; set; }
        public WebSocket WebSocket { get; set; }
    }
}
