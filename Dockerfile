
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY proyectoGrupal.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish proyectoGrupal.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# - PORT: Render la reemplaza por su propio puerto; 8080 es para probar localmente.
# - TZ: hora de Perú para las fechas de los reportes (el contenedor usa UTC por defecto).
# - La base de datos SQLite se guarda en /app/data.
ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=8080 \
    TZ=America/Lima \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/alerta_vecinal.db"

COPY --from=build /app/publish .

# La app corre con un usuario sin privilegios ($APP_UID, definido por la imagen de .NET),
# así que necesita permiso de escritura en la carpeta de la base de datos y en la de fotos.
RUN mkdir -p /app/data /app/wwwroot/uploads/incidencias \
    && chown -R $APP_UID /app/data /app/wwwroot/uploads

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "proyectoGrupal.dll"]
