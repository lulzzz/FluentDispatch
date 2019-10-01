using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extras.Quartz;
using Quartz;
using GrandCentralDispatch.Sample.Remote.Cluster.Jobs;
using GrandCentralDispatch.Sample.Remote.Cluster.Modules;

namespace GrandCentralDispatch.Sample.Remote.Cluster.Services.Core
{
    public class CoreService : ICoreService
    {
        private IScheduler _scheduler;

        public CancellationTokenSource CancellationTokenSource { get; private set; }

        public async Task Start()
        {
            CancellationTokenSource = new CancellationTokenSource();
            var builder = new ContainerBuilder();
            builder.RegisterModule<QuartzAutofacFactoryModule>();
            builder.RegisterModule<ApiModule>();
            builder.RegisterModule(
                new QuartzAutofacJobsModule(typeof(ApiJob).Assembly));
            var container = builder.Build();
            _scheduler = container.Resolve<IScheduler>();
            await _scheduler.Start(CancellationTokenSource.Token);
            await _scheduler.ScheduleJobs(Helpers.Helper.BuildJobs(), false, CancellationTokenSource.Token);
        }

        public void Stop()
        {
            CancellationTokenSource.Cancel();
        }
    }
}