FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/PocketStoryPoker.csproj .
RUN dotnet restore PocketStoryPoker.csproj
COPY src/ . 
RUN dotnet publish PocketStoryPoker.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PocketStoryPoker.dll"]
