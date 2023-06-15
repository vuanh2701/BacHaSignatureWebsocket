using System;

namespace WSSign.Models
{
    public class MessageRequest
    {
        public Guid Id { get; set; }
        public string Ip { get; set; }
        public string AgentType { get; set; }
        public string StatusCode { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
        public string Cmd { get; set; }
        public string Additional { get; set; }
    }
}
