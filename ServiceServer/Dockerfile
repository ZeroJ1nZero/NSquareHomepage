# Multi-stage Dockerfile for ServiceServer
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Web/Web.csproj src/Web/
RUN dotnet restore src/Web/Web.csproj

# Copy source code, wwwroot and publish
COPY src/ src/
RUN dotnet publish src/Web/Web.csproj -c Release -o /app/publish --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080;https://+:7001
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
EXPOSE 7001

ENTRYPOINT ["dotnet", "Web.dll"]
