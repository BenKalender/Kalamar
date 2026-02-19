FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/GatewayService/GatewayService.csproj", "src/GatewayService/"]
RUN dotnet restore "src/GatewayService/GatewayService.csproj"
COPY . .
RUN dotnet publish "src/GatewayService/GatewayService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GatewayService.dll"]