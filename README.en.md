
# SwitchYard

## Project Overview

SwitchYard is an educational toolkit for railway yards and hub operations. It provides hump-yard calculations, rolling/shunting simulation, yard layout design, and teaching resources. The project uses a frontend-backend separation: the frontend is a Vue 3 + TypeScript application and the backend is an ASP.NET Core Web API (.NET 8).

## Tech Stack

### Frontend (`switchyard-vue`)
- Framework: Vue 3.5.x + TypeScript
- Build tool: Vite
- UI library: Element Plus
- Router: Vue Router
- HTTP client: Axios
- Other: jQuery

### Backend (`SwitchYard.WebApi`)
- Framework: ASP.NET Core 8.0
- Language: C# (.NET 8.0)
- Authentication: JWT (JSON Web Tokens)
- Database: SQLite / MySQL (via Dapper)
- API docs: Swagger / OpenAPI
- Security: Argon2 password hashing

## Project Structure

```
SwitchYard/
├── switchyard-vue/                          # Frontend Vue application
│   ├── src/
│   │   ├── assets/                          # Static assets (base.css, main.css)
│   │   ├── components/                      # Shared components and icons
│   │   ├── hump/                            # Hump yard features (many Vue components)
│   │   ├── capacity/                        # Capacity analysis UI
│   │   ├── course/                          # Course module UI
│   │   ├── views/                           # Page views (Home, Login, About, user pages)
│   │   ├── router/                          # Router config
│   │   ├── utils/                           # Utilities (axios.ts)
│   │   ├── locales/                         # i18n files (en.json, zh.json)
│   │   ├── config.*                         # env configs and config.ts
│   │   └── main.ts / App.vue                # App entry
│   ├── public/                              # Static public resources
│   ├── package.json                         # Frontend dependencies and scripts
│   └── vite.config.ts                       # Vite configuration
│
├── SwitchYard.WebApi/                       # Backend Web API
│   └── SwitchYard.Service/                  # Web API service implementation
│       ├── Controllers/                     # API controllers (AuthController, HumpController)
│       ├── Models/                          # Data models (User, LoginRequest/Response, CreateUser...)
│       ├── Services/                        # Business services (JwtTokenService, UserService, InstanceAuthorizationService)
│       ├── Utils/                           # Utilities (SnowflakeIdGenerator, helpers)
│       ├── Database/                        # DB layer and connectors
│       ├── Program.cs                       # App startup
│       ├── appsettings*.json                # Environment configs
│       └── SwitchYard.Service.csproj        # Project file
│
└── SwitchYard.Hump/                         # Hump calculation core library
    ├── HumpCalculation.cs
    ├── HumpScheme.cs
    ├── HumpInstance.cs
    ├── HumpResistanceCalculator.cs
    ├── EnergyHeightCalculator.cs
    ├── Position.cs
    ├── Wagon.cs
    ├── RetarderStatus.cs
    └── HumpDatabase/                        # SQLite database (hump.db)

└── LocalData/                               # Local CSV data for hump module
    └── Hump/
        ├── Position.csv
        ├── PositionSegment.csv
        ├── Retarder.csv
        └── Switch.csv
```

## Key Features

### 1. Authentication & User Management
- JWT-based stateless authentication
- Dual-layer password protection:
  - Client-side: SHA-256 hashing (Crypto-JS)
  - Server-side: Argon2 hashing for storage
- User management: register, login, profile, permissions and instance authorization
- Token management: Axios interceptor attaches token, auto-redirect on expiry, token refresh support

### 2. Hump Yard Design & Simulation (Hump Module)

#### 2.1 Yard Layout Management
- Plan and profile (plan & longitudinal section) visualization and editing
- Real-time layout updates and controls

#### 2.2 Slope Design & Optimization
- Design hump slopes (approach, settling, rolling slopes)
- Visual slope sketch and parameter optimization
- Slope validation against operation constraints

#### 2.3 Rolling / Shunting Simulation
- Dynamic simulation of wagon rolling on hump
- Real-time calculation of speed, displacement, acceleration
- Multi-condition support (vehicle types, conditions)
- Outputs: velocity-time and displacement-time curves

#### 2.4 Energy Calculations & Analysis
- Resistance (drag) energy-height calculations
- Kinetic energy height calculations
- Energy balance analysis and retarder (brake) effectiveness evaluation

#### 2.5 Headway / Interval Checks
- Check safe follow-up intervals between wagons
- Collision warnings and safety assessment

