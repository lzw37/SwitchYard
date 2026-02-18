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
├── switchyard-vue/                          # 前端 Vue 应用
│   ├── src/
│   │   ├── assets/                          # 静态资源
│   │   │   ├── base.css
│   │   │   └── main.css
│   │   ├── components/                      # 通用组件
│   │   │   ├── HelloWorld.vue
│   │   │   ├── TheWelcome.vue
│   │   │   ├── WelcomeItem.vue
│   │   │   └── icons/                       # 图标组件
│   │   ├── hump/                            # 驼峰作业模块
│   │   │   ├── HumpMain.vue                 # 驼峰主界面
│   │   │   ├── HumpLayout.vue               # 站场布局视图
│   │   │   ├── HumpLayoutCtrl.vue           # 布局控制
│   │   │   ├── HumpSim.vue                  # 溜放仿真
│   │   │   ├── HumpSlopeCtrl.vue            # 坡度控制
│   │   │   ├── HumpSlopeDesigner.vue        # 坡度设计器
│   │   │   ├── HumpSlopeSketchBlock.vue     # 坡度草图块
│   │   │   ├── HumpCalculationCondition.vue # 计算条件设置
│   │   │   ├── HumpInstanceManager.vue      # 实例管理
│   │   │   ├── HumpTimeCurve.vue            # 时间曲线
│   │   │   ├── HumpVelocityCurve.vue        # 速度曲线
│   │   │   ├── HumpHeadwayCheck.vue         # 间隔时间检验
│   │   │   ├── Wagon.vue                    # 车辆组件
│   │   │   ├── humplayoutctrl.css           # 布局控制样式
│   │   │   └── humplayoutctrl.ts            # 布局控制逻辑
│   │   ├── capacity/                        # 容量模块
│   │   │   ├── CapacityMain.vue             # 容量主界面
│   │   │   ├── StationLayout.vue            # 站场布局
│   │   │   └── components/                  # 容量组件
│   │   ├── course/                          # 课程模块
│   │   │   └── CourseMain.vue               # 课程主界面
│   │   ├── views/                           # 页面视图
│   │   │   ├── HomeView.vue                 # 首页
│   │   │   ├── Login.vue                    # 登录页面
│   │   │   ├── CreateUser.vue               # 用户创建页面
│   │   │   ├── UserInfo.vue                 # 用户信息页面
│   │   │   ├── UserManager.vue              # 用户管理页面
│   │   │   └── AboutView.vue                # 关于页面
│   │   ├── router/                          # 路由配置
│   │   │   └── index.ts                     # 路由定义
│   │   ├── utils/                           # 工具函数
│   │   │   └── axios.ts                     # Axios 全局配置
│   │   ├── locales/                         # 国际化文件
│   │   │   ├── en.json                      # 英文翻译
│   │   │   └── zh.json                      # 中文翻译
│   │   ├── config.ts                        # 配置管理器
│   │   ├── config.development.json          # 开发环境配置
│   │   ├── config.production.json           # 生产环境配置
│   │   ├── i18n.ts                          # 国际化配置
│   │   ├── App.vue                          # 根组件
│   │   └── main.ts                          # 应用入口
│   ├── public/                              # 静态公共资源
│   │   └── _redirects
│   ├── package.json                         # 项目依赖
│   ├── vite.config.ts                       # Vite 配置
│   ├── tsconfig.json                        # TypeScript 配置
│   ├── tsconfig.app.json                    # 应用 TS 配置
│   ├── tsconfig.node.json                   # 节点 TS 配置
│   ├── env.d.ts                             # 环境类型定义
│   └── index.html                           # HTML 入口
│
├── SwitchYard.WebApi/                       # 后端 Web API
│   └── SwitchYard.Service/                  # Web API 主服务
│       ├── Controllers/                     # API 控制器
│       │   ├── AuthController.cs            # 用户认证接口
│       │   └── HumpController.cs            # 驼峰业务接口
│       ├── Models/                          # 数据模型
│       │   ├── User.cs                      # 用户模型
│       │   ├── LoginRequest.cs              # 登录请求
│       │   ├── LoginResponse.cs             # 登录响应
│       │   ├── CreateUserRequest.cs         # 创建用户请求
│       │   └── CreateUserResponse.cs        # 创建用户响应
│       ├── Services/                        # 业务服务
│       │   ├── JwtTokenService.cs           # JWT 令牌服务
│       │   ├── UserService.cs               # 用户业务逻辑
│       │   └── InstanceAuthorizationService.cs  # 实例权限校验
│       ├── Utils/                           # 工具类
│       │   └── SnowflakeIdGenerator.cs      # 雪花算法 ID 生成
│       ├── Database/                        # 数据库连接层
│       ├── Documentation/                   # API 文档
│       ├── DBConnector.cs                   # 数据库连接器
│       ├── Program.cs                       # 应用程序入口
│       ├── SwitchYard.Service.http          # HTTP 测试文件
│       ├── Postman_PreRequest_Script.js     # Postman 脚本
│       ├── appsettings.json                 # 基础配置
│       ├── appsettings.Development.json     # 开发配置
│       ├── appsettings.Production.json      # 生产配置
│       └── SwitchYard.Service.csproj        # 项目文件
│
│   └── SwitchYard.Hump/                     # 驼峰计算核心库
│       ├── HumpCalculation.cs               # 驼峰计算主类
│       ├── HumpScheme.cs                    # 驼峰方案
│       ├── HumpInstance.cs                  # 驼峰实例
│       ├── HumpResistanceCalculator.cs      # 阻力计算器
│       ├── EnergyHeightCalculator.cs        # 能耗高度计算器
│       ├── OperationCondition.cs            # 操作条件
│       ├── Position.cs                      # 位置数据模型
│       ├── Wagon.cs                         # 车辆模型
│       ├── RetarderStatus.cs                # 缓速器状态
│       ├── HumpDatabase/                    # 驼峰数据库
│       │   └── hump.db                      # SQLite 数据库文件
│       └── SwitchYard.Hump.csproj           # 项目文件
│
└── LocalData/                               # 本地数据文件
    └── Hump/                                # 驼峰相关数据
        ├── Position.csv                    # 位置数据
        ├── PositionSegment.csv             # 位置段数据
        ├── Retarder.csv                    # 缓速器数据
        └── Switch.csv                      # 道岔数据
