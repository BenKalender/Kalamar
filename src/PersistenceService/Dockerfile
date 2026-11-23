FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "global.json", "./"]
COPY ["src/PersistenceService/PersistenceService.csproj", "src/PersistenceService/"]
RUN dotnet restore "src/PersistenceService/PersistenceService.csproj"
COPY ["src/PersistenceService/", "src/PersistenceService/"]
RUN dotnet publish "src/PersistenceService/PersistenceService.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# aspnet image'ı varsayılan olarak ASPNETCORE_HTTP_PORTS=8080 ile dinler.
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "PersistenceService.dll"]
