#!/usr/bin/env bash
# =============================================================================
# install-secrets.sh
# -----------------------------------------------------------------------------
# 在 Ubuntu 服务器上以最小权限的方式安装 SwitchYard.Service 的敏感配置。
#
# 功能：
#   1. 确保运行账号 switchyard 存在。
#   2. 创建 /etc/switchyard 目录（root 拥有，0750）。
#   3. 交互收集 DB 用户名/密码、JWT 密钥（或自动生成 JWT 密钥）。
#   4. 生成 /etc/switchyard/api.env（owner switchyard:switchyard，权限 0600）。
#   5. 安装 systemd unit 文件并 daemon-reload。
#
# 用法：
#   sudo ./install-secrets.sh                # 交互输入
#   sudo JWT_AUTOGEN=1 ./install-secrets.sh  # 自动生成 JWT 密钥
#
# 卸载（保留备份）：
#   sudo ./install-secrets.sh --uninstall
# =============================================================================

set -euo pipefail

SERVICE_USER="switchyard"
SERVICE_GROUP="switchyard"
CONF_DIR="/etc/switchyard"
ENV_FILE="${CONF_DIR}/api.env"
SYSTEMD_UNIT_SRC="$(dirname "$0")/switchyard-api.service"
SYSTEMD_UNIT_DST="/etc/systemd/system/switchyard-api.service"

require_root() {
    if [[ "$(id -u)" -ne 0 ]]; then
        echo "ERROR: 必须以 root 运行（请使用 sudo）。" >&2
        exit 1
    fi
}

ensure_user() {
    if ! id -u "${SERVICE_USER}" >/dev/null 2>&1; then
        echo "[+] 创建系统账号 ${SERVICE_USER}"
        adduser --system --group --no-create-home --shell /usr/sbin/nologin "${SERVICE_USER}"
    else
        echo "[=] 系统账号 ${SERVICE_USER} 已存在"
    fi
}

ensure_conf_dir() {
    if [[ ! -d "${CONF_DIR}" ]]; then
        echo "[+] 创建配置目录 ${CONF_DIR}"
        install -d -o root -g "${SERVICE_GROUP}" -m 0750 "${CONF_DIR}"
    else
        chown root:"${SERVICE_GROUP}" "${CONF_DIR}"
        chmod 0750 "${CONF_DIR}"
    fi
}

prompt_secret() {
    local var_name="$1"
    local label="$2"
    local default_val="${3:-}"
    local val=""
    while [[ -z "${val}" ]]; do
        if [[ -n "${default_val}" ]]; then
            read -r -s -p "${label} [回车使用默认: ${default_val}]: " val || true
            echo
            val="${val:-${default_val}}"
        else
            read -r -s -p "${label}: " val || true
            echo
        fi
        if [[ -z "${val}" ]]; then
            echo "  值不能为空，请重新输入。"
        fi
    done
    printf -v "${var_name}" '%s' "${val}"
}

prompt_plain() {
    local var_name="$1"
    local label="$2"
    local default_val="${3:-}"
    local val=""
    if [[ -n "${default_val}" ]]; then
        read -r -p "${label} [${default_val}]: " val || true
        val="${val:-${default_val}}"
    else
        while [[ -z "${val}" ]]; do
            read -r -p "${label}: " val || true
        done
    fi
    printf -v "${var_name}" '%s' "${val}"
}

