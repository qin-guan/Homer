FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Homer.NetDaemon/Homer.NetDaemon.csproj", "Homer.NetDaemon/"]
COPY ["Homer.NetDaemon.Client/Homer.NetDaemon.Client.csproj", "Homer.NetDaemon.Client/"]
COPY ["Homer.ServiceDefaults/Homer.ServiceDefaults.csproj", "Homer.ServiceDefaults/"]
RUN dotnet restore "Homer.NetDaemon/Homer.NetDaemon.csproj"
COPY . .
WORKDIR "/src/Homer.NetDaemon"
RUN dotnet build "Homer.NetDaemon.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Homer.NetDaemon.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Homer.NetDaemon.dll"]
