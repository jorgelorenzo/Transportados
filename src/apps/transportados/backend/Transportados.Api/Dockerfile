# syntax=docker/dockerfile:1.7
# Build from src/apps/transportados:
# docker build -f backend/Transportados.Api/Dockerfile -t transportados-api .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.1.0
WORKDIR /src

COPY ["backend/Transportados.Api/Transportados.Api.csproj", "backend/Transportados.Api/"]
COPY ["backend/Transportados.Contracts/Transportados.Contracts.csproj", "backend/Transportados.Contracts/"]
COPY ["backend/Transportados.Domain/Transportados.Domain.csproj", "backend/Transportados.Domain/"]
COPY ["backend/Transportados.Persistence/Transportados.Persistence.csproj", "backend/Transportados.Persistence/"]
COPY ["shared/Transportados.Platform.AspNetCore/Transportados.Platform.AspNetCore.csproj", "shared/Transportados.Platform.AspNetCore/"]
COPY ["shared/Transportados.Platform.Core/Transportados.Platform.Core.csproj", "shared/Transportados.Platform.Core/"]
COPY ["shared/Transportados.Platform.EntityFramework/Transportados.Platform.EntityFramework.csproj", "shared/Transportados.Platform.EntityFramework/"]
RUN dotnet restore "./backend/Transportados.Api/Transportados.Api.csproj" --source https://api.nuget.org/v3/index.json

COPY . .
WORKDIR "/src/backend/Transportados.Api"
RUN dotnet build "./Transportados.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore /p:Version=$VERSION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.1.0
RUN dotnet publish "./Transportados.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false /p:Version=$VERSION

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Transportados.Api.dll"]
