FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# -------- BUILD --------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution
COPY ["Payments.sln", "./"]

# Copy project files
COPY ["Payments/Payments.csproj", "Payments/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
COPY ["IntegrationTests/IntegrationTests.csproj", "IntegrationTests/"]
COPY ["UnitTests/UnitTests.csproj", "UnitTests/"]

RUN dotnet restore

# Copy everything else
COPY . .

WORKDIR "/src/Payments"
RUN dotnet build -c Release -o /app/build

# -------- PUBLISH --------
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# -------- FINAL --------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Payments.dll"]