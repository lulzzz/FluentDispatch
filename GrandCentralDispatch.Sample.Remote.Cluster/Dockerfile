FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 5432

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["GrandCentralDispatch.Sample.Remote.Cluster/GrandCentralDispatch.Sample.Remote.Cluster.csproj", "GrandCentralDispatch.Sample.Remote.Cluster/"]
COPY ["GrandCentralDispatch.Sample.Remote.Contract/GrandCentralDispatch.Sample.Remote.Contract.csproj", "GrandCentralDispatch.Sample.Remote.Contract/"]
COPY ["GrandCentralDispatch/GrandCentralDispatch.csproj", "GrandCentralDispatch/"]
COPY ["GrandCentralDispatch.PerformanceCounters/GrandCentralDispatch.PerformanceCounters.csproj", "GrandCentralDispatch.PerformanceCounters/"]
COPY ["GrandCentralDispatch.Monitoring/GrandCentralDispatch.Monitoring.csproj", "GrandCentralDispatch.Monitoring/"]
RUN dotnet restore "GrandCentralDispatch.Sample.Remote.Cluster/GrandCentralDispatch.Sample.Remote.Cluster.csproj" -s https://www.myget.org/F/appmetrics -s https://api.nuget.org/v3/index.json
COPY . .
WORKDIR "/src/GrandCentralDispatch.Sample.Remote.Cluster"
RUN dotnet build "GrandCentralDispatch.Sample.Remote.Cluster.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GrandCentralDispatch.Sample.Remote.Cluster.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GrandCentralDispatch.Sample.Remote.Cluster.dll"]