FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./src/VideoApi/VideoApi.csproj
RUN dotnet publish ./src/VideoApi/VideoApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /app/uploads /app/results /app/status
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VideoApi.dll"]