using FluentDispatch.Models;

namespace FluentDispatch.Nodes
{
    public interface INode
    {
        /// <summary>
        /// <see cref="NodeMetrics"/>
        /// </summary>
        NodeMetrics NodeMetrics { get; }
    }
}