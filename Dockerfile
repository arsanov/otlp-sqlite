FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "OtlpServer.csproj"
RUN dotnet publish "OtlpServer.csproj" -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 4317
ENV ASPNETCORE_URLS="http://*:4317"
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OtlpServer.dll"]
