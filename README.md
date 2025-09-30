    # Video QR Processor (API + Worker)

## Visão geral
- API recebe upload de vídeos e publica mensagens no RabbitMQ.
- Worker consome, extrai frames com `ffmpeg` e tenta ler QR Codes (ZXing).
- Resultados e status são expostos via arquivos JSON e endpoints.

## Arquitetura
- API: ASP.NET Core (.NET 9)
- Worker: .NET Worker Service (`BackgroundService`)
- Fila: RabbitMQ
- Dependências: `ffmpeg`, ZXing

## Requisitos
- .NET 9 SDK
- RabbitMQ (ou via Docker Compose)
- ffmpeg no PATH (para execução local do Worker)
- Windows para leitura de QR (limitação do `System.Drawing` + `ZXing.Windows.Compatibility`)

## Como executar (docker-compose)