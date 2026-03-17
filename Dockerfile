# -----------------------------------
# Stage 1: Build Frontend (Angular)
# -----------------------------------
FROM node:20-alpine AS frontend-build
WORKDIR /app

# 配置 npm 使用淘宝镜像（加速国内构建）
RUN npm config set registry https://registry.npmmirror.com

# 升级 npm 到最新版本
RUN npm install -g npm@latest

# 复制 package.json 并安装依赖（利用 Docker 层缓存）
COPY frontend/package.json ./
RUN npm install --prefer-offline --no-audit

# 复制源代码并构建
COPY frontend/ ./
RUN npm run build -- --configuration=production

# -----------------------------------
# Stage 2: Build Backend (.NET)
# -----------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

# 复制后端代码（.dockerignore 已排除 bin/obj）
COPY backend/ ./backend/

# Build
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
