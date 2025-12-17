FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["OtlpServer.csproj", "./"]
RUN dotnet restore "OtlpServer.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet publish "OtlpServer.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 4317
ENV ASPNETCORE_URLS="http://*:4317"
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtlpServer.dll"]
