# Go Game Server

A production-ready, cloud-scalable game server written in Go. Features player management, game session handling, and configurable database backends — ready to deploy on AWS, GCP, or any cloud provider with Docker.

## Features

- **REST API** for player and game session management
- **Multiple database backends**: SQLite (dev), PostgreSQL (production), MySQL
- **Zero-config local development** — runs with SQLite out of the box
- **Docker & Docker Compose** — one command to run locally or with PostgreSQL
- **AWS deployment ready** — Terraform configs for ECS Fargate with auto-scaling
- **Comprehensive tests** — 47 tests covering services, handlers, middleware, config, and store
- **Game development guide** — step-by-step tutorials for building [chess](#game-development-guide) and [Mechabellum-style RTS](#game-development-guide) servers
- **Drop-in Unity clients** — complete [ChessUnityClient](unity/ChessUnityClient/) and [BattlerUnityClient](unity/BattlerUnityClient/) libraries (drag one script onto a GameObject and play)

## Game Development Guide

Want to build a game-specific server? See **[docs/GAME_DEVELOPMENT_GUIDE.md](docs/GAME_DEVELOPMENT_GUIDE.md)** for complete tutorials:

- **Example 1: Chess Server** — turn-based, HTTP REST, board state management, full move lifecycle
- **Example 2: Mechabellum-Style RTS** — real-time game loop (20 ticks/sec), unit types with different speeds/weapons/trajectories, projectile physics, WebSocket streaming
- **Unity Integration** — C# client examples for both turn-based and real-time games
- **Architecture Patterns** — server-authoritative design where all gameplay runs on the server and Unity is a visual renderer

## Unity Client Libraries

Two drop-in Unity libraries are included. See **[unity/README.md](unity/README.md)** for full documentation.

| Library | Game Type | Protocol | Entry Point |
|---------|-----------|----------|-------------|
| [ChessUnityClient](unity/ChessUnityClient/) | Turn-based chess | HTTP polling | `GameSetup.cs` |
| [BattlerUnityClient](unity/BattlerUnityClient/) | Mechabellum-style RTS | WebSocket streaming | `GameSetup.cs` |

**Quick start:** Copy a client folder into `Assets/`, drag `GameSetup.cs` onto a GameObject, hit Play.
All 3D assets and UI are generated procedurally — replace them with your own prefabs at any time.

## Quick Start

### Option 1: Run Locally (Fastest)

```bash
# Prerequisites: Go 1.21+
go run ./cmd/gameserver
```

That's it! The server starts on `http://localhost:3000` with SQLite. No database setup needed.

### Option 2: Docker (Recommended for Development)

```bash
# SQLite (zero config)
docker compose up gameserver-dev

# PostgreSQL (production-like)
docker compose --profile production up

# MySQL
docker compose --profile mysql up
```

### Option 3: Run with PostgreSQL Locally

```bash
# Start PostgreSQL (via Docker or system install)
docker run -d --name pg -e POSTGRES_USER=gameserver -e POSTGRES_PASSWORD=gameserver -e POSTGRES_DB=gameserver -p 5432:5432 postgres:16-alpine

# Run the server
DB_DRIVER=postgres DB_DSN="host=localhost user=gameserver password=gameserver dbname=gameserver port=5432 sslmode=disable" go run ./cmd/gameserver
```

## Configuration

All configuration is via environment variables with sensible defaults:

| Variable | Default | Description |
|----------|---------|-------------|
| `SERVER_HOST` | `0.0.0.0` | Bind address |
| `SERVER_PORT` | `3000` | HTTP port |
| `DB_DRIVER` | `sqlite` | Database driver: `sqlite`, `postgres`, or `mysql` |
| `DB_DSN` | `gameserver.db` | Database connection string |

### Database Connection Strings

```bash
# SQLite (default — best for local dev)
DB_DRIVER=sqlite
DB_DSN=gameserver.db

# PostgreSQL (recommended for production)
DB_DRIVER=postgres
DB_DSN="host=localhost user=gameserver password=secret dbname=gameserver port=5432 sslmode=disable"

# MySQL
DB_DRIVER=mysql
DB_DSN="gameserver:secret@tcp(localhost:3306)/gameserver?charset=utf8mb4&parseTime=True&loc=Local"
```

## API Reference

### Health Check

```
GET /api/health
```

Response: `{"status": "ok"}`

### Players

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/players` | List all players |
| `POST` | `/api/players` | Create a player |
| `GET` | `/api/players/{id}` | Get a player by ID |
| `PUT` | `/api/players/{id}` | Update a player |
| `DELETE` | `/api/players/{id}` | Delete a player |

**Create Player:**
```bash
curl -X POST http://localhost:3000/api/players \
  -H 'Content-Type: application/json' \
  -d '{"uid": "player-1", "name": "Alice"}'
```

Response:
```json
{
  "id": 1,
  "uid": "player-1",
  "name": "Alice",
  "num_sessions": 0,
  "created_at": "2025-01-01T00:00:00Z",
  "updated_at": "2025-01-01T00:00:00Z"
}
```

**Update Player:**
```bash
curl -X PUT http://localhost:3000/api/players/1 \
  -H 'Content-Type: application/json' \
  -d '{"name": "Alice Updated"}'
```

### Game Sessions

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/sessions` | List all sessions |
| `POST` | `/api/sessions` | Create a session |
| `GET` | `/api/sessions/{session_id}` | Get a session |
| `POST` | `/api/sessions/{session_id}/join` | Add a player to a session |
| `PUT` | `/api/sessions/{session_id}/status` | Update session status |
| `DELETE` | `/api/sessions/{session_id}` | Delete a session |

**Create Session:**
```bash
curl -X POST http://localhost:3000/api/sessions \
  -H 'Content-Type: application/json' \
  -d '{"session_id": "match-001", "max_players": 4}'
```

**Join Session:**
```bash
curl -X POST http://localhost:3000/api/sessions/match-001/join \
  -H 'Content-Type: application/json' \
  -d '{"player_id": 1}'
```

**Update Status** (`waiting` → `active` → `finished`):
```bash
curl -X PUT http://localhost:3000/api/sessions/match-001/status \
  -H 'Content-Type: application/json' \
  -d '{"status": "active"}'
```

## Testing

```bash
# Run all tests
go test ./...

# Run with verbose output
go test -v ./...

# Run specific package tests
go test -v ./internal/service/
go test -v ./internal/handler/

# Run with coverage
go test -cover ./...
```

## Project Structure

```
.
├── cmd/gameserver/       # Application entry point
│   └── main.go
├── internal/
│   ├── config/           # Environment-based configuration
│   ├── handler/          # HTTP handlers and route registration
│   ├── middleware/        # HTTP middleware (logging, JSON content-type)
│   ├── model/            # Data models (Player, GameSession)
│   ├── service/          # Business logic layer
│   └── store/            # Database connection and migrations
├── unity/
│   ├── ChessUnityClient/ # Drop-in Unity chess client library
│   ├── BattlerUnityClient/ # Drop-in Unity battler client library
│   └── README.md         # Unity client documentation
├── deploy/
│   └── aws/              # Terraform configs for AWS ECS Fargate
├── docs/
│   └── GAME_DEVELOPMENT_GUIDE.md  # Tutorials for chess & RTS game servers
├── main.go               # Root entry point (same as cmd/gameserver)
├── Dockerfile            # Multi-stage Docker build
├── docker-compose.yml    # Dev and production compose configs
└── README.md
```

## Cloud Deployment Tutorial

### Deploying to AWS (ECS Fargate + RDS PostgreSQL)

This guide walks you through deploying the game server to AWS with auto-scaling. The included Terraform configuration creates:

- **VPC** with public/private subnets across 2 availability zones
- **ECS Fargate** cluster running your game server containers
- **RDS PostgreSQL** database in private subnets
- **Application Load Balancer** for traffic distribution
- **Auto-scaling** based on CPU usage and request count

#### Prerequisites

1. [AWS CLI](https://aws.amazon.com/cli/) configured with your credentials
2. [Terraform](https://www.terraform.io/downloads) v1.0+
3. [Docker](https://docs.docker.com/get-docker/) installed

#### Step 1: Build and Push Docker Image

```bash
# Create an ECR repository
aws ecr create-repository --repository-name gameserver --region us-east-1

# Get your AWS account ID
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)

# Login to ECR
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin ${AWS_ACCOUNT_ID}.dkr.ecr.us-east-1.amazonaws.com

# Build and push
docker build -t gameserver .
docker tag gameserver:latest ${AWS_ACCOUNT_ID}.dkr.ecr.us-east-1.amazonaws.com/gameserver:latest
docker push ${AWS_ACCOUNT_ID}.dkr.ecr.us-east-1.amazonaws.com/gameserver:latest
```

#### Step 2: Configure Terraform

```bash
cd deploy/aws

# Copy the example variables file
cp terraform.tfvars.example terraform.tfvars

# Edit with your values
# - Set container_image to your ECR image URI
# - Set db_password to a strong password
# - Adjust desired_count, cpu, memory as needed
```

#### Step 3: Deploy

```bash
terraform init
terraform plan     # Review what will be created
terraform apply    # Deploy (takes ~10 minutes)
```

Terraform will output the ALB DNS name. Your API is available at:
```
http://<alb-dns>/api/health
```

#### Step 4: Verify

```bash
# Get the ALB DNS from Terraform output
ALB_DNS=$(terraform output -raw alb_dns)

# Health check
curl http://${ALB_DNS}/api/health

# Create a player
curl -X POST http://${ALB_DNS}/api/players \
  -H 'Content-Type: application/json' \
  -d '{"uid": "cloud-player-1", "name": "CloudGamer"}'
```

#### Step 5: Scale

The auto-scaling is pre-configured:
- **Scales out** when CPU > 70% or > 1000 requests/target
- **Scales in** after 5 minutes of low load
- **Minimum**: 2 tasks (set via `desired_count`)
- **Maximum**: 10 tasks

To adjust scaling manually:
```bash
# Update desired_count in terraform.tfvars
desired_count = 5

# Apply
terraform apply
```

#### Cleanup

```bash
terraform destroy
```

### Deploying to Other Cloud Providers

#### Google Cloud Run

```bash
# Build and push to Google Container Registry
gcloud builds submit --tag gcr.io/YOUR_PROJECT/gameserver

# Deploy to Cloud Run
gcloud run deploy gameserver \
  --image gcr.io/YOUR_PROJECT/gameserver \
  --port 3000 \
  --set-env-vars "DB_DRIVER=postgres,DB_DSN=host=/cloudsql/PROJECT:REGION:INSTANCE user=gameserver password=secret dbname=gameserver" \
  --add-cloudsql-instances PROJECT:REGION:INSTANCE \
  --allow-unauthenticated
```

#### Azure Container Apps

```bash
# Create resource group
az group create --name gameserver-rg --location eastus

# Create container app environment
az containerapp env create --name gameserver-env --resource-group gameserver-rg --location eastus

# Deploy
az containerapp create \
  --name gameserver \
  --resource-group gameserver-rg \
  --environment gameserver-env \
  --image YOUR_REGISTRY/gameserver:latest \
  --target-port 3000 \
  --env-vars DB_DRIVER=postgres DB_DSN="your-connection-string" \
  --min-replicas 1 \
  --max-replicas 10
```

#### DigitalOcean App Platform

```yaml
# app.yaml
name: gameserver
services:
  - name: gameserver
    image:
      registry_type: DOCR
      repository: gameserver
      tag: latest
    envs:
      - key: DB_DRIVER
        value: postgres
      - key: DB_DSN
        value: ${db.DATABASE_URL}
    instance_count: 2
    instance_size_slug: basic-xxs
databases:
  - name: db
    engine: PG
    version: "16"
```

## Best Practices Used

- **SQLite for development** — zero setup, embedded, fast iteration
- **PostgreSQL for production** — battle-tested, scalable, cloud-native
- **Environment-based configuration** — follows [12-factor app](https://12factor.net/) methodology
- **Multi-stage Docker build** — minimal ~15MB production image
- **Proper Go project layout** — follows [golang-standards/project-layout](https://github.com/golang-standards/project-layout)
- **Service layer pattern** — business logic separated from HTTP handlers
- **Soft deletes** — data is never permanently lost
- **Health check endpoint** — required for load balancer and container orchestration
- **Auto-migrations** — schema changes applied automatically on startup
- **Comprehensive tests** — unit tests, integration tests, HTTP round-trip tests

## License

MIT
