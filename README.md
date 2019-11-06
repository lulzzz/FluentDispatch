<h1 align="center">
  <img src="https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/Logo.png" height="128" width="128" alt="Logo" />
  <br />
  GrandCentralDispatch
</h1>

[![NuGet](https://img.shields.io/nuget/v/GrandCentralDispatch.svg)](https://www.nuget.org/packages/GrandCentralDispatch/) [![NuGet](https://img.shields.io/nuget/dt/GrandCentralDispatch.svg)](https://www.nuget.org/stats/packages/GrandCentralDispatch?groupby=Version)

# What is it?

**GrandCentralDispatch** is a .NET Standard 2.1 framework which makes easy to scaffold distributed systems and dispatch incoming load into units of work in a deterministic way. This framework is useful whenever you want to process a heavy workload coming from a specific source of data (i.e message broker, web endpoint, ...) in a non-blocking way (fire-and-forget pattern) but still being able to benefit from resiliency features (circuit beaking, back pressure, ...). The framework can be used to dispatch load into units of work **locally** (using .NET Threadpool) or **remotely** (using Remote Procedure Calls).

# Table of Contents

- [Installing from NuGet](#installing-from-nuget)
- [Quick start](#quick-start)
  - [Resolver](#resolver) 
  - [Node](#node)
  		- [Sequential Processing](#sequential-processing)
  		
  		- [Parallel Processing](#parallel-processing) 
  		
  		- [Circuit Breaking](#circuit-breaking)
  		
  		- [Local Processing](#local-processing)
  		
  		- [Remote Processing](#remote-processing)
  		
  		- [Node Queuing Strategy](#node-queuing-strategy)
	- [Cluster](#cluster) 
- [Requirements](#requirements)

# Installing from NuGet

To install **GrandCentralDispatch**, run the following command in the **Package Manager Console**

```
Install-Package GrandCentralDispatch
```

More details available [here](https://www.nuget.org/packages/GrandCentralDispatch/).

# Quick Start
## Resolver

**GrandCentralDispatch** provides a base class `Resolver<T>` that wraps your processing logic. It exposes an asynchronous and virtual `Process` method that you can override and which will execute whenever you synchronously **post** a new item to be later processed.

The `Process` method gets executed within a round-robin way, which means if you posted **100 items** into the system (the system may be composed of 1 cluster and 2 nodes), and if you've setup the cluster using options _NodeThrottling=10_ and _WindowInMilliseconds=1000_, then you will be able to process **10 items per second**. The remaining items are still waiting in the queue to be later processed.

The options provided by GrandCentralDispatch let you specify a way to handle **back-pressure**, by discarding items which could not be processed within the time-window using option _EvictItemsWhenNodesAreFull=true_. This behavior is useful when you want to enforce the main queue to fulfill your predicted throughput without growing unreasonably if you receive a peak of traffic you already know you could not sustain.

Let's write a simple resolver which will compute the geolocation from a request's IP.

```C#
internal sealed class IpResolver : Resolver<IPAddress>
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public IpResolver(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = loggerFactory.CreateLogger<IpResolver>();
    }

    /// <summary>
    /// Process each new IP address and resolve its geolocation
    /// </summary>
    /// <param name="item"><see cref="KeyValuePair{TKey,TValue}"/></param>
    /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="Task"/></returns>
    protected override async Task Process(IPAddress item,
        NodeMetrics nodeMetrics,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            $"New IP {item.ToString()} received, trying to resolve geolocation from node {nodeMetrics.Id}...");
        var response = await _httpClient.GetAsync(new Uri($"http://ip-api.com/json/{item.ToString()}"),
            cancellationToken);
        // Geolocation is a model class which is not detailed here
        var geolocation = JsonConvert.DeserializeObject<Geolocation>(await response.Content.ReadAsStringAsync());
    }
}
```

There are several tips we notice here:

-	`Resolver` base class supports generics, which simplifies the *specialization* of your `Process` method which here can only manipulates items of type **IPAddress**.
- `Process` method supports cancellation by providing a **CancellationToken**, which may be triggered if you pause the processing of your cluster.
- `Process` method provides you a `NodeMetrics` information, which brings you information details on the node which is processing your item (throughput, performance counters, ...).
-	Return type is `Task`, which means you can process stressful, intensive, long I/Os without having to worry about "the other side", where you only push new items in a synchronous and non-blocking way.
- Dependency injection is supported, feel free to provide as much as dependencies you need through the constructor.

## Node
Now you've been introduced to the concept of a *resolver*, let's digg into the node concept. 

GrandCentralDispatch executes your resolver within a **unit of work** which is orchestrated by its cluster. Each node processes its own chunk of items, which corresponds to a slice of the main queue whose items are unqueued in a periodical way (up to **NodeThrottling** count and within **WindowInMilliseconds** time-window) and added to the node assignment work. 

The nodes support two differents processing strategies: **sequential** or **parallel**.

#### Sequential Processing
The node can be setup using a sequential approach (_ClusterProcessingType= ClusterProcessingType.Sequential_), meaning that every bulk of items will be sequentially processed. In this mode, the process of an item must be completed prior moving to the next one.

This type of processing is the least CPU consuming, but may increase the queue size: items are unqueued slower than parallel processing, which may lead to a **higher memory consumption**.

#### Parallel Processing
The node processes bulk of items in parallel (_ClusterProcessingType=ClusterProcessingType.Parallel_). In this mode, the completion of an item's process is non-blocking in regards of the other items: the node can process several items at the same time depending on the degree of parallelism of your processor (a 4-core processor will process twice as much as a 2-core processor).

This type of processing is more CPU consuming, but it's optimal in regards of the queue size: items are fastly unqueued which **reduce the memory consumption**.

### Circuit Breaking
The resiliency of the item's processing within the resolver by each node is ensured by a circuit breaker whose options can be setup through **CircuitBreakerOptions**. 

Whenever your `Process` method raises an exception, it will be catch up by the circuit breaker, and depending on the threshold you specified, the circuit may open to protect your node. Additionally, the `Process` method can retry if the option **RetryAttempt** is > 0.

Also, every node is independent from the others so that an opened circuit will not impact the other nodes. **Making sure one fault doesn't sink the whole ship**.

### Local Processing
By default, a node is a local unit of work, which translates to a simple thread managed by the .NET Threadpool. 

### Remote Processing
GrandCentralDispatch also provides the ability to dispatch work accross remote nodes, using **Remote Procedure Call**. By doing so, the cluster must be provided with **Hosts** option filled with IP address and port of the corresponding nodes. You will have to deploy a node on the specified machine, a sample is accessible [here](https://github.com/bbougot/GrandCentralDispatch/tree/master/GrandCentralDispatch.Node).

Whenever new items are available to be processed, the cluster will dispatch them to the available nodes depending on the **NodeQueuingStrategy** option.

### Node Queuing Strategy
#### Randomized
Items are queued to nodes randomly.

#### Best Effort
Items are queued to least populated nodes first.

#### Healthiest
Items are queued to healthiest nodes first, taking into account CPU usage of remote nodes.

## Cluster
The cluster is the **main entry point** of your distributed system, which will dispatch your load accross all available nodes.

Let's write our first cluster.

```C#
public class Startup : Host.ClusterStartup
{
    public Startup(IConfiguration configuration) : base(configuration)
    {
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();

        // Configuring the clusters is mandatory
        services.ConfigureCluster(clusterOptions => 
        {
        	// We setup 5 local nodes
        	clusterOptions.ClusterSize = 5; 
        },
        circuitBreakerOptions => { });

        services.AddCluster<IPAddress>(
            sp => new IpResolver(sp.GetService<IHttpClientFactory>(), sp.GetService<ILoggerFactory>()));
        base.ConfigureServices(services);
    }
}
```

GrandCentralDispatch supports **dependency injection**, the cluster will be available through it accross the whole application and relies on a singleton lifetime. You can also declare it and use it manually as explained [here](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch.Sample/Program.cs).

### Usage
Once your cluster is defined, you can use it as follow:

```C#
[Route("api/[controller]")]
[ApiController]
public class ValuesController : ControllerBase
{
    private readonly ICluster<IPAddress> _requestCluster;

    /// <summary>
    /// The cluster is resolved by the .NET Core DI
    /// </summary>
    /// <param name="requestCluster"></param>
    public ValuesController(ICluster<IPAddress> requestCluster)
    {
        _requestCluster = requestCluster;
    }

    // GET api/values
    [HttpGet]
    public IActionResult Get()
    {
        // We post our data in a fire-and-forget way
        _requestCluster.Dispatch(requestIdentifier, Request.HttpContext.Connection.RemoteIpAddress);

        return Ok();
    }
}
```

The value is posted away from the calling thread, being synchronous and non-blocking, to be later processed by the 5 local nodes through the defined resolver.

# Requirements

**GrandCentralDispatch** framework requires .NET Standard 2.1 support, and it is available for applications targeting:


- .NET Core >= 3.0

Additionally, you need Docker to run remote-based samples on your PC.