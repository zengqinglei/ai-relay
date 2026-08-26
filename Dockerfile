# Node 版本的唯一来源是 .nvmrc，CI 通过 --build-arg NODE_VERSION=$(cat .nvmrc) 传入。
# 这里的默认值仅用于本地 `docker build`，请与 .nvmrc 保持一致。
ARG NODE_VERSION=24

# -----------------------------------
# Stage 1: Build Frontend (Angular)
# -----------------------------------
FROM node:${NODE_VERSION}-alpine AS frontend-build
WORKDIR /app

# 默认使用官方 registry（GitHub Runner 直连更快）。
# 国内构建可加速：--build-arg NPM_REGISTRY=https://registry.npmmirror.com
ARG NPM_REGISTRY=https://registry.npmjs.org
RUN npm config set registry "$NPM_REGISTRY"

# 依赖安装：严格按 lockfile 还原，保证与 CI、本地完全一致
# （不要在这里升级 npm —— 使用镜像自带版本，由 .nvmrc 统一控制）
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund

# 支持构建时传入 API Gateway 地址（用于前后端分离部署）
# 使用方式: docker build --build-arg API_GATEWAY=https://api.example.com .
ARG API_GATEWAY=""

# 复制源代码
COPY frontend/ ./

# 替换环境变量占位符并构建
RUN if [ -n "$API_GATEWAY" ]; then \
      echo "🔧 替换 API_GATEWAY: $API_GATEWAY"; \
      sed -i "s|__API_GATEWAY__|${API_GATEWAY}|g" src/environments/environment.prod.ts; \
    else \
      echo "🔧 使用默认配置（空 gateway）"; \
      sed -i "s|__API_GATEWAY__||g" src/environments/environment.prod.ts; \
    fi && \
    npm run build -- --configuration=production

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

# 1. 拷贝后端构建结果
COPY --from=backend-build /app/publish .

# 2. 拷贝前端构建结果到后端的 wwwroot 目录，让 ASP.NET Core 直接托管静态 SPA 文件
# Angular >= 17 生产构建会输出到 dist/<project>/browser 目录
COPY --from=frontend-build /app/dist/airelay-web/browser ./wwwroot

# 启动程序（Program.cs 会在应用启动前自动执行数据库迁移）
ENTRYPOINT ["dotnet", "AiRelay.Api.dll"]
