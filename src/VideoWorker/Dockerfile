    FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# build
FROM mcr.microsoft.com/dotnet/sdk:9.0-windowsservercore-ltsc2022 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/VideoWorker/VideoWorker.csproj -c Release -o /app /p:UseAppHost=false

# runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0-windowsservercore-ltsc2022
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "VideoWorker.dll"]