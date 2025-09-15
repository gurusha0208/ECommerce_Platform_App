# # Build stage
# FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# WORKDIR /src

# # Copy project files
# COPY src/Catalog.API/Catalog.API.csproj src/Catalog.API/
# COPY src/Common/Common.csproj src/Common/

# # Restore dependencies
# RUN dotnet restore src/Catalog.API/Catalog.API.csproj

# # Copy all source code
# COPY . .

# # Build and publish
# RUN dotnet publish src/Catalog.API/Catalog.API.csproj -c Release -o /app

# # Runtime stage
# FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
# WORKDIR /app
# COPY --from=build /app .

# # Set URL for ASP.NET
# ENV ASPNETCORE_URLS=http://+:5001
# EXPOSE 5001

# # Run the app
# ENTRYPOINT ["dotnet", "Catalog.API.dll"]



FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Catalog.API/Catalog.API.csproj", "Catalog.API/"]
COPY ["../Common/Common.csproj", "Common/"]   # relative to Dockerfile

# Restore dependencies
RUN dotnet restore "Catalog.API/Catalog.API.csproj"

# Copy everything
COPY . .

WORKDIR "/src/Catalog.API"
RUN dotnet publish "Catalog.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Catalog.API.dll"]

