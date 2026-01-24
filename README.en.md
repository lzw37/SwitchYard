
# SwitchYard

## Project Overview

SwitchYard is an educational toolkit for railway yards and hubs. It provides features such as hump yard calculations, train rolling (shunting) simulation, and yard layout design. The project uses a frontend-backend separated architecture: the frontend is built with Vue 3 and the backend is an ASP.NET Core Web API.

## Tech Stack

### Frontend (switchyard-vue)
- Framework: Vue 3.5.x + TypeScript
- Build tool: Vite
- UI: Element Plus
- Router: Vue Router
- HTTP client: Axios
- Other: jQuery

### Backend (SwitchYard.WebApi)
- Framework: ASP.NET Core 8.0
- Language: C# (.NET 8.0)
- Authentication: JWT (JSON Web Tokens)
- Database: SQLite / MySQL (via Dapper)
- API docs: Swagger / OpenAPI
- Security: Argon2 password hashing

## Project Structure

```
SwitchYard/
├── switchyard-vue/              # Frontend Vue application
│   ├── src/
│   │   ├── assets/              # Static assets
│   │   ├── components/          # Shared components
│   │   ├── hump/                # Hump yard related modules
│   │   │   ├── HumpMain.vue     # Hump main view
│   │   │   ├── HumpLayout.vue   # Yard layout
│   │   │   ├── HumpSim.vue      # Rolling/rolling-simulation
│   │   │   └── ...
│   │   ├── course/              # Course module
│   │   ├── router/              # Router configuration
│   │   ├── views/               # Page views
│   │   │   ├── Login.vue        # Login page
│   │   │   ├── HomeView.vue     # Home page
│   │   │   └── AboutView.vue    # About page
│   │   ├── utils/               # Utility functions
│   │   │   └── axios.ts         # Axios global config
│   │   ├── App.vue              # Root component
│   │   └── main.ts              # App entry
│   ├── package.json
│   └── vite.config.ts
│
├── SwitchYard.WebApi/           # Backend Web API
│   ├── SwitchYard.Service/      # Web API service
│   │   ├── Controllers/         # API controllers
│   │   │   ├── AuthController.cs    # Authentication controller
│   │   │   └── HumpController.cs    # Hump business controller
│   │   ├── Models/              # Data models
│   │   │   ├── User.cs
│   │   │   ├── LoginRequest.cs
│   │   │   └── LoginResponse.cs
│   │   ├── Services/            # Business services
│   │   │   ├── JwtTokenService.cs   # JWT token service
│   │   │   └── UserService.cs       # User service
│   │   ├── Program.cs           # App entry
│   │   └── appsettings.json     # Config
│   │
│   └── SwitchYard.Hump/         # Hump calculation core library
│       ├── HumpCalculator.cs
│       ├── EnergyHeightCalculator.cs
│       ├── Position.cs
│       └── Wagon.cs
│
└── LocalData/                   # Local data files
    └── Hump/                    # Hump related data
        ├── Position.csv
        ├── PositionSegment.csv
        ├── Retarder.csv
        └── Switch.csv
```

## Key Features

### 1. Authentication
- JWT token based authentication
- Client-side SHA-256 hashing before transmission
- Server-side secure storage using Argon2
- Axios global request interceptor to attach token
- Auto-redirect to login on token expiry

### 2. Hump Yard Module
- Yard layout visualization (plan and profile)
- Slope design and optimization for hump yards
- Rolling (shunting) simulation of wagons
- Velocity-time curve computations
- Displacement-time (time) curve analysis
- Energy calculations: resistance/kinetic energy heights
- Wagon concepts and parameter settings
- Headway (interval) safety checks

### 3. Course Module
- Teaching resource management
- Course content presentation

## Installation & Running

### Frontend

Requirements:
- Node.js (recommended 20.x or 22.x)
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

Configuration (SwitchYard.Service/appsettings.json):
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

Run the service:
```bash
cd SwitchYard.WebApi/SwitchYard.Service
dotnet restore
dotnet run
```

The API will be available at `https://localhost:5001` or `http://localhost:5000`.

Open the Swagger UI in development mode at: `https://localhost:5001/swagger`

## Configuration

### Frontend (`config.json`)
```json
{
  "serverurl": "https://localhost:5001"
}
```

### Backend
- Configure JWT keys, issuer, audience and expiration in `appsettings.json`
- Database connection strings support SQLite and MySQL
- CORS is configured to allow cross-origin requests

## API Endpoints

### Authentication
- `POST /api/Auth/login` - User login
- `GET /api/Auth/validate` - Validate token

### Hump Business APIs
- `GET /hump/getslopelayout` - Get slope layout
- `GET /hump/getflatlayout` - Get plan layout
- `GET /hump/getwagonconcept` - Get wagon concepts
- `POST /hump/getresistanceenergyheight` - Calculate resistance energy height
- `POST /hump/getkineticenergyheight` - Calculate kinetic energy height
- `POST /hump/GetVelocityCurve` - Get velocity curve
- `POST /hump/GetTimeCurve` - Get time curve

## Development Guide

### Frontend
1. All API requests use the configured Axios instance and automatically include the JWT token.
2. Router is configured in `src/router/index.ts`.
3. Global Axios config is in `src/utils/axios.ts`.
4. Page views are in `src/views/`, feature components are in their module folders.

### Backend
1. Add new APIs by creating controllers in the `Controllers/` folder.
2. Business logic belongs in the `Services/` folder.
3. Protect endpoints with `[Authorize]` where needed.
4. Define data models in the `Models/` folder.

## Security Features

1. Dual-layer password protection:
   - Client: SHA-256 hashed before transmission
   - Server: Argon2 hashed for storage

2. JWT authentication:
   - Token expiration configurable
   - Auto-refresh mechanism
   - Centralized error handling

3. HTTPS enforced in production

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add AmazingFeature'`)
4. Push to your branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

See the [LICENSE](LICENSE) file for details.

## Contact

Maintainers: Liao Zhengwen and the Railway Yards & Hubs course team, Beijing Jiaotong University

Project: https://github.com/lzw37/SwitchYard
