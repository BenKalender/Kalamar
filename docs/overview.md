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
git push (main, src/** değişti)                git push origin vX.Y.Z (sürüm tag'i)
   │                                               │
   ▼                                               ▼
GitHub Actions release.yml                     GitHub Actions promote.yml
   ├─ CI: build, test, Helm doğrulama             ├─ tag'teki values-dev.yaml → dev'de çalışan <kısa-sha>
   ├─ 5 image → kalamar-<servis>:<kısa-sha>       ├─ aynı image'a ek etiket: <kısa-sha> → X.Y.Z (build yok)
   └─ bot commit: values-dev.yaml                 ├─ bot commit: values-prod.yaml → image.tag: "X.Y.Z"
        → image.tag: "<kısa-sha>"                  └─ GitHub Release
   │                                               │
   └───────────────────────┬───────────────────────┘
                           ▼
Argo CD (lokal k3d cluster, ~3 dk'da bir main'i okur)
   root ─► deploy/argocd/apps/
            ├─ sealed-secrets  → kube-system
            ├─ kalamar-dev     → helm/kalamar + values-dev.yaml  → kalamar-dev namespace  (kalamar.localhost)
            └─ kalamar-prod    → helm/kalamar + values-prod.yaml → kalamar-prod namespace (kalamar-prod.localhost)
```

- **Dev** her main build'ini alır. **Prod** yalnızca sürüm tag'i atıldığında güncellenir ve dev'de test edilmiş image'ın aynısını kullanır.
- Promote, tag'teki kod dev'deki image'la aynı değilse durur. Bu durumda release'in bot commit'ini bekleyip tag'i onun üzerine atmak gerekir.
- Bir sürüm etiketi (`0.7.0` gibi) bir kez verildikten sonra başka bir image'a taşınamaz. Geri dönmek için Actions → Promote → Run workflow ile eski tag seçilir.
- Her ortamın kendi SQL Server'ı, PVC'si ve şifresi vardır.
- SQL SA şifreleri Git'te yalnızca **SealedSecret** olarak durur. Cluster'daki sealed-secrets controller onları normal Kubernetes Secret'larına çevirir.
- Cluster'da elle yapılan değişiklikleri Argo CD geri alır (self-heal).

## Önemli dosyalar

| Dosya | Amaç |
|-------|------|
| `deploy/scripts/bootstrap.ps1` | k3d cluster + Argo CD + root app kurulumu |
| `deploy/scripts/seal-sqlserver-secret.ps1` | SQL şifresini mühürleyip `values-<ortam>.yaml`'a yazar (`-Environment dev\|prod`) |
| `deploy/argocd/` | Argo CD ayarları ve Application tanımları |
| `helm/kalamar/` | Tüm sistemi kuran Helm chart |
| `.github/workflows/` | CI (PR), release (main → dev) ve promote (sürüm tag'i → prod) |
| `src/Frontend/nginx/default.conf.template` | `/api/` → gateway proxy'si |
| `src/GatewayService/appsettings.json` | YARP route'ları |
| `src/*/Controllers/*.cs` | REST endpoint'leri (çoğul route'lar) |
