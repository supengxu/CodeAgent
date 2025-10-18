#!/bin/bash
#
# CodeAgent Harness 全量校验脚本（跨平台 Shell 版本）
# 支持 Windows (Git Bash) 和 macOS/Linux
#

set -e

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 检测平台
IS_WINDOWS=false
IS_MACOS=false
IS_LINUX=false

case "$(uname -s)" in
    CYGWIN*|MINGW*|MSYS*)
        IS_WINDOWS=true
        ;;
    Darwin*)
        IS_MACOS=true
        ;;
    Linux*)
        IS_LINUX=true
        ;;
esac

# 获取脚本目录和项目根目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

# 参数解析
SKIP_TESTS=false
VERBOSE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-tests|-s)
            SKIP_TESTS=true
            shift
            ;;
        --verbose|-v)
            VERBOSE=true
            shift
            ;;
        --help|-h)
            echo "Usage: ./Validate.sh [options]"
            echo ""
            echo "Options:"
            echo "  -s, --skip-tests    Skip unit tests"
            echo "  -v, --verbose       Show detailed output"
            echo "  -h, --help          Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# 打印函数
print_header() {
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}   CodeAgent Harness 全量校验${NC}"
    if [ "$IS_WINDOWS" = true ]; then
        echo -e "${CYAN}   平台: Windows (Git Bash)${NC}"
    elif [ "$IS_MACOS" = true ]; then
        echo -e "${CYAN}   平台: macOS${NC}"
    else
        echo -e "${CYAN}   平台: Linux${NC}"
    fi
    echo -e "${CYAN}========================================${NC}"
    echo ""
}

print_step() {
    echo -e "${YELLOW}$1${NC}"
}

print_pass() {
    echo -e "${GREEN}$1${NC}"
}

print_fail() {
    echo -e "${RED}$1${NC}"
}

print_warn() {
    echo -e "${YELLOW}$1${NC}"
}

# 切换到项目根目录
cd "$ROOT_DIR"

# 打印标题
print_header

TOTAL_ERRORS=0

# ========================================
# 1. 代码格式检查
# ========================================
print_step "[1/4] 代码格式检查..."

if dotnet format --verify-no-changes --verbosity minimal > /tmp/format.log 2>&1; then
    print_pass "  [PASS] 代码格式检查通过"
else
    FORMAT_EXIT=$?
    print_fail "  [FAIL] 代码格式不规范"
    print_warn "  修复命令: dotnet format"
    if [ "$VERBOSE" = true ]; then
        cat /tmp/format.log
    fi
    TOTAL_ERRORS=$((TOTAL_ERRORS + 1))
fi

# ========================================
# 2. 编译检查（含 Nullable、代码分析）
# ========================================
echo ""
print_step "[2/4] 编译检查（含硬约束规则）..."

if dotnet build --no-incremental --configuration Release --verbosity minimal > /tmp/build.log 2>&1; then
    print_pass "  [PASS] 编译检查通过"
else
    BUILD_EXIT=$?
    print_fail "  [FAIL] 编译失败"
    echo ""
    # 提取错误信息
    grep -E "error (CS|CA)" /tmp/build.log | head -20 | while read -r line; do
        print_fail "  $line"
    done
    echo ""
    print_warn "  提示: 检查 Nullable 警告、代码分析规则违反"
    TOTAL_ERRORS=$((TOTAL_ERRORS + 1))
fi

# ========================================
# 3. 单元测试与覆盖率检查
# ========================================
if [ "$SKIP_TESTS" = false ]; then
    echo ""
    print_step "[3/4] 单元测试与覆盖率检查..."

    SETTINGS_PATH="$SCRIPT_DIR/coverlet.runsettings"

    if dotnet test --no-build --configuration Release \
        --collect:"XPlat Code Coverage" \
        --settings "$SETTINGS_PATH" \
        --verbosity minimal > /tmp/test.log 2>&1; then
        print_pass "  [PASS] 单元测试与覆盖率检查通过"
    else
        TEST_EXIT=$?
        print_fail "  [FAIL] 单元测试失败或覆盖率不达标"
        if [ "$VERBOSE" = true ]; then
            cat /tmp/test.log
        fi
        TOTAL_ERRORS=$((TOTAL_ERRORS + 1))
    fi
else
    echo ""
    print_step "[3/4] 单元测试已跳过 (--skip-tests)"
fi

# ========================================
# 4. AGENTS.md 规则检查
# ========================================
echo ""
print_step "[4/4] AGENTS.md 规则检查..."

AGENTS_FILE="$ROOT_DIR/AGENTS.md"
if [ -f "$AGENTS_FILE" ]; then
    MISSING_SECTIONS=""

    if ! grep -q "架构硬约束" "$AGENTS_FILE"; then
        MISSING_SECTIONS="架构硬约束"
    fi

    if ! grep -q "代码规范硬规则" "$AGENTS_FILE"; then
        MISSING_SECTIONS="$MISSING_SECTIONS 代码规范硬规则"
    fi

    if ! grep -q "完成标准" "$AGENTS_FILE"; then
        MISSING_SECTIONS="$MISSING_SECTIONS 完成标准"
    fi

    if ! grep -q "执行要求" "$AGENTS_FILE"; then
        MISSING_SECTIONS="$MISSING_SECTIONS 执行要求"
    fi

    if [ -n "$MISSING_SECTIONS" ]; then
        print_warn "  [WARN] AGENTS.md 缺少章节:$MISSING_SECTIONS"
    else
        print_pass "  [PASS] AGENTS.md 规则完整"
    fi
else
    print_fail "  [FAIL] AGENTS.md 文件不存在"
    TOTAL_ERRORS=$((TOTAL_ERRORS + 1))
fi

# ========================================
# 结果汇总
# ========================================
echo ""
echo -e "${CYAN}========================================${NC}"

if [ $TOTAL_ERRORS -eq 0 ]; then
    print_pass "   [PASS] 全量 Harness 校验通过"
    echo -e "${CYAN}========================================${NC}"
    exit 0
else
    print_fail "   [FAIL] 发现 $TOTAL_ERRORS 个错误"
    echo -e "${CYAN}========================================${NC}"
    echo ""
    print_warn "修复建议:"
    print_warn "  1. 运行 'dotnet format' 修复格式问题"
    print_warn "  2. 检查 Nullable 警告，添加空检查"
    print_warn "  3. 补充单元测试，确保覆盖率 >= 80%"
    print_warn "  4. 查看 AGENTS.md 确保规则完整"
    exit 1
fi
