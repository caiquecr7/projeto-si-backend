FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ProjetoSi.Api/ProjetoSi.Api.csproj src/ProjetoSi.Api/
RUN dotnet restore src/ProjetoSi.Api/ProjetoSi.Api.csproj
COPY src/ src/
RUN dotnet publish src/ProjetoSi.Api/ProjetoSi.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ProjetoSi.Api.dll"]
