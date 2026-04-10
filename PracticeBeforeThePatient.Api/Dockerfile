FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["PracticeBeforeThePatient.Api/PracticeBeforeThePatient.Api.csproj", "PracticeBeforeThePatient.Api/"]
COPY ["PracticeBeforeThePatient.Core/PracticeBeforeThePatient.Core.csproj", "PracticeBeforeThePatient.Core/"]
RUN dotnet restore "PracticeBeforeThePatient.Api/PracticeBeforeThePatient.Api.csproj"

COPY . .
WORKDIR "/src/PracticeBeforeThePatient.Api"
RUN dotnet publish "PracticeBeforeThePatient.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PracticeBeforeThePatient.Api.dll"]
