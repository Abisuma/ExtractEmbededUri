# Base image with .NET 9 runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# Build stage with SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore + add Playwright package (important!)
RUN dotnet restore

# Install Playwright CLI globally
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="$PATH:/root/.dotnet/tools"

# Build the project (this is required before playwright install)
RUN dotnet build

# Now install the browsers
RUN playwright install --with-deps

# Publish
RUN dotnet publish -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ExtractEmbededUri.dll"]