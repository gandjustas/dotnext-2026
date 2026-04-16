# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# These ARGs allow for swapping out the base used to make the final image when debugging from VS
ARG LAUNCHING_FROM_VS
# This sets the base image for final, but only if LAUNCHING_FROM_VS has been defined
ARG FINAL_BASE_IMAGE=${LAUNCHING_FROM_VS:+aotdebug}
ARG BUILD_CONFIGURATION=Release

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Install clang/zlib1g-dev dependencies for publishing to native
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
ARG BUILD_CONFIGURATION
WORKDIR /src
COPY --parents **/*.csproj *.props *.sln* .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	--mount=type=cache,id=modular-test-rest-backend,target=/app/build \
    dotnet restore --artifacts-path /app/build
COPY . .
WORKDIR "/src/Backend/Rest"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	--mount=type=cache,id=modular-test-rest-backend,target=/app/build \
	dotnet build \
	--no-restore \
	--artifacts-path /app/build \
	-c $BUILD_CONFIGURATION    

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	--mount=type=cache,id=modular-test-rest-backend,target=/app/build \
	dotnet publish \
	--no-restore \
	--no-build \
	--artifacts-path /app/build \
	-c $BUILD_CONFIGURATION \
	/p:UseAppHost=true \
	-o /app/publish

# This stage is used as the base for the final stage when launching from VS to support debugging in regular mode (Default when not using the Debug configuration)
FROM base AS aotdebug
USER root
# Install GDB to support native debugging
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gdb
USER app

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM ${FINAL_BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled} AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./Rest"]

