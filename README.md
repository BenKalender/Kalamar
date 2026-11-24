# Kalamar - Microservice Architecture Example

A small task-management system (users and the tasks assigned to them) built as .NET microservices and deployed with GitOps:
GitHub Actions builds the images, GHCR stores them, and Argo CD deploys the Helm chart to a local k3d cluster.

## Services

| Service | Role |
|---------|------|
| **Frontend** | Blazor WebAssembly UI served by nginx; proxies `/api/` to the gateway |
| **Gateway** | YARP reverse proxy (`/api/users`, `/api/tasks`) |
| **User Service** | User CRUD |
| **Task Service** | Task CRUD; checks with User Service that the assigned user exists |
| **Persistence Service** | The only service with database access (EF Core, migrations on startup) |
| **SQL Server** | `mssql/server:2022`, StatefulSet with a persistent volume |

See [docs/overview.md](docs/overview.md) for the request and deployment flow (Turkish).

## Technology stack

- C# / .NET 10, ASP.NET Core, Blazor WebAssembly, YARP
- Entity Framework Core with SQL Server
- Docker images on GitHub Container Registry (`ghcr.io/benkalender/kalamar-*`)
- Kubernetes: k3d (k3s in Docker) with Traefik ingress
- Helm chart (`helm/kalamar`)
- Argo CD (app-of-apps) and Sealed Secrets

## Run it locally

Requirements: Docker Desktop, [k3d](https://k3d.io), kubectl, helm (and [kubeseal](https://github.com/bitnami/sealed-secrets/releases) to seal a new password).

```powershell
.\deploy\scripts\bootstrap.ps1
```

The script creates the `kalamar` cluster, installs Argo CD and applies the root application; Argo CD then installs everything else from `main`.

- App: http://kalamar.localhost:8000
- Argo CD: http://argocd.localhost:8000 (user `admin`; the script prints the initial password)
- Status: `kubectl get applications -n argocd`
- Remove: `k3d cluster delete kalamar`

## Secrets

No plain-text secret is stored in Git. The SQL Server SA password is committed as a SealedSecret value in
`helm/kalamar/values-dev.yaml`, which only this cluster's sealed-secrets controller can decrypt.

- To set a new password: `.\deploy\scripts\seal-sqlserver-secret.ps1`, then commit `values-dev.yaml` and merge it to `main`.
- `bootstrap.ps1` backs up the controller's private key to `%USERPROFILE%\.kalamar\sealed-secrets-key.yaml` and restores it when the cluster is recreated. Never commit that file.

## Release flow

1. Pull request: `ci.yml` builds and tests the solution, validates the Helm chart and builds all images.
2. Merge to `main` with changes under `src/`: `release.yml` pushes `ghcr.io/benkalender/kalamar-<service>:<short-sha>` and commits the new tag to `values-dev.yaml`.
3. Argo CD picks up the commit (within ~3 minutes) and rolls out the new images.

## Development

```bash
dotnet build Kalamar.sln
dotnet test --solution Kalamar.sln
dotnet tool restore && dotnet ef migrations add <Name> --project src/PersistenceService
```
