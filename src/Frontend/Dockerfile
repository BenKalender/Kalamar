FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Frontend/Frontend.csproj", "src/Frontend/"]
RUN dotnet restore "src/Frontend/Frontend.csproj"
COPY . .
RUN dotnet publish "src/Frontend/Frontend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html
EXPOSE 80
COPY --from=build /app/publish/wwwroot .
COPY src/Frontend/nginx.conf /etc/nginx/conf.d/default.conf