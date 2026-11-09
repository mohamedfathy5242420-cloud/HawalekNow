FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source
COPY global.json ./
COPY backend/src/HawalekNow.Domain/*.csproj backend/src/HawalekNow.Domain/packages.lock.json backend/src/HawalekNow.Domain/
COPY backend/src/HawalekNow.Application/*.csproj backend/src/HawalekNow.Application/packages.lock.json backend/src/HawalekNow.Application/
COPY backend/src/HawalekNow.Infrastructure/*.csproj backend/src/HawalekNow.Infrastructure/packages.lock.json backend/src/HawalekNow.Infrastructure/
COPY backend/src/HawalekNow.Api/*.csproj backend/src/HawalekNow.Api/packages.lock.json backend/src/HawalekNow.Api/
RUN test "$(dotnet --version)" = "10.0.401" \
    && dotnet restore backend/src/HawalekNow.Api/HawalekNow.Api.csproj --locked-mode
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/HawalekNow.Api/HawalekNow.Api.csproj \
    --configuration Release --no-restore --no-self-contained --output /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "HawalekNow.Api.dll"]
