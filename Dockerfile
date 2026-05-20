# Acesse https://aka.ms/customizecontainer para saber como personalizar seu contêiner de depuração e como o Visual Studio usa este Dockerfile para criar suas imagens para uma depuração mais rápida.

# Esta fase é usada durante a execução no VS no modo rápido (Padrão para a configuração de Depuração)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Esta fase é usada para compilar o projeto de serviço
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./src/Fiap.CloudGames.API/Fiap.CloudGames.API.csproj", "src/Fiap.CloudGames.API/"]
COPY ["./src/Fiap.CloudGames.Application/Fiap.CloudGames.Application.csproj", "src/Fiap.CloudGames.Application/"]
COPY ["./src/Fiap.CloudGames.Domain/Fiap.CloudGames.Domain.csproj", "src/Fiap.CloudGames.Domain/"]
COPY ["./src/Fiap.CloudGames.Infrastructure/Fiap.CloudGames.Infrastructure.csproj", "src/Fiap.CloudGames.Infrastructure/"]
RUN dotnet restore "./src/Fiap.CloudGames.API/Fiap.CloudGames.API.csproj"
COPY . .
WORKDIR "/src/src/Fiap.CloudGames.API"
RUN dotnet build "./Fiap.CloudGames.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase é usada para publicar o projeto de serviço a ser copiado para a fase final
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Fiap.CloudGames.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Esta fase é usada na produção ou quando executada no VS no modo normal (padrão quando não está usando a configuração de Depuração)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fiap.CloudGames.API.dll"]