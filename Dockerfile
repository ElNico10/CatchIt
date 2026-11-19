# Three stages in one file:
#   test     runs the Core test suite on Linux (CI: docker build --target test).
#   build    publishes the web app and seeds its demo database.
#   runtime  the small image that serves the web app. It is the last stage, so a plain
#            `docker build .` produces it. .github/workflows/docker-publish.yml pushes it to Docker Hub.
# CatchIt.App is WinForms (Windows only), so it is never copied in.

# ---------- test ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /src
# Project files first so the restore layer is cached until a dependency changes.
COPY src/CatchIt.Core/CatchIt.Core.csproj src/CatchIt.Core/
COPY tests/CatchIt.Tests/CatchIt.Tests.csproj tests/CatchIt.Tests/
RUN dotnet restore tests/CatchIt.Tests/CatchIt.Tests.csproj
COPY src/CatchIt.Core src/CatchIt.Core
COPY tests tests
CMD ["dotnet", "test", "tests/CatchIt.Tests/CatchIt.Tests.csproj", "--no-restore"]

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/CatchIt.Core/CatchIt.Core.csproj src/CatchIt.Core/
COPY src/CatchIt.Web/CatchIt.Web.csproj src/CatchIt.Web/
RUN dotnet restore src/CatchIt.Web/CatchIt.Web.csproj
COPY src/CatchIt.Core src/CatchIt.Core
COPY src/CatchIt.Web src/CatchIt.Web
RUN dotnet publish src/CatchIt.Web/CatchIt.Web.csproj -c Release -o /app/publish --no-restore

# Seed the simulated demo catalog into the image. Free hosts wipe the disk on every restart, so the
# database ships inside the image and every start begins from the same demo data. Seeding here
# instead of at startup also keeps cold starts fast.
RUN mkdir -p /app/data && CATCHIT_DB=/app/data/catchit.db dotnet /app/publish/CatchIt.Web.dll --seed

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# The app runs as the image's non-root "app" user, which must be able to write the database
# (logins, registrations and the watchlist all write to it).
COPY --from=build --chown=app:app /app/data /app/data
# The publish workflow passes the image tag (the short commit SHA) so /health can report which image is
# running. That is how you can tell the cloud platform is serving the tag the workflow pushed.
ARG APP_VERSION=local
ENV CATCHIT_DB=/app/data/catchit.db \
    ASPNETCORE_ENVIRONMENT=Production \
    APP_VERSION=$APP_VERSION
USER app
EXPOSE 8080
# Hosts such as Render tell the app which port to use in $PORT; locally it defaults to 8080.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet CatchIt.Web.dll"]
