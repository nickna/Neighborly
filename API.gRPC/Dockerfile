# Use the official .NET 8.0 SDK image as the build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /app

# Copy csproj and restore dependencies
COPY API.gRPC/*.csproj ./API.gRPC/
COPY Neighborly/*.csproj ./Neighborly/

# Copy the LICENSE.txt and README.md files
COPY LICENSE.txt ./
# COPY ../README.md ./

WORKDIR /app/API.gRPC
RUN dotnet workload restore
RUN dotnet restore

# Copy the rest of the project files
WORKDIR /app
COPY API.gRPC/. ./API.gRPC/
COPY Neighborly/. ./Neighborly/

# Build the application
WORKDIR /app/API.gRPC
RUN dotnet publish -c Release -o out

# Use the official .NET 8.0 runtime image as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy the build output from the build environment
COPY --from=build-env /app/API.gRPC/out .

USER $APP_UID

# Set the entry point of the application
ENTRYPOINT ["./API"]