using System.Collections.Generic;
using Quartz;
using GrandCentralDispatch.Sample.Remote.Cluster.Jobs;

namespace GrandCentralDispatch.Sample.Remote.Cluster.Helpers
{
    public class Helper
    {
        /// <summary>
        /// Build Quartz jobs
        /// </summary>
        /// <returns></returns>
        public static Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> BuildJobs()
        {
            var apiJob = JobBuilder.Create<ApiJob>().WithIdentity(Constants.ApiJobKey)
                .WithDescription("Job which handles the API.")
                .Build();

            var apiTrigger = TriggerBuilder.Create()
                .WithIdentity(Constants.ApiJobKey.Name, Constants.ApiJobKey.Group)
                .ForJob(Constants.ApiJobKey)
                .StartNow()
                .Build();

            var jobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
            {
                {
                    apiJob, new List<ITrigger>
                    {
                        apiTrigger
                    }.AsReadOnly()
                }
            };

            return jobs;
        }
    }
}