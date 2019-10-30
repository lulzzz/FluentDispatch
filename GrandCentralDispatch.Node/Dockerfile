FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 9090

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["GrandCentralDispatch.Monitoring/GrandCentralDispatch.Monitoring.csproj", "GrandCentralDispatch.Monitoring/"]
COPY ["GrandCentralDispatch.Host/GrandCentralDispatch.Host.csproj", "GrandCentralDispatch.Host/"]
COPY ["GrandCentralDispatch.Node/GrandCentralDispatch.Node.csproj", "GrandCentralDispatch.Node/"]
COPY ["GrandCentralDispatch.Contract/GrandCentralDispatch.Contract.csproj", "GrandCentralDispatch.Contract/"]
COPY ["GrandCentralDispatch/GrandCentralDispatch.csproj", "GrandCentralDispatch/"]
COPY ["GrandCentralDispatch.PerformanceCounters/GrandCentralDispatch.PerformanceCounters.csproj", "GrandCentralDispatch.PerformanceCounters/"]
RUN dotnet restore "GrandCentralDispatch.Node/GrandCentralDispatch.Node.csproj" -s https://www.myget.org/F/appmetrics -s https://api.nuget.org/v3/index.json
COPY . .
WORKDIR "/src/GrandCentralDispatch.Node"
RUN dotnet build "GrandCentralDispatch.Node.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GrandCentralDispatch.Node.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GrandCentralDispatch.Node.dll"]