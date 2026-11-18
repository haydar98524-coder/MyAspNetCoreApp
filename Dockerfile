FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["KirkukInstitute.csproj", "./"]
RUN dotnet restore "KirkukInstitute.csproj"

COPY . .
RUN dotnet publish "KirkukInstitute.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data/backups \
    && chown -R $APP_UID:$APP_UID /app/App_Data
USER $APP_UID

ENTRYPOINT ["dotnet", "KirkukInstitute.dll"]
