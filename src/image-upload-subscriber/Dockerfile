FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 11111

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["UploadSubscriber.csproj", "."]
RUN dotnet restore "./UploadSubscriber.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "UploadSubscriber.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UploadSubscriber.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UploadSubscriber.dll"]