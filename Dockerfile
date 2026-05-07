# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS web-build
WORKDIR /src/elite-menu
COPY elite-menu/package*.json ./
RUN npm ci
COPY elite-menu ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-build
WORKDIR /src
COPY . .
COPY --from=web-build /src/EliteRestaurant.Api/wwwroot/menu ./EliteRestaurant.Api/wwwroot/menu
RUN dotnet publish EliteRestaurant.Api/EliteRestaurant.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=dotnet-build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "EliteRestaurant.Api.dll"]
