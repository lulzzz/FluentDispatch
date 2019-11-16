<h1 align="center">
  <img src="https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/Logo.png" height="128" width="128" alt="Logo" />
  <br />
  GrandCentralDispatch
</h1>

[![NuGet](https://img.shields.io/nuget/v/GrandCentralDispatch.svg)](https://www.nuget.org/packages/GrandCentralDispatch/) [![NuGet](https://img.shields.io/nuget/dt/GrandCentralDispatch.svg)](https://www.nuget.org/stats/packages/GrandCentralDispatch?groupby=Version)
<p align="left">
  <a href="https://github.com/bbougot/GrandCentralDispatch/actions"><img alt="GitHub Actions status" src="https://github.com/bbougot/GrandCentralDispatch/workflows/.NET%20Core/badge.svg"></a>
</p>

# What is it?

**GrandCentralDispatch** is a .NET Standard 2.1 framework which makes easy to scaffold distributed systems and dispatch incoming load into units of work in a deterministic way. This framework is useful whenever you want to process a heavy workload coming from a specific source of data (i.e message broker, web endpoint, ...) in a non-blocking way (fire-and-forget pattern) but still being able to benefit from resiliency features (circuit beaking, back pressure, ...). The framework can be used to dispatch load into units of work **locally** (using .NET Threadpool) or **remotely** (using Remote Procedure Calls).

# Table of Contents

- [Installing from NuGet](#installing-from-nuget)
- [Quick start](#quick-start)
	- [Architecture](#architecture)
	- [Resolver](#resolver) 
	- [Node](#node)
	- [Cluster](#cluster)
- [Advanced Usage](#advanced-usage)
	- [Sequential Processing](#sequential-processing)
	- [Parallel Processing](#parallel-processing) 
	- [Node Queuing Strategy](#node-queuing-strategy)
- [Circuit Breaking](#circuit-breaking)
- [Processing Type](#processing-type)
	- [Local](#local)
	- [Remote](#remote)
- [Resolver chaining](#resolver-chaining)
- [Persistence](#persistence)
- [Monitoring](#monitoring)
- [Hosting](#hosting)
- [Samples](#samples)
	- [Local Processing](#local-processing)
	- [Remote Processing](#remote-processing) 
- [Requirements](#requirements)

# Installing from NuGet

To install **GrandCentralDispatch**, run the following command in the **Package Manager Console**

```
Install-Package GrandCentralDispatch
```

More details available [here](https://www.nuget.org/packages/GrandCentralDispatch/).

# Quick Start
## Architecture
![Architecture](https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/Architecture.png)

**GrandCentralDispatch** handles the incoming load and delegates the ingress traffic as chunks to event loop schedulers which dispatch them to their own nodes (units of work). These nodes are either local threads managed by the .NET Threadpool or remote nodes which are called through Remote Procedure Call.

**GrandCentralDispatch** acts as a load-balancer but on the application level rather than the network level. GCD is able to monitor the health of its remote nodes (CPU usage, ...) and dispatch workload to the healthiest among them in order to anticipate any overwhelm node prior any downtime. 
 
## Resolver

**GrandCentralDispatch** provides a base class `Resolver<T>` that wraps your processing logic. It exposes an asynchronous and virtual `Process` method that you can override and which will execute whenever you synchronously **post** a new item to be later processed.

The `Process` method gets executed within a round-robin way, which means if you posted **100 items** into the system (the system may be composed of 1 cluster and 2 nodes), and if you've setup the cluster using options _NodeThrottling=10_ ¹ and _WindowInMilliseconds=1000_, then you will be able to process **10 items per second**. The remaining items are still waiting in the queue to be later processed.

¹ _NodeThrottling_ and other options are browsable [here](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch/Options/ClusterOptions.cs) for reference. 

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
        _requestCluster.Dispatch(Request.HttpContext.Connection.RemoteIpAddress);

        return Ok();
    }
}
```
The value is posted away from the calling thread, being synchronous and non-blocking, to be later processed by one of the 5 local nodes through the defined resolver.

## Advanced Usage
The nodes support two differents processing strategies: **sequential** or **parallel**.

### Sequential Processing
The node can be setup using a sequential approach (_ClusterProcessingType=ClusterProcessingType.Sequential_), meaning that every bulk of items will be sequentially processed. In this mode, the process of an item must be completed prior moving to the next one.

This type of processing is the least CPU consuming, but may increase the queue size: items are unqueued slower than parallel processing, which may lead to a **higher memory consumption**.

### Parallel Processing
The node processes bulk of items in parallel (_ClusterProcessingType=ClusterProcessingType.Parallel_). In this mode, the completion of an item's process is non-blocking in regards of the other items: the node can process several items at the same time depending on the degree of parallelism of your processor (a 4-core processor will process twice as much as a 2-core processor).

This type of processing is more CPU consuming, but it's optimal in regards of the queue size: items are fastly unqueued which **reduce the memory consumption**.

### Node Queuing Strategy
Whenever new items are available to be processed, the cluster will dispatch them to the available nodes depending on the **NodeQueuingStrategy** option.

#### Randomized
Items are queued to nodes randomly.

#### Best Effort
Items are queued to least populated nodes first.

#### Healthiest
Items are queued to healthiest nodes first, taking into account CPU usage of remote nodes.

## Circuit Breaking
The resiliency of the item's processing within the resolver by each node is ensured by a circuit breaker whose options can be setup through **CircuitBreakerOptions**. 

Whenever your `Process` method raises an exception, it will be catch up by the circuit breaker, and depending on the threshold you specified, the circuit may open to protect your node. Additionally, the `Process` method can retry if the option **RetryAttempt** is > 0.

Also, every node is independent from the others so that an opened circuit will not impact the other nodes. **Making sure one fault doesn't sink the whole ship**.

## Processing Type
### Local
By default, a node is a local unit of work, which translates to a simple thread managed by the .NET Threadpool. 

### Remote
GrandCentralDispatch also provides the ability to dispatch work accross remote nodes, using **Remote Procedure Call**. By doing so, the cluster must be provided with **Hosts** option filled with IP address and port of the corresponding nodes. You will have to deploy a node on the specified machine, a sample is accessible [here](https://github.com/bbougot/GrandCentralDispatch/tree/master/GrandCentralDispatch.Node).

## Resolver chaining
While declaring a single resolver to the cluster is the simplest use case, you may need to compute several and independent tasks to your incoming workload, before having to deal with a final result. 

**GrandCentralDispatch** is able to decouple your workflow into several _partial_ resolvers without tied-coupling them, whose result will be computed by a _final_ resolver.

### Partial resolver
A partial resolver is a generic resolver of type `PartialResolver<TInput1,TOuput1>` where TInput1 is the **input** type to process and TOutput1 is the **output** type which will later be manipulated by your final resolver.

You can compose your cluster with up to 2 partial resolvers, which will compute your workload into intermediate and independent results. 

For instance, you may have a `MetadataResolver` resolver which retrieves the informations of a movie (overview, cast, ...) from an API and a `SentimentPredictionResolver` resolver which will use Tensorflow to proceed a sentiment analysis based on a user rating.

Both of these resolvers are independent and thus are computed in parallel.

### Final resolver
The final resolver is a generic resolver of type `DualResolver<TOutput1, TOutput2>` where TOutput1 is the result type of your first partial resolver and TOutput2 is the result type of your second partial resolver.

This resolver will then be able to process both of the computed results based on your custom implementation.

For instance, you may have a `IndexerResolver` resolver which indexes both the movie metadata computed from `MetadataResolver` as well as the sentiment analysis (user liked/disliked the movie) computed from `SentimentPredictionResolver` into an ElasticSearch cluster.

## Persistence
**GrandCentralDispatch** is able to store on disk and in real-time the workload submitted to the cluster. 

Persistence feature let the cluster recover from a hard failure (power outage, crash, ...) while resuming on the items which have not been fully processed prior the incident.

Be aware this feature introduces a non-negligeable overhead, due to on-the-fly serializing and storing, which is why this feature is disabled by default (_PersistenceEnabled=false_). 

## Monitoring
**GrandCentralDispatch** is able to expose key metrics (node/cluster performance counters, throughput, Apdex score, latency, ...) through InfluxDB using AppMetrics. 

The logic is implemented through the NuGet package GrandCentralDispatch.Monitoring.

```
Install-Package GrandCentralDispatch.Monitoring
```

More details available [here](https://www.nuget.org/packages/GrandCentralDispatch.Monitoring/).

## Hosting
**GrandCentralDispatch** facilitates its integration to an IHost instance by providing:

- [GrandCentralDispatchClusterHost](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch.Host/Hosting/GrandCentralDispatchClusterHost.cs): Scaffolds the cluster by offering a convenient way to provide cluster options, logging options, monitoring and port binding. 
- [GrandCentralDispatchNodeHost](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch.Host/Hosting/GrandCentralDispatchNodeHost.cs): Scaffolds the node by offering a convenient way to provide resolver types, logging options and port binding. 

The logic is implemented through the NuGet package GrandCentralDispatch.Host.

```
Install-Package GrandCentralDispatch.Host
```

More details available [here](https://www.nuget.org/packages/GrandCentralDispatch.Host/).

## Samples
### Local Processing
The [sample](https://github.com/bbougot/GrandCentralDispatch/tree/master/GrandCentralDispatch.Sample) is a .NET Core 3.0 console application which uses **GrandCentralDispatch** to:

- Send 10 000 messages to the cluster.
- Dispatch them to 10 local nodes.
- The resolver processes each of the incoming messages to compute a Fibonacci number (1000) and await for 125ms delay (which simulates a short I/O).
- Prints in the console the throughput of the cluster (msg/s) as well as the throughput of each individual nodes.

![Sample](https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/sample.png)

![Animated sample](https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/animated-sample.gif)

This sample demonstrates the ability to offload a heavy process away from the main thread (the thread is still responsive while dispatching the messages) in a non-blocking and synchronous way (i.e fire and forget).

### Remote Processing
The sample is decoupled in 3 parts:

- [Node](https://github.com/bbougot/GrandCentralDispatch/tree/master/GrandCentralDispatch.Host): .NET Core 3.0 Web Host deployed on a machine and identified by an IP address and a port (http://localhost:9090) on which the cluster establishes a gRPC connection.
- [Cluster](https://github.com/bbougot/GrandCentralDispatch/tree/master/GrandCentralDispatch.Cluster): .NET Core 3.0 Web Host which exposes a RESTful endpoint (POST http://localhost:5432/api/sentiment).
- [Contract](https://github.com/bbougot/GrandCentralDispatch/tree/master/GrandCentralDispatch.Contract): .NET Standard 2.1 assembly containing all the necessary resolvers used by the nodes to process the incoming requests.

The goal is to send POST requests to the cluster (http://localhost:5432/api/sentiment) containing a JSON body:

```
{
	'Title': 'Avatar',
	'ReviewText': 'good movie!'
}
```

The cluster dispatches the content of this request to its healthiest remote nodes using gRPC, on which two resolvers are called independently (they are not tied coupled):

**Partial Resolvers**

-	[MetadataResolver](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch.Contract/Resolvers/MetadataResolver.cs): Retrieve the movie overview from TMDb.
- [SentimentPredictionResolver](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch.Contract/Resolvers/SentimentPredictionResolver.cs): Uses Tensorflow and processes a text analysis to extract the sentiment behind the ReviewText property. 

**Final Resolver**

- [IndexerResolver](https://github.com/bbougot/GrandCentralDispatch/blob/master/GrandCentralDispatch.Contract/Resolvers/IndexerResolver.cs): Waits for the 2 first resolvers to finish and indexes the result (title, movie overview and user-based movie sentiment: i.e liked or disliked) in ElasticSearch.

ElasticSearch is automatically deployed through Docker as well as the Node, Cluster, monitoring stack (InfluxDB, Grafana) and other ELK stack tools (Logstash and Kibana).

After cloning this repository, you only need to execute this [script](https://github.com/bbougot/GrandCentralDispatch/blob/master/DockerSetup.cmd) in Windows or this [script](https://github.com/bbougot/GrandCentralDispatch/blob/master/DockerSetup.sh) if running Unix/Linux/macOS environments. Make sure you're using the latest version of Docker/Docker Compose.

The results of each request is then accessible through Kibana under the index `sentiment` (http://localhost:5601) and monitoring is available through Grafana (http://localhost:3000).

This sample demonstrates the ability to dispatch a workload to remote machines in order to guarantee a high availability. It works similarly to a load-balancer, but on the application level.

**Processing**

![Remote sample](https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/remote-sample.gif)

**Monitoring**

![Monitoring](https://raw.githubusercontent.com/bbougot/GrandCentralDispatch/master/Assets/monitoring.gif)

## Requirements

**GrandCentralDispatch** framework requires .NET Standard 2.1 support, and is available for applications targeting .NET Core >= 3.0, thus supporting Windows/Linux/macOS environments.



Additionally, you need Docker to run remote-based samples on your PC.
