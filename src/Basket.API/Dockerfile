# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY src/Basket.API/Basket.API.csproj src/Basket.API/
COPY src/Common/Common.csproj src/Common/

# Restore dependencies
RUN dotnet restore src/Basket.API/Basket.API.csproj

# Copy all source code
COPY . .

# Build and publish
RUN dotnet publish src/Basket.API/Basket.API.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# ENV ASPNETCORE_URLS=http://+:5003
# EXPOSE 5003
ENV ASPNETCORE_URLS=http://+:${PORT}

# Run the app
ENTRYPOINT ["dotnet", "Basket.API.dll"]




