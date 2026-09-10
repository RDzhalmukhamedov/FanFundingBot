# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for all projects in the solution
COPY ["FanFundingBot.App/FanFundingBot.App.csproj", "FanFundingBot.App/"]
COPY ["FanFundingBot.Bot/FanFundingBot.Bot.csproj", "FanFundingBot.Bot/"]
COPY ["FanFundingBot.Domain/FanFundingBot.Domain.csproj", "FanFundingBot.Domain/"]
COPY ["FanFundingBot.Application/FanFundingBot.Application.csproj", "FanFundingBot.Application/"]
COPY ["FanFundingBot.Infrastructure.Persistence/FanFundingBot.Infrastructure.Persistence.csproj", "FanFundingBot.Infrastructure.Persistence/"]
COPY ["FanFundingBot.Infrastructure.ExternalStorage/FanFundingBot.Infrastructure.ExternalStorage.csproj", "FanFundingBot.Infrastructure.ExternalStorage/"]

# Restore dependencies for the startup project (restores all transitive dependencies)
RUN dotnet restore "FanFundingBot.App/FanFundingBot.App.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/FanFundingBot.App"
RUN dotnet build "./FanFundingBot.App.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FanFundingBot.App.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FanFundingBot.App.dll"]