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

Everything runs on one Windows machine. Docker Desktop runs a k3d cluster, and Argo CD inside that cluster deploys the app from this repo's `main` branch.

### 1. Install the prerequisites

Run the commands in PowerShell. After installing, **open a new terminal** so that `PATH` is refreshed.

| Tool | Needed for | Install | Verify |
|------|------------|---------|--------|
| WSL 2 | Docker Desktop backend | `wsl --install` (then reboot) | `wsl --version` |
| Docker Desktop | runs the cluster's nodes as containers | `winget install -e --id Docker.DockerDesktop` | `docker version` shows both Client and Server |
| k3d | creates the k3s cluster in Docker | `winget install -e --id k3d.k3d` | `k3d version` |
| kubectl | talks to the cluster | `winget install -e --id Kubernetes.kubectl` | `kubectl version --client` |
| Helm | installs Argo CD | `winget install -e --id Helm.Helm` | `helm version` |
| Git | cloning the repo | `winget install -e --id Git.Git` | `git --version` |
| kubeseal | only to seal a new SQL password | download `kubeseal-<version>-windows-amd64.tar.gz` from [releases](https://github.com/bitnami/sealed-secrets/releases) and put `kubeseal.exe` in a folder on `PATH` | `kubeseal --version` |
| .NET 10 SDK | only for building or running services outside the cluster | `winget install -e --id Microsoft.DotNet.SDK.10` | `dotnet --version` |

Tested with: Docker Engine 29.9, k3d 5.9.0, kubectl 1.36, Helm 4.1, kubeseal 0.41.2.

Docker Desktop setup:
- Start Docker Desktop and wait until it shows *Engine running*.
- Make sure Docker has **at least 8 GB of memory**. With dev and prod running, the cluster uses about 5 GB.
- Docker Desktop's own Kubernetes is not needed; you can leave it disabled. If it is enabled, it can switch your kubectl context to `docker-desktop`. Switch back with `kubectl config use-context k3d-kalamar`.

### 2. Install the cluster

```powershell
git clone https://github.com/BenKalender/Kalamar.git
cd Kalamar
.\deploy\scripts\bootstrap.ps1
# If script execution is blocked:
# powershell -ExecutionPolicy Bypass -File .\deploy\scripts\bootstrap.ps1
```

The script checks the tools and then installs the following, in order. Everything after Argo CD is installed by Argo CD from Git, not by the script.

| # | Component | Where | Installed by |
|---|-----------|-------|--------------|
| 1 | k3d cluster `kalamar` (server, agent and load-balancer containers), with Traefik ingress and local-path storage | Docker | `bootstrap.ps1` (`deploy/k3d/cluster.yaml`) |
| 2 | Sealed Secrets key restored from `%USERPROFILE%\.kalamar\`, if a backup exists | `kube-system` | `bootstrap.ps1` |
| 3 | Argo CD | `argocd` | `bootstrap.ps1` (Helm) |
| 4 | `root` application (app-of-apps), which reads `deploy/argocd/apps/` | `argocd` | `bootstrap.ps1` (`kubectl apply`) |
| 5 | `sealed-secrets` controller | `kube-system` | Argo CD (sync wave -1) |
| 6 | `kalamar-dev` and `kalamar-prod`, each deployed in three sync waves: SealedSecret, then SQL Server with its volume, then the 5 services | `kalamar-dev`, `kalamar-prod` | Argo CD (waves -2, -1, 0) |

At the end, the script prints the URLs and the Argo CD `admin` password. The first run takes several minutes because the images are downloaded.

### 3. Verify the installation

```powershell
kubectl config current-context                    # k3d-kalamar
kubectl get nodes                                 # 2 nodes, Ready
kubectl get pods -n argocd                        # all 1/1 Running
kubectl get applications -n argocd                # root, sealed-secrets, kalamar-dev, kalamar-prod: Synced / Healthy
kubectl get pods -n kube-system -l app.kubernetes.io/name=sealed-secrets   # 1/1 Running
kubectl get sealedsecret -n kalamar-dev           # SYNCED True: the password was decrypted
kubectl get pods,pvc -n kalamar-dev               # 6 pods Running, PVC Bound (same for kalamar-prod)
```

Then open:

| URL | What |
|-----|------|
| http://kalamar.localhost:8000 | dev: every build of `main` |
| http://kalamar-prod.localhost:8000 | prod: only released versions (`vX.Y.Z` tags) |
| http://argocd.localhost:8000 | Argo CD UI; user `admin` |

To read the Argo CD password again:

```powershell
$p = kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}'
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p))
```

After 5 failed logins, Argo CD locks the account for a few minutes.

### 4. Pause, stop or remove

You can stop things at different levels. Every level except the last two keeps the databases.

| Level | Stop | Resume | What keeps running |
|-------|------|--------|--------------------|
| One application: Argo CD stops syncing it | `kubectl annotate application kalamar-prod -n argocd argocd.argoproj.io/skip-reconcile=true` | `kubectl annotate application kalamar-prod -n argocd argocd.argoproj.io/skip-reconcile-` | The app's pods; Argo CD does not sync or self-heal it |
| One application and its pods | First add the annotation above, then `kubectl scale deploy,statefulset --all -n kalamar-prod --replicas=0` | Remove the annotation; Argo CD restores the replicas from Git (about 20 s) | Everything else |
| Argo CD | `kubectl scale deploy,statefulset --all -n argocd --replicas=0` | `kubectl scale deploy,statefulset --all -n argocd --replicas=1` (or re-run `bootstrap.ps1`) | All apps; nothing syncs, and manual changes stay until Argo CD wakes up |
| The whole cluster | `k3d cluster stop kalamar` | `k3d cluster start kalamar` (or re-run `bootstrap.ps1`) | Nothing; frees about 5 GB of memory |
| One environment, permanently | Delete `deploy/argocd/apps/kalamar-prod.yaml` and merge, then `kubectl delete namespace kalamar-prod` | Restore the file and merge | Other environments |
| Everything | `k3d cluster delete kalamar` | See [Reinstall from scratch](#5-reinstall-from-scratch) | Nothing |

Notes:
- **Why not just turn off auto-sync?** `kubectl patch` on a child application, such as removing `syncPolicy.automated`, is reverted by `root` within seconds. Child applications are defined in Git, and `root` self-heals them back. The `skip-reconcile` annotation is not part of those definitions, so it stays. To turn off auto-sync permanently, change the file in Git.
- While an application is suspended, its status in the Argo CD UI is frozen at its last value.
- After `k3d cluster start`, allow 2–5 minutes. Until the Argo CD pods are Ready, the UI returns 503 and the application statuses are stale. Check with `kubectl get pods -n argocd`.
- **Startup with Windows:** a running cluster starts again with Docker Desktop. A cluster you stopped with `k3d cluster stop` stays stopped until you run `k3d cluster start`.
- Deleting an application's file from Git removes the Argo CD application but not its resources, because the applications have no deletion finalizer. That is why the namespace is deleted by hand. Deleting the namespace also deletes the PVC and its data.

**Removing everything:**

```powershell
k3d cluster delete kalamar          # cluster, Argo CD, both databases and the kubectl context
```

What remains on the machine:
- `%USERPROFILE%\.kalamar\sealed-secrets-key.yaml`: the Sealed Secrets private key. Keep it if you want the sealed passwords in Git to keep working. Delete it with `Remove-Item -Recurse $env:USERPROFILE\.kalamar` only if you plan to re-seal (see B below).
- The k3s image in Docker. Remove it with `docker image rm rancher/k3s:v1.35.10-k3s1`.
- The Helm repo entry. Remove it with `helm repo remove argo`.
- The tools. Remove them with `winget uninstall --id <id>`, using the IDs from the table above.

### 5. Reinstall from scratch

**A. Same machine, key backup kept** (the usual case):

```powershell
.\deploy\scripts\bootstrap.ps1
```

The script restores the Sealed Secrets key, so the sealed passwords in Git still work. Argo CD brings everything back from `main`. The databases start empty; migrations recreate the schema.

**B. New machine, or the key backup was deleted:** the new cluster gets a new key, and the sealed passwords in Git can no longer be decrypted. The SealedSecrets fail, and SQL Server and Persistence wait for the missing Secret.
1. Run `.\deploy\scripts\bootstrap.ps1`. It creates the cluster and backs up the new key.
2. On a branch, seal a new password for each environment:
   ```powershell
   .\deploy\scripts\seal-sqlserver-secret.ps1 -Environment dev
   .\deploy\scripts\seal-sqlserver-secret.ps1 -Environment prod
   ```
3. Commit `helm/kalamar/values-dev.yaml` and `values-prod.yaml`, then open a PR and merge it. Argo CD deploys both environments within about 3 minutes.

## Secrets

No plain-text secret is stored in Git. Each environment's SQL Server SA password is committed as a SealedSecret value in
`helm/kalamar/values-<env>.yaml`, which only this cluster's sealed-secrets controller can decrypt.

- To set a new password: `.\deploy\scripts\seal-sqlserver-secret.ps1 -Environment dev` (or `prod`), then commit the values file and merge it to `main`.
- `bootstrap.ps1` backs up the controller's private key to `%USERPROFILE%\.kalamar\sealed-secrets-key.yaml` and restores it when the cluster is recreated. Never commit that file. Anyone who has it can decrypt every sealed value in this repo.

## Environments

| Environment | Namespace | Values file | Updated by |
|-------------|-----------|-------------|------------|
| dev | `kalamar-dev` | `values-dev.yaml` | `release.yml`, on every merge to `main` that changes `src/` |
| prod | `kalamar-prod` | `values-prod.yaml` | `promote.yml`, when a `vX.Y.Z` tag is pushed |

Both run in the same local cluster, each with its own SQL Server and data.

## Release flow

1. Pull request: `ci.yml` builds and tests the solution, validates the Helm chart and builds all images.
2. Merge to `main` with changes under `src/`: `release.yml` pushes `ghcr.io/benkalender/kalamar-<service>:<short-sha>` and commits the new tag to `values-dev.yaml`.
3. Argo CD picks up the commit (within ~3 minutes) and rolls out the new images to dev.
4. To release what is running in dev, tag `main` after the `chore(dev): deploy ...` commit and push the tag:
   ```powershell
   git pull; git tag -a v0.8.0 -m "..."; git push origin v0.8.0
   ```
   `promote.yml` adds the `0.8.0` tag to the same images (no rebuild), commits it to `values-prod.yaml` and creates a GitHub Release. Argo CD then updates prod.
5. Rollback: Actions → Promote → Run workflow with an earlier tag (e.g. `v0.7.0`).

## Development

```bash
dotnet build Kalamar.sln
dotnet test --solution Kalamar.sln
dotnet tool restore && dotnet ef migrations add <Name> --project src/PersistenceService
```
