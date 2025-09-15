# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy Identity.API and Common project files
COPY src/Identity.API/Identity.API.csproj src/Identity.API/
COPY src/Common/Common.csproj src/Common/

# Restore dependencies
RUN dotnet restore src/Identity.API/Identity.API.csproj

# Copy all source code
COPY . .

# Build and publish (explicit path to csproj!)
RUN dotnet publish src/Identity.API/Identity.API.csproj -c Release -o /app

# Runtime stage 
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# EXPOSE 5002 -this is local development only
# ENV ASPNETCORE_URLS=http://+:5002

# # Copy wait-for-it script - this is local development only
# COPY wait-for-it.sh ./
# RUN chmod +x wait-for-it.sh

# Use wait-for-it to wait for SQL before starting
# ENTRYPOINT ["./wait-for-it.sh", "postgres:5432", "--", "dotnet", "Identity.API.dll"]
ENTRYPOINT ["dotnet", "Identity.API.dll"]

