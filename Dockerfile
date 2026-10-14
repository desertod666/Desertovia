# syntax=docker/dockerfile:1

FROM node:26-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
# Vite writes the bundle to ../src/Desertod.Api/wwwroot (see vite.config.ts).
ARG VITE_APP_BASE=/
ENV VITE_APP_BASE=$VITE_APP_BASE
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Desertod.Domain/Desertod.Domain.csproj src/Desertod.Domain/
COPY src/Desertod.Infrastructure/Desertod.Infrastructure.csproj src/Desertod.Infrastructure/
COPY src/Desertod.Api/Desertod.Api.csproj src/Desertod.Api/
RUN dotnet restore src/Desertod.Api/Desertod.Api.csproj
COPY src/ src/
COPY --from=web /src/src/Desertod.Api/wwwroot src/Desertod.Api/wwwroot
RUN dotnet publish src/Desertod.Api/Desertod.Api.csproj -c Release -o /out/api --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
RUN mkdir -p /keys && chown $APP_UID /keys
COPY --from=build /out/api .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Desertod.Api.dll"]
