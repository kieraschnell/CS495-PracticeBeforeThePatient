FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["PracticeBeforeThePatient.Web/PracticeBeforeThePatient.Web.csproj", "PracticeBeforeThePatient.Web/"]
COPY ["PracticeBeforeThePatient.Core/PracticeBeforeThePatient.Core.csproj", "PracticeBeforeThePatient.Core/"]
RUN dotnet restore "PracticeBeforeThePatient.Web/PracticeBeforeThePatient.Web.csproj"

COPY . .
WORKDIR "/src/PracticeBeforeThePatient.Web"
RUN dotnet publish "PracticeBeforeThePatient.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PracticeBeforeThePatient.Web.dll"]
