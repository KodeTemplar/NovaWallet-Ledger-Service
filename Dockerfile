FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY NovaWallet.sln ./
COPY src/NovaWallet.Domain/NovaWallet.Domain.csproj src/NovaWallet.Domain/
COPY src/NovaWallet.Application/NovaWallet.Application.csproj src/NovaWallet.Application/
COPY src/NovaWallet.Infrastructure/NovaWallet.Infrastructure.csproj src/NovaWallet.Infrastructure/
COPY src/NovaWallet.Api/NovaWallet.Api.csproj src/NovaWallet.Api/

RUN dotnet restore src/NovaWallet.Api/NovaWallet.Api.csproj --configfile NuGet.Config

COPY src/ src/
RUN dotnet publish src/NovaWallet.Api/NovaWallet.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "NovaWallet.Api.dll"]
