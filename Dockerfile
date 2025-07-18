# ========================================
# Stage 1: Build NextJS Frontend
# ========================================
FROM --platform=$BUILDPLATFORM node:18-alpine AS frontend-build

WORKDIR /app/frontend
COPY src/Presentation/loqi-web/package*.json ./
RUN npm ci
COPY src/Presentation/loqi-web/ ./
RUN npm run build

# ========================================
# Stage 2: Build .NET Backend
# ========================================
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build

WORKDIR /app
COPY LoQi.sln ./
COPY src/Core/LoQi.Domain/*.csproj src/Core/LoQi.Domain/
COPY src/Core/LoQi.Application/*.csproj src/Core/LoQi.Application/
COPY src/Infrastructure/LoQi.Infrastructure/*.csproj src/Infrastructure/LoQi.Infrastructure/
COPY src/Persistence/LoQi.Persistence/*.csproj src/Persistence/LoQi.Persistence/
COPY src/Presentation/LoQi.API/*.csproj src/Presentation/LoQi.API/

RUN dotnet restore

COPY src/ src/
RUN dotnet publish src/Presentation/LoQi.API/LoQi.API.csproj \
    -c Release \
    -o /app/publish

# ========================================
# Stage 3: Runtime Image
# ========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy .NET application
COPY --from=backend-build /app/publish ./

# Copy NextJS static files
COPY --from=frontend-build /app/frontend/out ./wwwroot/

# Expose port
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Sadece uygulamayı başlat - DB init işi docker-compose'da
ENTRYPOINT ["dotnet", "LoQi.API.dll"]