```

## 核心功能

### 1. 用户认证与管理系统
- **JWT 令牌认证**: 采用 JWT 基于令牌的无状态认证
- **双重密码保护**:
  - 前端: 使用 SHA-256/Crypto-JS 对密码进行哈希
  - 后端: 使用 Argon2 算法加密存储
- **用户管理**:
  - 用户注册与创建
  - 用户登录与验证
  - 用户信息管理
  - 权限控制与实例授权
- **Token 管理**:
  - Axios 全局拦截器自动附加认证令牌
  - Token 过期自动跳转登录页
  - 支持 Token 刷新

### 2. 驼峰作业设计与仿真模块 (Hump 模块)
#### 2.1 站场布局管理
- **平面布局设计**: 可视化编辑驼峰站场平面布置图
- **纵断面设计**: 支持站场纵向坡度规划
- **动态展示**: 实时更新站场布局信息

#### 2.2 坡度设计与优化
- **坡度设计工具**: 设计驼峰进车坡、展开坡、溜放坡
- **坡度草图**: 可视化绘制坡度曲线
- **参数优化**: 根据作业条件优化坡度参数
- **坡度验证**: 检验坡度设计是否符合作业要求

#### 2.3 车辆溜放仿真
- **动态仿真**: 模拟车辆在驼峰上的溜放过程
- **实时计算**: 计算车辆速度、位移、加速度变化
- **多工况支持**: 支持不同车辆类型和作业条件
- **仿真结果**: 输出速度-时间曲线、位移-时间曲线

#### 2.4 能量计算与分析
- **阻力能耗高度计算**: 计算克服阻力所需的能耗高度
- **动能高度计算**: 计算车辆动能对应的等效高度
- **能量平衡分析**: 验证能量平衡关系
- **缓速器效能评估**: 评估缓速器的制动效果

#### 2.5 间隔时间检验
- **追踪间隔检验**: 检验两车间的追踪安全间隔
- **碰撞预警**: 预警可能的车辆碰撞情况
- **作业安全评估**: 综合评估作业方案的安全性

#### 2.6 车辆参数管理
- **车辆概念**: 定义和管理不同类型的车辆
- **车辆属性**: 设置车辆的质量、阻力系数等参数
- **车辆库管理**: 维护常用车辆参数库

### 3. 能力分析模块 (Capacity 模块)
- **站场容量评估**: 分析车站能力和瓶颈
- **通过能力分析**: 评估车站通过能力
- **站场布局优化**: 基于能力分析的布局优化建议

### 4. 课程教学模块 (Course 模块)
- **教学资源管理**: 组织和管理教学相关资源
- **课程内容展示**: 展示课程相关的理论知识
- **案例库**: 提供实际工程案例参考

### 5. 国际化支持 (i18n)
- **多语言支持**: 支持中文和英文界面切换
- **完整翻译**: 涵盖所有主要功能和菜单
- **灵活配置**: 易于添加新的语言支持

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
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "WebApi": {
    "Hosts": [ "http://localhost:5000", "https://localhost:5001" ]
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://localhost:5173",
      "http://localhost:3000",
      "https://localhost:3000"
    ]
  },
  "Jwt": {
    "SecretKey": "SwitchYard_JWT_Secret_Key_2024_MinLength32Characters_ForSecurity",
    "Issuer": "SwitchYard.Service",
    "Audience": "SwitchYard.Client",
    "ExpirationMinutes": "60"
  },
  "HumpDatabase": {
    "DatabaseType": "Sqllite",
    "MysqlConfig": {
      "Host": "127.0.0.1",
      "Port": 3306,
      "Database": "database_name",
      "Username": "root",
      "Password": "password"
    },
    "SqlliteConfig": {
      "DatabaseFile": "path/to/hump.db"
    }
  }
}
```

