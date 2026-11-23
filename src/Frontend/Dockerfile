FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "global.json", "./"]
COPY ["src/Frontend/Frontend.csproj", "src/Frontend/"]
RUN dotnet restore "src/Frontend/Frontend.csproj"
COPY ["src/Frontend/", "src/Frontend/"]
RUN dotnet publish "src/Frontend/Frontend.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html
EXPOSE 80
# /api/ isteklerinin gideceği gateway adresi; Helm chart bunu kendi servis adıyla ezer.
ENV API_UPSTREAM=http://gateway-service:8080
COPY --from=build /app/publish/wwwroot .
COPY src/Frontend/nginx/default.conf.template /etc/nginx/templates/default.conf.template
