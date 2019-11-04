using MessagePack;

namespace GrandCentralDispatch.Contract.Models
{
    /// <summary>
    /// This is the payload exchanged between the cluster and its nodes
    /// It should be decorated by <see cref="MessagePackObjectAttribute"/> because it is serialized using MessagePack
    /// </summary>
    [MessagePackObject(true)]
    public class MovieReview
    {
        public string ReviewText { get; set; }
    }
}
