# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy Catalog.API and Common project files
COPY src/Catalog.API/Catalog.API.csproj src/Catalog.API/
COPY src/Common/Common.csproj src/Common/

# Restore dependencies
RUN dotnet restore src/Catalog.API/Catalog.API.csproj

# Copy all source code
COPY . .

# Build and publish (explicit path to csproj!)
RUN dotnet publish src/Catalog.API/Catalog.API.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# EXPOSE 80
ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001

# Copy wait-for-it script
COPY wait-for-it.sh ./
RUN chmod +x wait-for-it.sh

# Use wait-for-it to wait for SQL before starting
ENTRYPOINT ["./wait-for-it.sh", "postgres:5432", "--", "dotnet", "Catalog.API.dll"]
