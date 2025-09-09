# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln .
COPY ./ApiGateway/ApiGateway.csproj ./ApiGateway/
RUN dotnet restore ApiGateway/ApiGateway.csproj

# Copy everything and build
COPY . .
WORKDIR /src/ApiGateway
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "ApiGateway.dll"]≈
