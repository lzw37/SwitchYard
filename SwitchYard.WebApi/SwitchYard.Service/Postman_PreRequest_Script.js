/**
 * Postman Pre-request Script for JWT Authentication (with Refresh Token support)
 * 用于自动处理JWT Token的获取和无感刷新
 *
 * 使用说明：
 * 1. 在Postman的Collection或Folder级别设置此脚本
 * 2. 配置环境变量：
 *    - base_url: API基础URL (例如: http://localhost:5000)
 *    - username: 用户名 (例如: admin)
 *    - password: 密码 (例如: admin123)
 * 3. 脚本会自动管理以下环境变量：
 *    - jwt_token:           Access Token
 *    - token_expiry:        Access Token 过期时间戳（秒）
 *    - refresh_token:       Refresh Token
 *    - refresh_token_expiry: Refresh Token 过期时间戳（秒）
 */

// ============================================================================
// 配置区域
// ============================================================================

const CONFIG = {
    // 从环境变量获取配置
    baseUrl: pm.environment.get("base_url") || "http://localhost:5000",
    username: pm.environment.get("username") || "admin",
    password: pm.environment.get("password") || "admin123",
    loginEndpoint: "/api/Auth/login",
    refreshEndpoint: "/api/Auth/refresh",

    // Access Token 过期前提前刷新的时间（秒）
    refreshBeforeExpiry: 60
};

// ============================================================================
// 辅助函数
// ============================================================================

/**
 * 检查Token是否存在
 */
function hasToken() {
    const token = pm.environment.get("jwt_token");
    return token && token.length > 0;
}

/**
 * 检查Token是否已过期或即将过期
 */
function isTokenExpired() {
    const expiryTimestamp = pm.environment.get("token_expiry");
    
    if (!expiryTimestamp) {
        return true; // 没有过期时间信息，认为已过期
    }
  
    const now = Math.floor(Date.now() / 1000); // 当前时间戳（秒）
    const expiry = parseInt(expiryTimestamp);
    
    // 如果Token在配置的提前时间内过期，则认为需要刷新
    return (expiry - now) <= CONFIG.refreshBeforeExpiry;
}

/**
 * 从JWT Token中解析payload
 */
