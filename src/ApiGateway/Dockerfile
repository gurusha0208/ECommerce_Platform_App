# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first for restore caching
COPY src/ApiGateway/ApiGateway.csproj src/ApiGateway/

# Restore dependencies
RUN dotnet restore src/ApiGateway/ApiGateway.csproj

# Copy everything
COPY . .

# Build & publish
RUN dotnet publish src/ApiGateway/ApiGateway.csproj -c Release -o /app /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Let Render inject the runtime port
ENV ASPNETCORE_URLS=http://+:$PORT

EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
