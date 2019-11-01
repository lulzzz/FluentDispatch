using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Nodes
{
    public interface INode
    {
        /// <summary>
        /// <see cref="NodeMetrics"/>
        /// </summary>
        NodeMetrics NodeMetrics { get; }
    }
}