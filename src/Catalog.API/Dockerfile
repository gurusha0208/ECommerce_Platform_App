# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY src/Catalog.API/Catalog.API.csproj src/Catalog.API/
COPY src/Common/Common.csproj src/Common/

# Restore dependencies
RUN dotnet restore src/Catalog.API/Catalog.API.csproj

# Copy all source code
COPY . .

# Build and publish
RUN dotnet publish src/Catalog.API/Catalog.API.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# # Set URL for ASP.NET - this is local development only
# ENV ASPNETCORE_URLS=http://+:5001
# EXPOSE 5001

# Run the app
ENTRYPOINT ["dotnet", "Catalog.API.dll"]




