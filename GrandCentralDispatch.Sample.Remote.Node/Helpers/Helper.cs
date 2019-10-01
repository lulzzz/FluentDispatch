using System.Collections.Generic;
using Quartz;
using GrandCentralDispatch.Sample.Remote.Node.Jobs;

namespace GrandCentralDispatch.Sample.Remote.Node.Helpers
{
    public class Helper
    {
        /// <summary>
        /// Build Quartz jobs
        /// </summary>
        /// <returns></returns>
        public static Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> BuildJobs()
        {
            var nodeJob = JobBuilder.Create<NodeJob>().WithIdentity(Constants.NodeJobKey)
                .WithDescription("Job which handles the node listener.")
                .Build();

            var nodeTrigger = TriggerBuilder.Create()
                .WithIdentity(Constants.NodeJobKey.Name, Constants.NodeJobKey.Group)
                .ForJob(Constants.NodeJobKey)
                .StartNow()
                .Build();

            var jobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
            {
                {
                    nodeJob, new List<ITrigger>
                    {
                        nodeTrigger
                    }.AsReadOnly()
                }
            };

            return jobs;
        }
    }
}