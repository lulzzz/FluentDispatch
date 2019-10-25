FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 9090

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["GrandCentralDispatch.Sample.Remote.Node/GrandCentralDispatch.Sample.Remote.Node.csproj", "GrandCentralDispatch.Sample.Remote.Node/"]
COPY ["GrandCentralDispatch.Sample.Remote.Contract/GrandCentralDispatch.Sample.Remote.Contract.csproj", "GrandCentralDispatch.Sample.Remote.Contract/"]
COPY ["GrandCentralDispatch/GrandCentralDispatch.csproj", "GrandCentralDispatch/"]
COPY ["GrandCentralDispatch.PerformanceCounters/GrandCentralDispatch.PerformanceCounters.csproj", "GrandCentralDispatch.PerformanceCounters/"]
RUN dotnet restore "GrandCentralDispatch.Sample.Remote.Node/GrandCentralDispatch.Sample.Remote.Node.csproj" -s https://www.myget.org/F/appmetrics -s https://api.nuget.org/v3/index.json
COPY . .
WORKDIR "/src/GrandCentralDispatch.Sample.Remote.Node"
RUN dotnet build "GrandCentralDispatch.Sample.Remote.Node.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GrandCentralDispatch.Sample.Remote.Node.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GrandCentralDispatch.Sample.Remote.Node.dll"]