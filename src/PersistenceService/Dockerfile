FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PersistenceService/PersistenceService.csproj", "src/PersistenceService/"]
RUN dotnet restore "src/PersistenceService/PersistenceService.csproj"
COPY . .
RUN dotnet publish "src/PersistenceService/PersistenceService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8082
ENV ASPNETCORE_URLS=http://+:8082
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersistenceService.dll"]