#### 运行项目
```bash
cd SwitchYard.WebApi/SwitchYard.Service
dotnet restore
dotnet run
```

API 将在 `https://localhost:5001`（HTTPS）或 `http://localhost:5000`（HTTP）上运行。

#### 访问 Swagger API 文档
开发模式下访问: 
- HTTPS: `https://localhost:5001/swagger`
- HTTP: `http://localhost:5000/swagger`

## 配置说明

### 前端配置 (config.development.json / config.production.json)

**开发环境配置** (`src/config.development.json`)：
```json
{
  "serverurl": "https://localhost:7297"
}
```

**生产环境配置** (`src/config.production.json`)：
```json
{
  "serverurl": "https://api.kapparail.com:8080"
}
```

### 后端配置
- **JWT 认证**: 在 `appsettings.json` 中配置密钥、签发者、受众和过期时间
- **数据库连接**: 支持 SQLite 和 MySQL，在 `ConnectionStrings` 中配置
- **CORS**: 已配置允许跨域请求

## API 接口

### 认证接口 (AuthController)
- `POST /api/Auth/login` - 用户登录
- `POST /api/Auth/register` - 用户注册
- `POST /api/Auth/validate` - 验证 Token

### 驼峰业务接口 (HumpController)
- `GET /api/Hump/getslopelayout` - 获取坡段布局
- `GET /api/Hump/getflatlayout` - 获取平面布局
- `GET /api/Hump/getwagonconcept` - 获取车辆概念
- `POST /api/Hump/getresistanceenergyheight` - 计算阻力能耗高度
- `POST /api/Hump/getkineticenergyheight` - 计算动能高度
- `POST /api/Hump/GetVelocityCurve` - 获取速度曲线
- `POST /api/Hump/GetTimeCurve` - 获取时间曲线

详细的 API 文档请访问 Swagger UI：`http://localhost:5000/swagger` 或 `https://localhost:5001/swagger`

## 开发指南

### 前端开发
1. 所有 API 请求使用已配置的 Axios 实例，自动携带 JWT Token
2. 路由配置在 `src/router/index.ts`
3. 全局 Axios 配置在 `src/utils/axios.ts`
4. 页面组件放在 `src/views/`，业务组件放在对应模块文件夹

### 后端开发
1. 新增 API 接口在 `Controllers/` 文件夹创建控制器
   - `AuthController.cs` - 用户认证接口
   - `HumpController.cs` - 驼峰业务接口
2. 业务逻辑放在 `Services/` 文件夹
   - `JwtTokenService.cs` - JWT 令牌管理
   - `UserService.cs` - 用户业务逻辑
   - `InstanceAuthorizationService.cs` - 实例权限校验
3. 需要认证的接口添加 `[Authorize]` 特性
4. 数据模型定义在 `Models/` 文件夹
   - `User.cs` - 用户模型
   - `LoginRequest.cs` / `LoginResponse.cs` - 登录请求/响应
   - `CreateUserRequest.cs` / `CreateUserResponse.cs` - 创建用户请求/响应

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

项目链接: [https://gitee.com/lzw37/SwitchYard](https://gitee.com/lzw37/SwitchYard)
