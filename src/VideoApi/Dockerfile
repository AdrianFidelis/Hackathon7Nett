FROM mcr.microsoft.com/dotnet/sdk:9.0-windowsservercore-ltsc2022 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/VideoApi/VideoApi.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-windowsservercore-ltsc2022
WORKDIR /app
COPY --from=build /app .
EXPOSE 5032
ENV ASPNETCORE_URLS=http://+:5032
ENTRYPOINT ["dotnet", "VideoApi.dll"]