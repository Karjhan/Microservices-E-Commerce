# -------- BASE RUNTIME --------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# -------- BUILD --------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution
COPY ["ApiGateway.sln", "./"]

# Copy project files
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["IntegrationTests/IntegrationTests.csproj", "IntegrationTests/"]

# Restore
RUN dotnet restore

# Copy everything
COPY . .

WORKDIR /src/ApiGateway
RUN dotnet build -c Release -o /app/build

# -------- PUBLISH --------
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# -------- FINAL --------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ApiGateway.dll"]