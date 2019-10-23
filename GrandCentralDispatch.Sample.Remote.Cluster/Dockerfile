#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-nanoserver-1903 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-nanoserver-1903 AS build
WORKDIR /src
COPY ["GrandCentralDispatch.Sample.Remote.Cluster/GrandCentralDispatch.Sample.Remote.Cluster.csproj", "GrandCentralDispatch.Sample.Remote.Cluster/"]
COPY ["GrandCentralDispatch.Sample.Remote.Contract/GrandCentralDispatch.Sample.Remote.Contract.csproj", "GrandCentralDispatch.Sample.Remote.Contract/"]
COPY ["GrandCentralDispatch/GrandCentralDispatch.csproj", "GrandCentralDispatch/"]
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