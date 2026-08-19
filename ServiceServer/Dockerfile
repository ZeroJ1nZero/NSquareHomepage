# Multi-stage Dockerfile for ServiceServer
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY src/ServiceServer.Domain/ServiceServer.Domain.csproj src/ServiceServer.Domain/
COPY src/ServiceServer.Application/ServiceServer.Application.csproj src/ServiceServer.Application/
COPY src/ServiceServer.Infrastructure/ServiceServer.Infrastructure.csproj src/ServiceServer.Infrastructure/
COPY src/ServiceServer.Api/ServiceServer.Api.csproj src/ServiceServer.Api/
RUN dotnet restore src/ServiceServer.Api/ServiceServer.Api.csproj

# Copy source code, wwwroot and publish
COPY src/ src/
RUN dotnet publish src/ServiceServer.Api/ServiceServer.Api.csproj -c Release -o /app/publish --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080;https://+:7001
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
EXPOSE 7001

ENTRYPOINT ["dotnet", "ServiceServer.Api.dll"]
