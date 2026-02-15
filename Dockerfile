# ---- Build Stage ----
FROM golang:1.24-alpine AS builder

RUN apk add --no-cache gcc musl-dev

WORKDIR /app

COPY go.mod go.sum ./
RUN go mod download

COPY . .

# Build with CGO enabled for SQLite support
RUN CGO_ENABLED=1 go build -o /gameserver ./cmd/gameserver

# ---- Runtime Stage ----
FROM alpine:3.21

RUN apk add --no-cache ca-certificates

COPY --from=builder /gameserver /gameserver

EXPOSE 3000

ENTRYPOINT ["/gameserver"]
