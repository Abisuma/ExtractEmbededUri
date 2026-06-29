# Base image with .NET 9 runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# Build stage with SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore

# Install Playwright CLI and browsers here (SDK available)
RUN dotnet tool install --global Microsoft.Playwright.CLI \
    && playwright install
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet publish -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ExtractEmbededUri.dll"]
