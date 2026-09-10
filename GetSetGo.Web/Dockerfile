# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

WORKDIR /app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project file
COPY ["GetSetGo.Web/GetSetGo.Web.csproj", "GetSetGo.Web/"]

# Restore dependencies
RUN dotnet restore "GetSetGo.Web/GetSetGo.Web.csproj"

# Copy source
COPY . .

# Build
RUN dotnet build "GetSetGo.Web/GetSetGo.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# Publish
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "GetSetGo.Web/GetSetGo.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# Final image
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "GetSetGo.Web.dll"]