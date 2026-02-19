# Kalamar - Microservice Architecture Example

This project demonstrates a microservice architecture using C#, Docker, Kubernetes (micro8s), Helm charts, and Argo CD for GitOps deployment.

## Architecture Overview

The Kalamar system consists of the following microservices:
1. **Frontend Service** - Blazor WebAssembly application
2. **API Gateway** - Routes requests to appropriate services
3. **User Service** - Manages user data
4. **Task Service** - Manages tasks
5. **Persistence Service** - Handles database operations

## Technology Stack
- C# .NET 8
- Entity Framework Core with PostgreSQL
- Docker
- Kubernetes (micro8s)
- Helm charts
- Argo CD for GitOps

## Secrets & GitHub Safety
Before pushing to GitHub, use env files for credentials and keep them out of git.

### Docker Compose
1. Copy `.env.example` to `.env` and set a strong password.
2. (Optional) Copy `docker-compose.override.example.yml` to `docker-compose.override.yml`.

```bash
copy .env.example .env
copy docker-compose.override.example.yml docker-compose.override.yml
```

### Helm
Set a real password via a local override file:

```bash
copy helm\kalamar\values.yaml helm\kalamar\values.local.yaml
```

Then edit `values.local.yaml` and set `sqlserver.saPassword`. This file is gitignored.