#### 2.6 Wagon Parameter Management
- Define and manage wagon types and parameters (mass, resistance coefficients)
- Maintain a wagon parameter library

### 3. Capacity Analysis (Capacity Module)
- Yard capacity evaluation and throughput analysis
- Layout optimization suggestions based on capacity

### 4. Course & Teaching Module
- Teaching resource management and course content presentation
- Case library for real engineering examples

### 5. Internationalization (i18n)
- Chinese and English support
- Configurable translations and easy to add more languages

## Installation & Running

### Frontend

Requirements:
- Node.js 20.19.0+ or 22.12.0+
- npm or yarn

Install dependencies:
```bash
cd switchyard-vue
npm install
```

Run in development mode:
```bash
npm run dev
```

Build for production:
```bash
npm run build
```

Preview production build:
```bash
npm run preview
```

### Backend

Requirements:
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

Run the service:
```bash
cd SwitchYard.WebApi/SwitchYard.Service
dotnet restore
dotnet run
```

The API will listen on `https://localhost:5001` (HTTPS) and `http://localhost:5000` (HTTP) by default.
Swagger UI is available in development at `https://localhost:5001/swagger` or `http://localhost:5000/swagger`.

## Configuration

### Frontend configuration (environment files)
- Development: `src/config.development.json`
```json
{
  "serverurl": "https://localhost:7297"
}
```
- Production: `src/config.production.json`
```json
{
  "serverurl": "https://api.kapparail.com:8080"
}
```

### Backend configuration (`appsettings.json`)
- Example keys and sections present in `SwitchYard.Service/appsettings.json`:
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "WebApi": { "Hosts": [ "http://localhost:5000", "https://localhost:5001" ] },
  "Cors": { "AllowedOrigins": [ "http://localhost:5173", "https://localhost:5173", "http://localhost:3000", "https://localhost:3000" ] },
  "Jwt": {
    "SecretKey": "SwitchYard_JWT_Secret_Key_2024_MinLength32Characters_ForSecurity",
    "Issuer": "SwitchYard.Service",
    "Audience": "SwitchYard.Client",
    "ExpirationMinutes": "60"
  },
  "HumpDatabase": {
    "DatabaseType": "Sqllite",
    "MysqlConfig": { "Host": "127.0.0.1", "Port": 3306, "Database": "database_name", "Username": "root", "Password": "password" },
    "SqlliteConfig": { "DatabaseFile": "path/to/hump.db" }
  }
}
```

## API Endpoints

### Authentication (`AuthController`)
- `POST /api/Auth/login` - User login
- `POST /api/Auth/register` - User register
- `POST /api/Auth/validate` - Validate token

### Hump Business APIs (`HumpController`)
- `GET /api/Hump/getslopelayout` - Get slope layout
- `GET /api/Hump/getflatlayout` - Get plan layout
- `GET /api/Hump/getwagonconcept` - Get wagon concepts
- `POST /api/Hump/getresistanceenergyheight` - Calculate resistance energy height
- `POST /api/Hump/getkineticenergyheight` - Calculate kinetic energy height
- `POST /api/Hump/GetVelocityCurve` - Get velocity curve
- `POST /api/Hump/GetTimeCurve` - Get time curve

Detailed API docs available in Swagger: `http://localhost:5000/swagger` or `https://localhost:5001/swagger`.

## Development Guide

### Frontend
1. All API requests use the configured Axios instance and automatically include JWT token.
2. Router config in `src/router/index.ts`.
3. Global Axios configuration in `src/utils/axios.ts`.
4. Page views in `src/views/`, feature components in module folders (e.g., `src/hump/`).

### Backend
1. Add controllers under `Controllers/` to expose new APIs.
2. Business logic belongs in `Services/`.
3. Protect endpoints with `[Authorize]` as needed.
4. Define data models in `Models/`.

## Security Features

1. Dual-layer password protection:
   - Client-side: SHA-256 before transmission
   - Server-side: Argon2 for storage
2. JWT authentication with configurable expiration and refresh support
3. HTTPS recommended/required in production

## Contributing

1. Fork this repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m "Add AmazingFeature"`)
4. Push to your branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

See [LICENSE](LICENSE) for details.

## Contact

Maintainers: Beijing Jiaotong University — Railway Yards & Hubs course team (Liao Zhengwen et al.)

Project repository: https://gitee.com/lzw37/SwitchYard
