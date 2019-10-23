#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-nanoserver-1903 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-nanoserver-1903 AS build
WORKDIR /src
COPY ["GrandCentralDispatch.Sample.Remote.Node/GrandCentralDispatch.Sample.Remote.Node.csproj", "GrandCentralDispatch.Sample.Remote.Node/"]
COPY ["GrandCentralDispatch.Sample.Remote.Contract/GrandCentralDispatch.Sample.Remote.Contract.csproj", "GrandCentralDispatch.Sample.Remote.Contract/"]
COPY ["GrandCentralDispatch/GrandCentralDispatch.csproj", "GrandCentralDispatch/"]
RUN dotnet restore "GrandCentralDispatch.Sample.Remote.Node/GrandCentralDispatch.Sample.Remote.Node.csproj"
COPY . .
WORKDIR "/src/GrandCentralDispatch.Sample.Remote.Node"
RUN dotnet build "GrandCentralDispatch.Sample.Remote.Node.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GrandCentralDispatch.Sample.Remote.Node.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GrandCentralDispatch.Sample.Remote.Node.dll"]