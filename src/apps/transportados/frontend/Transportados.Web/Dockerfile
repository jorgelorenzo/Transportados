# syntax=docker/dockerfile:1.7
# Build from src/apps/transportados:
# docker build -f frontend/Transportados.Web/Dockerfile -t transportados-web .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.1.0
WORKDIR /src

COPY ["frontend/Transportados.Web/Transportados.Web.csproj", "frontend/Transportados.Web/"]
COPY ["frontend/Transportados.Client/Transportados.Client.csproj", "frontend/Transportados.Client/"]
COPY ["backend/Transportados.Contracts/Transportados.Contracts.csproj", "backend/Transportados.Contracts/"]
COPY ["shared/Transportados.Platform.UI/Transportados.Platform.UI.csproj", "shared/Transportados.Platform.UI/"]
RUN dotnet restore "./frontend/Transportados.Web/Transportados.Web.csproj" --source https://api.nuget.org/v3/index.json

COPY . .
WORKDIR "/src/frontend/Transportados.Web"
RUN dotnet build "./Transportados.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore /p:Version=$VERSION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.1.0
RUN dotnet publish "./Transportados.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false /p:Version=$VERSION

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Transportados.Web.dll"]
