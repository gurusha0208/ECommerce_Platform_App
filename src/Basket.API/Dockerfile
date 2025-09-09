# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy Basket.API and Common project files
COPY Basket.API/Basket.API.csproj Basket.API/
COPY Common/Common.csproj Common/

# Restore dependencies
RUN dotnet restore Basket.API/Basket.API.csproj

# Copy all source code
COPY . .

# Build and publish
WORKDIR /src/Basket.API
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "Basket.API.dll"]