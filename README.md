    # Video QR Processor (API + Worker)

## Vis�o geral
- API recebe upload de v�deos e publica mensagens no RabbitMQ.
- Worker consome, extrai frames com `ffmpeg` e tenta ler QR Codes (ZXing).
- Resultados e status s�o expostos via arquivos JSON e endpoints.

## Arquitetura
- API: ASP.NET Core (.NET 9)
- Worker: .NET Worker Service (`BackgroundService`)
- Fila: RabbitMQ
- Depend�ncias: `ffmpeg`, ZXing

## Requisitos
- .NET 9 SDK
- RabbitMQ (ou via Docker Compose)
- ffmpeg no PATH (para execu��o local do Worker)
- Windows para leitura de QR (limita��o do `System.Drawing` + `ZXing.Windows.Compatibility`)

## Como executar (docker-compose)