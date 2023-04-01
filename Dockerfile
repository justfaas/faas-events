### Builder
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview AS build
ARG TARGETARCH
WORKDIR /app

RUN apt update && apt install libxml2-utils -y

# restore dependencies; use nuget config for private dependencies
COPY ./src/faas-events.csproj ./
RUN dotnet restore -a $TARGETARCH

COPY ./src/. ./
RUN dotnet publish -c release -a $TARGETARCH -o dist faas-events.csproj

### Runtime
FROM mcr.microsoft.com/dotnet/aspnet:7.0 as final

RUN addgroup faas-app && useradd -G faas-app faas-user

WORKDIR /app
COPY --from=build /app/dist/ ./
RUN chown faas-user:faas-app -R .

USER faas-user

ENTRYPOINT [ "dotnet", "faas-events.dll" ]
