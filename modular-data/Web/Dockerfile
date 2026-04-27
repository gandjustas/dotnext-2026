# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG BUILD_CONFIGURATION=Release

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION
WORKDIR /src
COPY --parents **/*.csproj **/*.props *.sln* **/NuGet.config* .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	--mount=type=cache,id=modular-data,target=/app/build \
	dotnet restore --artifacts-path /app/build
COPY . .
WORKDIR "/src/Web"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	--mount=type=cache,id=modular-data,target=/app/build \
	dotnet build \
	--no-restore \
	--artifacts-path /app/build \
	-c $BUILD_CONFIGURATION

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
	--mount=type=cache,id=modular-data,target=/app/build \
	dotnet publish \
	--no-restore \
	--no-build \
	--artifacts-path /app/build \
	-c $BUILD_CONFIGURATION \
	/p:UseAppHost=false \
	-o /app/publish

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Web.dll"]