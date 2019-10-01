namespace GrandCentralDispatch.Models
{
    /// <summary>
    /// Define a Host to be used as a remote node
    /// </summary>
    public class Host
    {
        /// <summary>
        /// Machine name or IP address
        /// </summary>
        public string MachineName { get; }

        /// <summary>
        /// Port
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// <see cref="Host"/>
        /// </summary>
        /// <param name="machineName"></param>
        /// <param name="port"></param>
        public Host(string machineName, int port)
        {
            MachineName = machineName;
            Port = port;
        }
    }
}