function parseJwtPayload(token) {
    try {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(
      atob(base64).split('').map(function(c) {
return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join('')
     );
     return JSON.parse(jsonPayload);
    } catch (e) {
        console.error("解析JWT Token失败:", e);
  return null;
    }
}

/**
 * 检查 Refresh Token 是否有效（存在且未过期）
 */
function hasValidRefreshToken() {
    const refreshToken = pm.environment.get("refresh_token");
    if (!refreshToken || refreshToken.length === 0) return false;
    const expiry = pm.environment.get("refresh_token_expiry");
    if (!expiry) return false;
    const now = Math.floor(Date.now() / 1000);
    return parseInt(expiry) > now + 10; // 至少还有10秒
}

/**
 * 使用 Refresh Token 无感刷新 Access Token
 */
function refreshAccessToken() {
    const refreshToken = pm.environment.get("refresh_token");
    const refreshUrl = CONFIG.baseUrl + CONFIG.refreshEndpoint;

    const refreshRequest = {
        url: refreshUrl,
        method: 'POST',
        header: { 'Content-Type': 'application/json' },
        body: {
            mode: 'raw',
            raw: JSON.stringify({ refreshToken: refreshToken })
        }
    };

    console.log("[JWT Auth] 正在使用 Refresh Token 刷新 Access Token...");

    pm.sendRequest(refreshRequest, function (err, response) {
        if (err || response.code !== 200) {
            console.warn("[JWT Auth] Refresh Token 刷新失败，尝试重新登录...");
            login();
            return;
        }

        try {
            const body = response.json();
            saveTokens(body.token || body.Token, body.expiresIn || body.ExpiresIn,
                       body.refreshToken || body.RefreshToken, body.refreshTokenExpiresIn || body.RefreshTokenExpiresIn);
            console.log("[JWT Auth] ✅ Access Token 刷新成功");
        } catch (e) {
            console.error("[JWT Auth] 解析刷新响应失败:", e);
            login();
        }
    });
}

/**
 * 保存 Token 信息到环境变量
 */
function saveTokens(accessToken, expiresIn, refreshToken, refreshExpiresIn) {
    const now = Math.floor(Date.now() / 1000);

    if (accessToken) {
        const payload = parseJwtPayload(accessToken);
        const expiry = (payload && payload.exp) ? payload.exp : (now + (expiresIn || 1800));
        pm.environment.set("jwt_token", accessToken);
        pm.environment.set("token_expiry", expiry.toString());
    }

    if (refreshToken) {
        const refreshExpiry = now + (refreshExpiresIn || 604800); // 默认7天
        pm.environment.set("refresh_token", refreshToken);
        pm.environment.set("refresh_token_expiry", refreshExpiry.toString());
    }
}

/**
 * 登录并获取 Access Token + Refresh Token
 */
function login() {
    const loginUrl = CONFIG.baseUrl + CONFIG.loginEndpoint;
    
    const loginRequest = {
        url: loginUrl,
        method: 'POST',
        header: {
            'Content-Type': 'application/json'
        },
        body: {
          mode: 'raw',
   raw: JSON.stringify({
           username: CONFIG.username,
 password: CONFIG.password
            })
        }
    };
    
    console.log(`[JWT Auth] 正在登录用户: ${CONFIG.username}...`);
    
    pm.sendRequest(loginRequest, function (err, response) {
        if (err) {
       console.error("[JWT Auth] 登录请求失败:", err);
            throw new Error("无法获取JWT Token，登录请求失败");
    }
        
        if (response.code !== 200) {
            console.error("[JWT Auth] 登录失败，状态码:", response.code);
            console.error("[JWT Auth] 响应内容:", response.text());
            throw new Error(`登录失败，状态码: ${response.code}`);
    }
        
        try {
            const responseBody = response.json();
            
    if (!responseBody.token) {
   console.error("[JWT Auth] 登录响应中没有token字段");
       throw new Error("登录响应格式错误");
       }
            
            const token = responseBody.token;
            const refreshToken = responseBody.refreshToken;

            saveTokens(token, responseBody.expiresIn, refreshToken, responseBody.refreshTokenExpiresIn);

            console.log(`[JWT Auth] ✅ 登录成功！用户: ${responseBody.username || CONFIG.username}, 角色: ${responseBody.role || 'Unknown'}`);
     
        } catch (e) {
          console.error("[JWT Auth] 解析登录响应失败:", e);
            throw new Error("无法解析登录响应");
        }
    });
}

// ============================================================================
// 主逻辑
// ============================================================================

try {
    // 检查是否需要获取或刷新 Access Token
    if (!hasToken()) {
        console.log("[JWT Auth] 未找到 Access Token，正在登录...");
        if (hasValidRefreshToken()) {
            refreshAccessToken();
        } else {
            login();
        }
    } else if (isTokenExpired()) {
        console.log("[JWT Auth] Access Token 已过期或即将过期...");
        if (hasValidRefreshToken()) {
            // 优先用 Refresh Token 静默刷新，无需重新登录
            refreshAccessToken();
        } else {
            console.log("[JWT Auth] Refresh Token 不可用，重新登录...");
            login();
        }
    } else {
        console.log("[JWT Auth] ✅ Token 有效，跳过登录");

        // 显示当前Token信息
        const token = pm.environment.get("jwt_token");
        const payload = parseJwtPayload(token);
        if (payload) {
            const expiry = pm.environment.get("token_expiry");
            const now = Math.floor(Date.now() / 1000);
            const remaining = parseInt(expiry) - now;
            console.log(`[JWT Auth] Access Token 剩余有效时间: ${Math.floor(remaining / 60)}分${remaining % 60}秒`);
        }
    }
} catch (error) {
    console.error("[JWT Auth] 发生错误:", error.message);
    // 清除无效的 Token
    pm.environment.unset("jwt_token");
    pm.environment.unset("token_expiry");
    pm.environment.unset("refresh_token");
    pm.environment.unset("refresh_token_expiry");
}

// ============================================================================
// 自动设置Authorization Header
// ============================================================================

// 如果当前请求不是登录请求，则自动添加Authorization头
const currentUrl = pm.request.url.toString();
if (!currentUrl.includes("/api/Auth/login")) {
    const token = pm.environment.get("jwt_token");
    if (token) {
     pm.request.headers.add({
            key: 'Authorization',
       value: `Bearer ${token}`
        });
        console.log("[JWT Auth] ? 已自动添加Authorization头");
    }
}
