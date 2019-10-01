using MessagePack;

namespace GrandCentralDispatch.Sample.Models
{
    [MessagePackObject(true)]
    public class Message
    {
        public string Body { get; set; }
    }
}
