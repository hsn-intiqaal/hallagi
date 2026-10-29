# ===== Build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
# Restore first (cached until a project or package version changes)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Hallagi.Domain/Hallagi.Domain.csproj src/Hallagi.Domain/
COPY src/Hallagi.Application/Hallagi.Application.csproj src/Hallagi.Application/
COPY src/Hallagi.Infrastructure/Hallagi.Infrastructure.csproj src/Hallagi.Infrastructure/
COPY src/Hallagi.Api/Hallagi.Api.csproj src/Hallagi.Api/
RUN dotnet restore src/Hallagi.Api/Hallagi.Api.csproj
COPY src/ src/
RUN dotnet publish src/Hallagi.Api/Hallagi.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ===== Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080
# non-root user shipped with the official .NET images
USER $APP_UID
ENTRYPOINT ["dotnet", "Hallagi.Api.dll"]
