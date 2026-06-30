# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install system dependencies needed by Chromium
RUN apt-get update && apt-get install -y \
    libglib2.0-0 \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libdbus-1-3 \
    libxcb1 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    && rm -rf /var/lib/apt/lists/*

COPY . .
RUN dotnet restore

# Install Playwright CLI
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet build
RUN playwright install --with-deps

RUN dotnet publish -c Release -o /app/publish

# Final image
FROM base AS final
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Copy Playwright browsers
COPY --from=build /root/.cache/ms-playwright /root/.cache/ms-playwright

ENTRYPOINT ["dotnet", "ExtractEmbededUri.dll"]