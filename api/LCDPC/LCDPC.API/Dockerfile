FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["LCDPC.API/LCDPC.API.csproj", "LCDPC.API/"]
COPY ["LCDPC.Application/LCDPC.Application.csproj", "LCDPC.Application/"]
COPY ["LCDPC.Domain/LCDPC.Domain.csproj", "LCDPC.Domain/"]
COPY ["LCDPC.Infrastructure/LCDPC.Infrastructure.csproj", "LCDPC.Infrastructure/"]

RUN dotnet restore "LCDPC.API/LCDPC.API.csproj"

COPY . .
WORKDIR "/src/LCDPC.API"
RUN dotnet publish "LCDPC.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LCDPC.API.dll"]
