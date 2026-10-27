# Imagen de la API (T10, RNF-16). El contexto es la RAÍZ del repositorio (la API referencia Application e Infrastructure):
#   docker build -f src/CoreBancario.Api/Dockerfile .
# Multietapa: el SDK compila y publica; la imagen final solo lleva el runtime de ASP.NET.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/ src/
# Sin generar el contrato OpenAPI en la imagen: ya está versionado en Git y el CI comprueba que está al día (ADR-0019).
RUN dotnet publish src/CoreBancario.Api/CoreBancario.Api.csproj -c Release -o /app/publish -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Puerto 8080 (el predeterminado de las imágenes de .NET 8+) y sin root.
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CoreBancario.Api.dll"]
