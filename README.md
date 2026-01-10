# SwitchYard

## 项目简介

SwitchYard 是一个铁路站场与枢纽教学工具包，提供驼峰作业计算、列车溜放仿真、站场布局设计等功能。项目采用前后端分离架构，前端使用 Vue 3，后端使用 ASP.NET Core Web API。

## 技术栈

### 前端 (switchyard-vue)
- **框架**: Vue 3.5.25 + TypeScript
- **构建工具**: Vite 7.2.4
- **UI 框架**: Element Plus 2.13.0
- **路由**: Vue Router 4.6.3
- **HTTP 客户端**: Axios 1.13.2
- **其他**: jQuery 3.7.1

### 后端 (SwitchYard.WebApi)
- **框架**: ASP.NET Core 8.0
- **语言**: C# (.NET 8.0)
- **认证**: JWT (JSON Web Token)
- **数据库**: SQLite / MySQL (通过 Dapper)
- **API 文档**: Swagger / OpenAPI
- **安全**: Argon2 密码哈希

## 项目结构

```
SwitchYard/
├── switchyard-vue/              # 前端 Vue 应用
│   ├── src/
│   │   ├── assets/              # 静态资源
│   │   ├── components/          # 通用组件
│   │   ├── hump/                # 驼峰相关功能模块
│   │   │   ├── HumpMain.vue     # 驼峰主界面
│   │   │   ├── HumpLayout.vue   # 站场布局
│   │   │   ├── HumpSim.vue      # 溜放仿真
│   │   │   └── ...
│   │   ├── course/              # 课程模块
│   │   ├── router/              # 路由配置
│   │   ├── views/               # 页面视图
│   │   │   ├── Login.vue        # 登录页面
│   │   │   ├── HomeView.vue     # 首页
│   │   │   └── AboutView.vue    # 关于页面
│   │   ├── utils/               # 工具函数
│   │   │   └── axios.ts         # Axios 全局配置
│   │   ├── App.vue              # 根组件
│   │   └── main.ts              # 应用入口
│   ├── package.json
│   └── vite.config.ts
│
├── SwitchYard.WebApi/           # 后端 Web API
│   ├── SwitchYard.Service/      # Web API 服务
│   │   ├── Controllers/         # API 控制器
│   │   │   ├── AuthController.cs    # 认证控制器
│   │   │   └── HumpController.cs    # 驼峰业务控制器
│   │   ├── Models/              # 数据模型
│   │   │   ├── User.cs
│   │   │   ├── LoginRequest.cs
│   │   │   └── LoginResponse.cs
│   │   ├── Services/            # 业务服务
│   │   │   ├── JwtTokenService.cs   # JWT 令牌服务
│   │   │   └── UserService.cs       # 用户服务
│   │   ├── Program.cs           # 应用程序入口
│   │   └── appsettings.json     # 配置文件
│   │
│   └── SwitchYard.Hump/         # 驼峰计算核心库
│       ├── HumpCalculator.cs    # 驼峰计算器
│       ├── EnergyHeightCalculator.cs
│       ├── Position.cs
│       └── Wagon.cs
│
└── LocalData/                   # 本地数据文件
    └── Hump/                    # 驼峰相关数据
        ├── Position.csv
        ├── PositionSegment.csv
        ├── Retarder.csv
        └── Switch.csv
```

## 核心功能

### 1. 用户认证系统
- JWT 令牌认证机制
- 前端密码 SHA-256 哈希
- 后端 Argon2 密码加密存储
- Axios 全局请求拦截器自动添加 Token
- Token 过期自动跳转登录页

### 2. 驼峰作业模块
- **站场布局**: 可视化站场平纵断面图
- **坡度设计**: 驼峰坡度设计与优化
- **溜放仿真**: 车辆溜放过程动态模拟
- **速度曲线**: 车辆速度-时间曲线计算
- **时间曲线**: 车辆位移-时间曲线分析
- **能量计算**: 阻力能耗高度、动能高度计算
- **车辆参数**: 车辆概念及参数设置
- **间隔时间检验**: 追踪间隔时间安全检验

### 3. 课程模块
- 教学资源管理
- 课程内容展示

## 安装与运行

### 前端项目

#### 环境要求
- Node.js 20.19.0+ 或 22.12.0+
- npm 或 yarn

#### 安装步骤
```bash
cd switchyard-vue
npm install
```

#### 开发模式运行
```bash
npm run dev
```

#### 生产构建
```bash
npm run build
```

#### 预览生产构建
```bash
npm run preview
```

### 后端项目

#### 环境要求
- .NET 8.0 SDK
- Visual Studio 2022 或 VS Code

#### 配置文件
在 `SwitchYard.Service/appsettings.json` 中配置：
```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-here-must-be-at-least-32-characters",
    "Issuer": "SwitchYard.Service",
    "Audience": "SwitchYard.Client",
    "ExpirationMinutes": 60
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=database.db"
  }
}
```

#### 运行项目
```bash
cd SwitchYard.WebApi/SwitchYard.Service
dotnet restore
dotnet run
```

API 将在 `https://localhost:5001` 或 `http://localhost:5000` 上运行。

#### 访问 Swagger API 文档
开发模式下访问: `https://localhost:5001/swagger`

## 配置说明

### 前端配置 (config.json)
```json
{
  "serverurl": "https://localhost:5001"
}
```

### 后端配置
- **JWT 认证**: 在 `appsettings.json` 中配置密钥、签发者、受众和过期时间
- **数据库连接**: 支持 SQLite 和 MySQL，在 `ConnectionStrings` 中配置
- **CORS**: 已配置允许跨域请求

## API 接口

### 认证接口
- `POST /api/Auth/login` - 用户登录
- `GET /api/Auth/validate` - 验证 Token

### 驼峰业务接口
- `GET /hump/getslopelayout` - 获取坡段布局
- `GET /hump/getflatlayout` - 获取平面布局
- `GET /hump/getwagonconcept` - 获取车辆概念
- `POST /hump/getresistanceenergyheight` - 计算阻力能耗高度
- `POST /hump/getkineticenergyheight` - 计算动能高度
- `POST /hump/GetVelocityCurve` - 获取速度曲线
- `POST /hump/GetTimeCurve` - 获取时间曲线

## 开发指南

### 前端开发
1. 所有 API 请求使用已配置的 Axios 实例，自动携带 JWT Token
2. 路由配置在 `src/router/index.ts`
3. 全局 Axios 配置在 `src/utils/axios.ts`
4. 页面组件放在 `src/views/`，业务组件放在对应模块文件夹

### 后端开发
1. 新增 API 接口在 `Controllers/` 文件夹创建控制器
2. 业务逻辑放在 `Services/` 文件夹
3. 需要认证的接口添加 `[Authorize]` 特性
4. 数据模型定义在 `Models/` 文件夹

## 安全特性

1. **双重密码保护**:
   - 前端: SHA-256 哈希后传输
   - 后端: Argon2 加密存储

2. **JWT 认证**:
   - Token 过期时间可配置
   - 自动刷新机制
   - 统一错误处理

3. **HTTPS 支持**: 生产环境强制 HTTPS

## 参与贡献

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 提交 Pull Request

## 许可证

查看 [LICENSE](LICENSE) 文件了解详情。

## 联系方式

项目维护者: 北京交通大学交通运输学院 “铁路站场与枢纽”课程组 廖正文 等

项目链接: [https://gitee.com/your-repo/SwitchYard](https://gitee.com/your-repo/SwitchYard)
