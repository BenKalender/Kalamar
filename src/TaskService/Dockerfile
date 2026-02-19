FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/TaskService/TaskService.csproj", "src/TaskService/"]
RUN dotnet restore "src/TaskService/TaskService.csproj"
COPY . .
RUN dotnet publish "src/TaskService/TaskService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8083
ENV ASPNETCORE_URLS=http://+:8083
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TaskService.dll"]