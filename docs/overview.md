# Kalamar Mikroservis Mimarisi Özeti

## 🎯 Domain
Basit bir **Task Management (Görev Yönetim)** sistemi — kullanıcılar oluşturulur ve kullanıcılara görevler atanır.

---

## 🏗️ Katmanlı Mimari (5 Servis)

| Servis | Rol | Teknoloji | Port |
|--------|-----|-----------|------|
| **Frontend** | Blazor WebAssembly SPA — kullanıcı arayüzü | .NET 10, Bootstrap | 8084 |
| **API Gateway** | Ters proxy — istekleri ilgili servislere yönlendirir | .NET 8, YARP | 8080 |
| **User Service** | Kullanıcı CRUD işlemleri | .NET 8, REST API | 8081 |
| **Task Service** | Görev CRUD işlemleri | .NET 8, REST API | 8083 |
| **Persistence Service** | Veritabanı erişim katmanı (EF Core) | .NET 8, SQL Server | 8082 |

---

## 🔗 İletişim Akışı

```
Browser ─┬─► Frontend (8084) ─┐
         │                     │
         └─► Gateway (8080) ◄──┘
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
   User (8081)  Task (8083)  Persistence (8082)
        │           │                │
        └───────────┴────────────────┘
                      │
                   SQL Server (1433)
```

---

## 🐳 Docker Compose Yapısı

```yaml
Services: 6 (sqlserver + persistence + user + task + gateway + frontend)
Network:  Docker bridge (servisler birbirini DNS ismiyle görür)
Volumes:  sqlserver-data (DB kalıcılığı)
```

---

## 📁 Önemli Dosyalar

| Dosya | Amaç |
|-------|------|
| `src/Frontend/nginx.conf` | Reverse proxy (same-origin) |
| `src/GatewayService/Program.cs` | CORS + YARP yapılandırması |
| `src/*/Controllers/*.cs` | REST endpoint'leri (çoğul route) |
| `helm/kalamar/templates/*.yaml` | Kubernetes deployment şablonları |
| `.env` / `docker-compose.override.yml` | Yerel secret yönetimi |

