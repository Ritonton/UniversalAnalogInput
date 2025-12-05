#!/bin/bash

# Universal Analog Input - Unix Build Script

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default options
BUILD_TYPE="debug"
SKIP_RUST=false
SKIP_CSHARP=false
CLEAN_BUILD=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --release)
            BUILD_TYPE="release"
            shift
            ;;
        --skip-rust)
            SKIP_RUST=true
            shift
            ;;
        --skip-csharp)
            SKIP_CSHARP=true
            shift
            ;;
        --clean)
            CLEAN_BUILD=true
            shift
            ;;
        --help)
            echo -e "${BLUE}Universal Analog Input - Build Script Help${NC}"
            echo ""
            echo -e "${YELLOW}Usage:${NC}"
            echo "  ./build.sh [options]"
            echo ""
            echo -e "${YELLOW}Options:${NC}"
            echo "  --release     Build in release mode (default: debug)"
            echo "  --skip-rust   Skip Rust library build"
            echo "  --skip-csharp Skip C# application build"
            echo "  --clean       Clean previous builds before building"
            echo "  --help        Show this help message"
            echo ""
            echo -e "${YELLOW}Examples:${NC}"
            echo "  ./build.sh                   (Build everything in debug)"
            echo "  ./build.sh --release         (Build everything in release)"
            echo "  ./build.sh --clean --release (Clean build in release)"
            echo "  ./build.sh --skip-rust       (Build only C# part)"
            echo ""
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "${BLUE}========================================"
echo "Universal Analog Input - Build Script"
echo -e "========================================${NC}"

if [[ "$BUILD_TYPE" == "release" ]]; then
    CONFIG="Release"
else
    CONFIG="Debug"
fi

echo -e "${YELLOW}Build Configuration:${NC}"
echo "  Build Type: $BUILD_TYPE"
echo "  Skip Rust: $SKIP_RUST"
echo "  Skip C#: $SKIP_CSHARP"
echo "  Clean Build: $CLEAN_BUILD"
echo ""

# Check prerequisites
echo -e "${BLUE}[0/6] Checking prerequisites...${NC}"

if ! command -v cargo &> /dev/null; then
    echo -e "${RED}ERROR: Cargo not found. Please install Rust from https://rustup.rs/${NC}"
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: dotnet not found. Please install .NET 9 SDK${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Prerequisites check passed${NC}"

# Get project root
PROJECT_ROOT="$(dirname "$(dirname "$(realpath "$0")")")"

# Clean build if requested
if [ "$CLEAN_BUILD" = true ]; then
    echo ""
    echo -e "${BLUE}[1/6] Cleaning previous builds...${NC}"
    
    cd "$PROJECT_ROOT"
    rm -rf target/
    rm -rf native/target/
    rm -rf ui/UniversalAnalogInputUI/bin/
    rm -rf ui/UniversalAnalogInputUI/obj/
    
    echo -e "${GREEN}✓ Clean completed${NC}"
fi

# Build Rust library
if [ "$SKIP_RUST" = false ]; then
    echo ""
    echo -e "${BLUE}[2/6] Building Rust native library...${NC}"
    cd "$PROJECT_ROOT"
    
    if [ "$BUILD_TYPE" = "release" ]; then
        echo "Building release version..."
        cargo build --release
    else
        echo "Building debug version..."
        cargo build
    fi
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}ERROR: Failed to build Rust library${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✓ Rust library built successfully${NC}"
else
    echo -e "${YELLOW}⚠ Skipping Rust build${NC}"
fi

# Generate C headers
echo ""
echo -e "${BLUE}[3/6] Generating C headers...${NC}"
cd "$PROJECT_ROOT"

cbindgen --config native/cbindgen.toml --crate universal-analog-input --output ui/native_bindings.h 2>/dev/null || {
    echo -e "${YELLOW}⚠ cbindgen failed, headers may be outdated${NC}"
}

echo -e "${GREEN}✓ Headers generated${NC}"

# Copy library to C# project
echo ""
echo -e "${BLUE}[4/6] Copying Rust library to C# project...${NC}"

if [ "$BUILD_TYPE" = "release" ]; then
    LIB_PATH="target/release/libuniversal_analog_input.so"
else
    LIB_PATH="target/debug/libuniversal_analog_input.so"
fi

# On macOS, the extension would be .dylib
if [[ "$OSTYPE" == "darwin"* ]]; then
    LIB_PATH="${LIB_PATH%.so}.dylib"
fi

if [ -f "$LIB_PATH" ]; then
    cp "$LIB_PATH" "ui/UniversalAnalogInputUI/"
    echo -e "${GREEN}✓ Copied $BUILD_TYPE library to C# project${NC}"
else
    echo -e "${RED}ERROR: Library not found at $LIB_PATH${NC}"
    echo -e "${YELLOW}Note: This script is designed for Windows. On Unix systems, the library extension differs.${NC}"
    exit 1
fi

# Build C# application
if [ "$SKIP_CSHARP" = false ]; then
    echo ""
    echo -e "${BLUE}[5/6] Building C# application...${NC}"
    cd "$PROJECT_ROOT/ui"
    
    echo "Restoring NuGet packages..."
    dotnet restore UniversalAnalogInputUI.sln --verbosity quiet
    if [ $? -ne 0 ]; then
        echo -e "${RED}ERROR: Failed to restore NuGet packages${NC}"
        exit 1
    fi
    
    echo "Building C# application ($CONFIG)..."
    dotnet build UniversalAnalogInputUI.sln -c "$CONFIG" --verbosity quiet
    if [ $? -ne 0 ]; then
        echo -e "${RED}ERROR: Failed to build C# application${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✓ C# application built successfully${NC}"
else
    echo -e "${YELLOW}⚠ Skipping C# build${NC}"
fi

# Copy library to output directory
echo ""
echo -e "${BLUE}[6/6] Copying library to output directory...${NC}"

OUTPUT_DIR="ui/UniversalAnalogInputUI/bin/$CONFIG/net9.0-linux/"
# Adjust for macOS
if [[ "$OSTYPE" == "darwin"* ]]; then
    OUTPUT_DIR="${OUTPUT_DIR/linux/osx}"
fi

if [ -f "$LIB_PATH" ]; then
    mkdir -p "$OUTPUT_DIR"
    cp "$LIB_PATH" "$OUTPUT_DIR/"
    echo -e "${GREEN}✓ Library copied to output directory${NC}"
fi

# Success message
echo ""
echo -e "${GREEN}========================================"
echo "✅ Build completed successfully!"
echo -e "========================================${NC}"
echo ""
echo -e "${BLUE}Build Summary:${NC}"
echo "  Configuration: $CONFIG"
echo "  Rust Library: $LIB_PATH"
echo "  C# Output: $OUTPUT_DIR"
echo ""
echo -e "${BLUE}To run the application:${NC}"
echo -e "  ${YELLOW}cd ui${NC}"
echo -e "  ${YELLOW}dotnet run --project UniversalAnalogInputUI${NC}"
echo ""
echo -e "${BLUE}Note:${NC}"
echo -e "${YELLOW}This project is primarily designed for Windows with WinUI 3.${NC}"
echo -e "${YELLOW}Unix builds will compile but may not run correctly.${NC}"
echo ""