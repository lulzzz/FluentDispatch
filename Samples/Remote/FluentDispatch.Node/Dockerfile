FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 9090

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["FluentDispatch.Monitoring/FluentDispatch.Monitoring.csproj", "FluentDispatch.Monitoring/"]
COPY ["FluentDispatch.Host/FluentDispatch.Host.csproj", "FluentDispatch.Host/"]
COPY ["Samples/Remote/FluentDispatch.Node/FluentDispatch.Node.csproj", "Samples/Remote/FluentDispatch.Node/"]
COPY ["Samples/Remote/FluentDispatch.Contract/FluentDispatch.Contract.csproj", "Samples/Remote/FluentDispatch.Contract/"]
COPY ["FluentDispatch/FluentDispatch.csproj", "FluentDispatch/"]
COPY ["FluentDispatch.PerformanceCounters/FluentDispatch.PerformanceCounters.csproj", "FluentDispatch.PerformanceCounters/"]
RUN dotnet restore "Samples/Remote/FluentDispatch.Node/FluentDispatch.Node.csproj" -s https://www.myget.org/F/appmetrics -s https://api.nuget.org/v3/index.json
COPY . .
WORKDIR "/src/Samples/Remote/FluentDispatch.Node"
RUN dotnet build "FluentDispatch.Node.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FluentDispatch.Node.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FluentDispatch.Node.dll"]