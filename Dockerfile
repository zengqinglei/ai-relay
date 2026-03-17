# -----------------------------------
# Stage 1: Build Frontend (Angular)
# -----------------------------------
FROM node:20-alpine AS frontend-build
WORKDIR /app

# 升级 npm 到最新版本
RUN npm install -g npm@latest

COPY frontend/package.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build -- --configuration=production

# -----------------------------------
# Stage 2: Build Backend (.NET)
# -----------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

# 拷贝后端代码（包含 common.props、components、ddd-struct）
COPY backend/ ./backend/

WORKDIR "/src/backend/src/AiRelay.Api"
RUN dotnet publish "AiRelay.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------
# Stage 3: Final Runtime Image
# -----------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

# 设定环境默认配置
ENV ASPNETCORE_HTTP_PORTS=8080

# 1. 拷贝后端构建结果
COPY --from=backend-build /app/publish .

# 2. 拷贝前端构建结果到后端的 wwwroot 目录，让 ASP.NET Core 直接托管静态 SPA 文件
# Angular >= 17 生产构建会输出到 dist/<project>/browser 目录
COPY --from=frontend-build /app/dist/airelay-web/browser ./wwwroot

# 启动程序（Program.cs 会在应用启动前自动执行数据库迁移）
ENTRYPOINT ["dotnet", "AiRelay.Api.dll"]
