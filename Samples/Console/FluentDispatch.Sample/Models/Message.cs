using MessagePack;

namespace FluentDispatch.Sample.Models
{
    [MessagePackObject(true)]
    public class Message
    {
        public string Body { get; set; }
    }
}
