using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace GrandCentralDispatch.Monitoring
{
    internal class MetricsStartup : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseMetricsAllEndpoints();
                app.UseMetricsAllMiddleware();
                next(app);
            };
        }
    }
}