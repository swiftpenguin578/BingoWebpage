FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /source

COPY Bingo.slnx global.json Directory.Build.props ./
COPY src/Bingo.Web/Bingo.Web.csproj src/Bingo.Web/
COPY src/Bingo.Application/Bingo.Application.csproj src/Bingo.Application/
COPY src/Bingo.Domain/Bingo.Domain.csproj src/Bingo.Domain/
COPY src/Bingo.Infrastructure/Bingo.Infrastructure.csproj src/Bingo.Infrastructure/
RUN dotnet restore src/Bingo.Web/Bingo.Web.csproj

COPY src/ src/
RUN dotnet publish src/Bingo.Web/Bingo.Web.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bingo.Web.dll"]
