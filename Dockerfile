# ----------------------------------------------------
# 階段 1：Runtime 執行環境 (使用輕量化的 ASP.NET 9 映像檔)
# ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
# .NET 8 / 9 預設 HTTP 容器通訊埠為 8080
EXPOSE 8080
EXPOSE 8081

# ----------------------------------------------------
# 階段 2：SDK 建置環境 (用於還原套件與編譯)
# ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 先複製 .csproj 並進行 NuGet 套件還原，可有效利用 Docker 層快取 (Layer Cache)
COPY ["MyBackend.csproj", "./"]
RUN dotnet restore "MyBackend.csproj"

# 複製其餘原始碼並進行編譯
COPY . .
WORKDIR "/src"
RUN dotnet build "MyBackend.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ----------------------------------------------------
# 階段 3：發布應用程式 (Publish)
# ----------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MyBackend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ----------------------------------------------------
# 階段 4：最終打包 (複製 Publish 產出至 Runtime 映像檔)
# ----------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyBackend.dll"]