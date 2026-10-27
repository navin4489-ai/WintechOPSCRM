# ---------------------------------------------------------------------------
# ORO POS — single image serving the .NET API and the Angular SPA.
#
# The Angular bundle is committed at web/oro-pos-web/dist/oro-pos-web/browser
# (built with scripts/build-web.ps1). It is copied in rather than rebuilt here
# so the image build stays within Render's free-plan memory limits.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/ ./
RUN dotnet publish ORO_POS.API/ORO_POS.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
# Program.cs serves ./wwwroot as the SPA root and falls back to index.html.
COPY web/oro-pos-web/dist/oro-pos-web/browser/ ./wwwroot/

# Render injects $PORT and expects the app to bind 0.0.0.0:$PORT.
# Expanded at runtime by the shell (a Dockerfile ENV cannot read $PORT).
EXPOSE 10000

CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet ORO_POS.API.dll"]
