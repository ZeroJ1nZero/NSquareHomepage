# Multi-stage Dockerfile for ResourceServer
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY src/ResourceServer.Domain/ResourceServer.Domain.csproj src/ResourceServer.Domain/
COPY src/ResourceServer.Application/ResourceServer.Application.csproj src/ResourceServer.Application/
COPY src/ResourceServer.Infrastructure/ResourceServer.Infrastructure.csproj src/ResourceServer.Infrastructure/
COPY src/ResourceServer.Api/ResourceServer.Api.csproj src/ResourceServer.Api/
RUN dotnet restore src/ResourceServer.Api/ResourceServer.Api.csproj

# Copy source code and publish
COPY src/ src/
RUN dotnet publish src/ResourceServer.Api/ResourceServer.Api.csproj -c Release -o /app/publish --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080;https://+:7002
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
EXPOSE 7002

ENTRYPOINT ["dotnet", "ResourceServer.Api.dll"]
