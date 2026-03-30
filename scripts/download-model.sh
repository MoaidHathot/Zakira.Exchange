#!/usr/bin/env bash
# Downloads the all-MiniLM-L6-v2 ONNX model and vocabulary file for Zakira.Exchange.
#
# Usage: ./download-model.sh [output-dir]
#   output-dir: Directory to download into (default: src/Zakira.Exchange.Core/Models/)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-"$SCRIPT_DIR/../src/Zakira.Exchange.Core/Models"}"

BASE_URL="https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main"

declare -A FILES=(
    ["all-MiniLM-L6-v2.onnx"]="$BASE_URL/onnx/model.onnx"
    ["vocab.txt"]="$BASE_URL/vocab.txt"
)

# Resolve to absolute path
OUTPUT_DIR="$(cd "$SCRIPT_DIR" && mkdir -p "$OUTPUT_DIR" && cd "$OUTPUT_DIR" && pwd)"

echo "Output directory: $OUTPUT_DIR"

for filename in "${!FILES[@]}"; do
    dest="$OUTPUT_DIR/$filename"
    url="${FILES[$filename]}"

    if [ -f "$dest" ]; then
        echo "Already exists, skipping: $filename"
        continue
    fi

    echo "Downloading $filename ..."
    if command -v curl &> /dev/null; then
        curl -L --fail --progress-bar -o "$dest" "$url"
    elif command -v wget &> /dev/null; then
        wget --show-progress -q -O "$dest" "$url"
    else
        echo "Error: Neither curl nor wget found. Please install one of them."
        exit 1
    fi

    size=$(stat -f%z "$dest" 2>/dev/null || stat -c%s "$dest" 2>/dev/null || echo "unknown")
    echo "  Downloaded: $filename ($size bytes)"
done

echo ""
echo "Model files are ready in: $OUTPUT_DIR"
echo "You can use --model-path to point to the .onnx file, or place these files"
echo "alongside the Zakira executable in a 'Models' subdirectory."
