/**
 * Postman Pre-request Script for JWT Authentication
 * 用于自动处理JWT Token的获取和刷新
 * 
 * 使用说明：
 * 1. 在Postman的Collection或Folder级别设置此脚本
 * 2. 配置环境变量：
 *    - base_url: API基础URL (例如: http://localhost:5000)
 *    - username: 用户名 (例如: admin)
 *    - password: 密码 (例如: admin123)
 * 3. 脚本会自动管理以下环境变量：
 *    - jwt_token: JWT Token
 *    - token_expiry: Token过期时间戳
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

    // Token过期前提前刷新的时间（秒）
    // 如果Token在60秒内过期，则重新获取
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
 * 登录并获取JWT Token
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
            
    // 解析Token获取过期时间
          const payload = parseJwtPayload(token);
  let expiryTimestamp;
            
       if (payload && payload.exp) {
        // 从Token的exp字段获取过期时间
       expiryTimestamp = payload.exp;
  console.log(`[JWT Auth] Token将在 ${new Date(expiryTimestamp * 1000).toLocaleString()} 过期`);
   } else if (responseBody.expiresIn) {
   // 从响应的expiresIn字段计算过期时间
    const now = Math.floor(Date.now() / 1000);
       expiryTimestamp = now + responseBody.expiresIn;
    console.log(`[JWT Auth] Token有效期: ${responseBody.expiresIn} 秒`);
       } else {
      // 默认60分钟过期
                const now = Math.floor(Date.now() / 1000);
   expiryTimestamp = now + 3600;
      console.warn("[JWT Auth] 无法获取Token过期时间，使用默认值60分钟");
            }
            
            // 保存Token和过期时间到环境变量
            pm.environment.set("jwt_token", token);
        pm.environment.set("token_expiry", expiryTimestamp.toString());
            
    // 保存用户信息（可选）
   if (responseBody.username) {
   pm.environment.set("current_user", responseBody.username);
 }
  if (responseBody.role) {
      pm.environment.set("current_role", responseBody.role);
            }
            
            console.log(`[JWT Auth] ? 登录成功！用户: ${responseBody.username || CONFIG.username}, 角色: ${responseBody.role || 'Unknown'}`);
     
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
    // 检查是否需要获取或刷新Token
    if (!hasToken()) {
      console.log("[JWT Auth] 未找到Token，正在获取...");
     login();
    } else if (isTokenExpired()) {
      console.log("[JWT Auth] Token已过期或即将过期，正在刷新...");
      login();
    } else {
  console.log("[JWT Auth] ? Token有效，跳过登录");
        
        // 显示当前Token信息
        const token = pm.environment.get("jwt_token");
        const payload = parseJwtPayload(token);
        if (payload) {
    const expiry = pm.environment.get("token_expiry");
            const now = Math.floor(Date.now() / 1000);
 const remaining = parseInt(expiry) - now;
            console.log(`[JWT Auth] Token剩余有效时间: ${Math.floor(remaining / 60)}分${remaining % 60}秒`);
        }
    }
} catch (error) {
console.error("[JWT Auth] 发生错误:", error.message);
    // 可以选择清除无效的Token
    pm.environment.unset("jwt_token");
    pm.environment.unset("token_expiry");
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
