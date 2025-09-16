# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy ApiGateway and Common project files
COPY src/ApiGateway/ApiGateway.csproj src/ApiGateway/


# Restore dependencies
RUN dotnet restore src/ApiGateway/ApiGateway.csproj

# Copy all source code
COPY . .

# Build and publish (explicit path to csproj!)
RUN dotnet publish src/ApiGateway/ApiGateway.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# EXPOSE 80 - local development only
# ENV ASPNETCORE_URLS=http://+:5100
# EXPOSE 5100

ENV ASPNETCORE_URLS=http://+:${PORT}
    
# Copy wait-for-it script - local development only
# COPY wait-for-it.sh ./
# RUN chmod +x wait-for-it.sh

# Use wait-for-it to wait for SQL before starting - local development only

#ENTRYPOINT ["./wait-for-it.sh", "postgres:5432", "--", "dotnet", "ApiGateway.dll"]
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
