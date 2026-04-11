# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DealMe.slnx ./
COPY DealMe.Core/DealMe.Core.csproj DealMe.Core/
COPY DealMe.Infrastructure/DealMe.Infrastructure.csproj DealMe.Infrastructure/
COPY DealMe.Api/DealMe.Api.csproj DealMe.Api/
RUN dotnet restore DealMe.slnx

COPY . .
RUN dotnet publish DealMe.Api/DealMe.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /data

ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001
ENTRYPOINT ["dotnet", "DealMe.Api.dll"]
