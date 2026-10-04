# Imagem da API para o Render.com (contexto de build: pasta backend/).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RadarTalentos.API/RadarTalentos.API.csproj RadarTalentos.API/
RUN dotnet restore RadarTalentos.API/RadarTalentos.API.csproj
COPY RadarTalentos.API/ RadarTalentos.API/
RUN dotnet publish RadarTalentos.API/RadarTalentos.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# O Render informa a porta em $PORT; 8080 é o padrão local da imagem.
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} dotnet RadarTalentos.API.dll"]
