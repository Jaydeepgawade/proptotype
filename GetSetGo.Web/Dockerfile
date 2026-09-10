# Base image - used for running the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Render will access the application on port 8080
EXPOSE 8080

# Tell ASP.NET Core to listen on all network interfaces
ENV ASPNETCORE_URLS=http://0.0.0.0:8080


# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project file and restore dependencies
COPY ["Prototype Sim.csproj", "./"]
RUN dotnet restore "./Prototype Sim.csproj"

# Copy the remaining source code
COPY . .

# Build the application
RUN dotnet build "./Prototype Sim.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build


# Publish image
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./Prototype Sim.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false


# Final production image
FROM base AS final

WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Start the application
ENTRYPOINT ["dotnet", "Prototype Sim.dll"]