FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "global.json", "./"]
COPY ["src/GatewayService/GatewayService.csproj", "src/GatewayService/"]
RUN dotnet restore "src/GatewayService/GatewayService.csproj"
COPY ["src/GatewayService/", "src/GatewayService/"]
RUN dotnet publish "src/GatewayService/GatewayService.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# aspnet image'ı varsayılan olarak ASPNETCORE_HTTP_PORTS=8080 ile dinler.
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "GatewayService.dll"]
