FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Server/Wolfrender.Highscores.Server.csproj Server/
COPY Source/Features/Highscores/Shared/ Source/Features/Highscores/Shared/
COPY Source/Features/Recording/ Source/Features/Recording/

WORKDIR /src/Server
RUN dotnet restore Wolfrender.Highscores.Server.csproj

COPY Server/ ./
RUN dotnet publish Wolfrender.Highscores.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data

COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:5080 \
    Highscores__FilePath=/data/highscores.json \
    TZ=UTC

EXPOSE 5080

ENTRYPOINT ["dotnet", "Wolfrender.Highscores.Server.dll"]
