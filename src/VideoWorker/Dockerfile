    FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./src/VideoWorker/VideoWorker.csproj
RUN dotnet publish ./src/VideoWorker/VideoWorker.csproj -c Release -o /app/publish

# runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
# ffmpeg para extração de frames
RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*
# diretórios compartilhados
RUN mkdir -p /app/uploads /app/results /app/status
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VideoWorker.dll"]

# Alternativa Windows (para processamento real em container):
# FROM mcr.microsoft.com/dotnet/runtime:9.0-windowsservercore-ltsc2022 AS final
# WORKDIR /app
# RUN powershell -Command \
#   Set-ExecutionPolicy Bypass -Scope Process -Force; \
#   iwr https://chocolatey.org/install.ps1 -UseBasicParsing | iex; \
#   choco install -y ffmpeg
# RUN mkdir uploads results status
# COPY --from=build /app/publish .
# ENTRYPOINT ["dotnet", "VideoWorker.dll"]