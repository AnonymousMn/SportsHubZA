FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/SportsClubHub.Domain/SportsClubHub.Domain.csproj", "src/SportsClubHub.Domain/"]
COPY ["src/SportsClubHub.Web/SportsClubHub.Web.csproj", "src/SportsClubHub.Web/"]
RUN dotnet restore "src/SportsClubHub.Web/SportsClubHub.Web.csproj"

COPY . .
RUN dotnet publish "src/SportsClubHub.Web/SportsClubHub.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet SportsClubHub.Web.dll --urls http://0.0.0.0:${PORT:-8080}"]
