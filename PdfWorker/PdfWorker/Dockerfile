FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PdfWorker/PdfWorker.sln", "PdfWorker/"]
COPY ["PdfWorker/PdfWorker/PdfWorker.csproj", "PdfWorker/PdfWorker/"]
COPY ["PdfWorker/Application/Application.csproj", "PdfWorker/Application/"]
COPY ["PdfWorker/Domain/Domain.csproj", "PdfWorker/Domain/"]
COPY ["PdfWorker/Infrastructure/Infrastructure.csproj", "PdfWorker/Infrastructure/"]
COPY ["PdfWorker/Contracts/Contracts.csproj", "PdfWorker/Contracts/"]
COPY ["Payments/Contracts/Contracts.csproj", "Payments/Contracts/"]
RUN dotnet restore "PdfWorker/PdfWorker/PdfWorker.csproj"
COPY PdfWorker/ PdfWorker/
COPY Payments/Contracts/ Payments/Contracts/
WORKDIR "/src/PdfWorker/PdfWorker"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PdfWorker.dll"]
