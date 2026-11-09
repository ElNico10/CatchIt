# Runs the Core test suite on Linux. CatchIt.App is WinForms (Windows only), so it is not copied in.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /src
# Project files first so the restore layer is cached until a dependency changes.
COPY src/CatchIt.Core/CatchIt.Core.csproj src/CatchIt.Core/
COPY tests/CatchIt.Tests/CatchIt.Tests.csproj tests/CatchIt.Tests/
RUN dotnet restore tests/CatchIt.Tests/CatchIt.Tests.csproj
COPY src/CatchIt.Core src/CatchIt.Core
COPY tests tests
CMD ["dotnet", "test", "tests/CatchIt.Tests/CatchIt.Tests.csproj", "--no-restore"]
