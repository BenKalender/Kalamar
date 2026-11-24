# Kalamar Mimari Özeti

## Domain
Basit bir **görev yönetimi** sistemi: kullanıcılar oluşturulur ve kullanıcılara görevler atanır.

## Servisler

Hepsi .NET 10. .NET servisleri container içinde 8080'i, frontend (nginx) 80'i dinler.
Parantez içindeki portlar yalnızca `dotnet run` ile tek başına çalıştırırken kullanılır (`launchSettings.json`).

| Servis | Rol | Teknoloji |
|--------|-----|-----------|
| **Frontend** | Kullanıcı arayüzü; nginx statik dosyaları sunar ve `/api/`'yi gateway'e iletir | Blazor WebAssembly, Bootstrap, nginx |
| **Gateway** (8080) | Ters proxy: `/api/users` → User, `/api/tasks` → Task | YARP |
| **User Service** (8081) | Kullanıcı CRUD; veriyi Persistence'a iletir | ASP.NET Core |
| **Task Service** (8083) | Görev CRUD; görev atanırken kullanıcının varlığını User Service'e sorar | ASP.NET Core |
| **Persistence Service** (8082) | Veritabanına erişen tek servis; migration'ları açılışta uygular | EF Core, SQL Server |
| **SQL Server** | `mcr.microsoft.com/mssql/server:2022` | StatefulSet + PVC |

## İstek akışı (k3d cluster)

```
Tarayıcı ── http://kalamar.localhost:8000
   │
   ▼
k3d load balancer (127.0.0.1:8000) ─► Traefik Ingress
   │
   ▼
Frontend (nginx) ── /api/* ─► Gateway (YARP)
                                ├─► User Service ─┐
                                └─► Task Service ─┼─► Persistence Service ─► SQL Server
                                     │            │                          (PVC: local-path)
                                     └─► User Service (kullanıcı var mı?)
```

Tarayıcı her şeyi aynı adresten (origin) çağırdığı için CORS gerekmez.

## Deploy akışı (GitOps)

```
git push (main, src/** değişti)
   │
   ▼
GitHub Actions release.yml
   ├─ CI: build, test, Helm doğrulama
   ├─ 5 image → ghcr.io/benkalender/kalamar-<servis>:<kısa-sha> (+ latest)
   └─ bot commit: helm/kalamar/values-dev.yaml → image.tag: "<kısa-sha>"
   │
   ▼
Argo CD (lokal k3d cluster, ~3 dk'da bir main'i okur)
   root ─► deploy/argocd/apps/
            ├─ sealed-secrets  → kube-system
            └─ kalamar-dev     → helm/kalamar + values-dev.yaml → kalamar-dev namespace
```

- SQL SA şifresi Git'te yalnızca **SealedSecret** olarak durur. Cluster'daki sealed-secrets controller onu normal bir Kubernetes Secret'ına çevirir.
- Cluster'da elle yapılan değişiklikleri Argo CD geri alır (self-heal).

## Önemli dosyalar

| Dosya | Amaç |
|-------|------|
| `deploy/scripts/bootstrap.ps1` | k3d cluster + Argo CD + root app kurulumu |
| `deploy/scripts/seal-sqlserver-secret.ps1` | SQL şifresini mühürleyip `values-dev.yaml`'a yazar |
| `deploy/argocd/` | Argo CD ayarları ve Application tanımları |
| `helm/kalamar/` | Tüm sistemi kuran Helm chart |
| `.github/workflows/` | CI (PR) ve release (main) |
| `src/Frontend/nginx/default.conf.template` | `/api/` → gateway proxy'si |
| `src/GatewayService/appsettings.json` | YARP route'ları |
| `src/*/Controllers/*.cs` | REST endpoint'leri (çoğul route'lar) |
