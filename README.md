# Visionary Analytics - Video QR Analyzer (MVP)

- .NET 9, RabbitMQ, Worker Service, ZXing.
- Fluxo: Upload -> Mensageria -> Worker extrai frames (1 fps) c/ ffmpeg -> ZXing detecta QR -> results/{id}.json.
- Endpoints:
  - POST /api/video/upload (multipart/form-data) -> { id }
  - GET /api/video/status/{id}
  - GET /api/video/results/{id}

## Rodando local
- dotnet run em src/VideoApi e src/VideoWorker; suba RabbitMQ.

## Docker (Windows Containers)
- Docker Desktop em modo Windows Containers
- docker-compose up -d

## CI
- GitHub Actions (build + test).