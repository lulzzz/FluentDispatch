FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 5432

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["GrandCentralDispatch.Cluster/GrandCentralDispatch.Cluster.csproj", "GrandCentralDispatch.Cluster/"]
COPY ["GrandCentralDispatch.Contract/GrandCentralDispatch.Contract.csproj", "GrandCentralDispatch.Contract/"]
COPY ["GrandCentralDispatch/GrandCentralDispatch.csproj", "GrandCentralDispatch/"]
COPY ["GrandCentralDispatch.PerformanceCounters/GrandCentralDispatch.PerformanceCounters.csproj", "GrandCentralDispatch.PerformanceCounters/"]
COPY ["GrandCentralDispatch.Monitoring/GrandCentralDispatch.Monitoring.csproj", "GrandCentralDispatch.Monitoring/"]
RUN dotnet restore "GrandCentralDispatch.Cluster/GrandCentralDispatch.Cluster.csproj" -s https://www.myget.org/F/appmetrics -s https://api.nuget.org/v3/index.json
COPY . .
WORKDIR "/src/GrandCentralDispatch.Cluster"
RUN dotnet build "GrandCentralDispatch.Cluster.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GrandCentralDispatch.Cluster.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GrandCentralDispatch.Cluster.dll"]