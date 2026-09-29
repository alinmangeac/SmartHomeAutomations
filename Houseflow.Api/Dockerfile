FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Houseflow.Api/Houseflow.Api.csproj Houseflow.Api/
RUN dotnet restore Houseflow.Api/Houseflow.Api.csproj
COPY Houseflow.Api/ Houseflow.Api/
WORKDIR /src/Houseflow.Api
RUN dotnet publish Houseflow.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Houseflow.Api.dll"]