write_env_file() {
    local db_host="$1" db_port="$2" db_name="$3" db_user="$4" db_pwd="$5" jwt_secret="$6"

    # 备份旧文件
    if [[ -f "${ENV_FILE}" ]]; then
        local ts
        ts="$(date +%Y%m%d-%H%M%S)"
        echo "[+] 备份现有 ${ENV_FILE} 至 ${ENV_FILE}.bak.${ts}"
        cp -p "${ENV_FILE}" "${ENV_FILE}.bak.${ts}"
        chmod 0600 "${ENV_FILE}.bak.${ts}"
    fi

    # 使用 install 原子化创建并设定权限/属主
    local tmp
    tmp="$(mktemp)"
    chmod 0600 "${tmp}"
    cat > "${tmp}" <<EOF
# 由 install-secrets.sh 自动生成于 $(date -Iseconds)
# 切勿将本文件加入版本控制。
ASPNETCORE_ENVIRONMENT=Production

HumpDatabase__DatabaseType=Mysql
HumpDatabase__MysqlConfig__Host=${db_host}
HumpDatabase__MysqlConfig__Port=${db_port}
HumpDatabase__MysqlConfig__Database=${db_name}
HumpDatabase__MysqlConfig__Username=${db_user}
HumpDatabase__MysqlConfig__Password=${db_pwd}
HumpDatabase__MysqlConfig__SslMode=Preferred
HumpDatabase__MysqlConfig__CharSet=utf8mb4
HumpDatabase__MysqlConfig__ConnectionTimeout=15

Jwt__SecretKey=${jwt_secret}
EOF

    install -o "${SERVICE_USER}" -g "${SERVICE_GROUP}" -m 0600 "${tmp}" "${ENV_FILE}"
    rm -f "${tmp}"
    echo "[+] 已写入 ${ENV_FILE} (owner ${SERVICE_USER}:${SERVICE_GROUP}, mode 0600)"
}

install_unit() {
    if [[ -f "${SYSTEMD_UNIT_SRC}" ]]; then
        echo "[+] 安装 systemd unit -> ${SYSTEMD_UNIT_DST}"
        install -o root -g root -m 0644 "${SYSTEMD_UNIT_SRC}" "${SYSTEMD_UNIT_DST}"
        systemctl daemon-reload
        echo "    启用并启动："
        echo "      sudo systemctl enable --now switchyard-api"
        echo "      sudo systemctl status switchyard-api"
    else
        echo "[!] 未找到 ${SYSTEMD_UNIT_SRC}，跳过 unit 安装。"
    fi
}

uninstall() {
    require_root
    if [[ -f "${ENV_FILE}" ]]; then
        local ts
        ts="$(date +%Y%m%d-%H%M%S)"
        echo "[+] 备份 ${ENV_FILE} 至 ${ENV_FILE}.removed.${ts}"
        mv "${ENV_FILE}" "${ENV_FILE}.removed.${ts}"
        chmod 0600 "${ENV_FILE}.removed.${ts}"
    fi
    if [[ -f "${SYSTEMD_UNIT_DST}" ]]; then
        echo "[+] 停止并移除 systemd unit"
        systemctl disable --now switchyard-api 2>/dev/null || true
        rm -f "${SYSTEMD_UNIT_DST}"
        systemctl daemon-reload
    fi
    echo "[OK] 卸载完成（敏感文件已重命名为 .removed.<时间戳> 备份保留）。"
}

main() {
    if [[ "${1:-}" == "--uninstall" ]]; then
        uninstall
        exit 0
    fi

    require_root
    ensure_user
    ensure_conf_dir

    echo
    echo "===== SwitchYard.Service 敏感配置安装 ====="
    echo "提示：密码输入时不会显示。"
    echo

    local db_host db_port db_name db_user db_pwd jwt_secret

    prompt_plain  db_host    "MySQL Host"      "127.0.0.1"
    prompt_plain  db_port    "MySQL Port"      "3306"
    prompt_plain  db_name    "MySQL Database"  "hump"
    prompt_plain  db_user    "MySQL Username"  ""
    prompt_secret db_pwd     "MySQL Password"  ""

    if [[ "${JWT_AUTOGEN:-0}" == "1" ]]; then
        if ! command -v openssl >/dev/null 2>&1; then
            echo "ERROR: 需要 openssl 自动生成 JWT 密钥。" >&2
            exit 1
        fi
        jwt_secret="$(openssl rand -base64 64 | tr -d '\n')"
        echo "[+] 已自动生成 JWT 密钥（不会显示）。"
    else
        echo
        echo "JWT 签名密钥（至少 32 字符，建议 64+，可用 openssl rand -base64 64 生成）："
        prompt_secret jwt_secret "Jwt__SecretKey" ""
    fi

    write_env_file "${db_host}" "${db_port}" "${db_name}" "${db_user}" "${db_pwd}" "${jwt_secret}"
    install_unit

    echo
    echo "===== 完成 ====="
    echo "下一步："
    echo "  1) 部署应用程序到 /opt/switchyard/api/"
    echo "  2) sudo systemctl enable --now switchyard-api"
    echo "  3) sudo journalctl -u switchyard-api -f"
}

main "$@"
