#!/bin/bash
# Usage:
#   ./start.sh                       # sequential benchmark
#   ./start.sh -par 4                # parallel benchmark, 4 threads (2|4|8|12|16)
#   ./start.sh image                 # sequential image viewer
#   ./start.sh image -par 4          # parallel image viewer
#   ./start.sh all                   # benchmark + image (sequential)
#   ./start.sh all -par 4            # benchmark + image (parallel)
#   ./start.sh -cmd                  # sequential benchmark, console output only (no image)
#   ./start.sh -par 4 -cmd           # parallel benchmark, console output only (no image)

MODE="benchmark"
PAR=false
THREADS=2
CMD=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        benchmark|image|all)
            MODE="$1"
            ;;
        -par)
            PAR=true
            if [[ -n "${2:-}" && "$2" =~ ^[0-9]+$ ]]; then
                THREADS="$2"
                shift
            fi
            ;;
        -cmd)
            CMD="--cmd"
            ;;
        *)
            echo "Usage: $0 [benchmark|image|all] [-par [2|4|8|12|16]] [-cmd]" >&2
            exit 1
            ;;
    esac
    shift
done

echo "Building Release..."
dotnet build MandelbrotSet.csproj -c Release --nologo -v q

EXE="./bin/Release/net10.0-windows/MandelbrotSet.exe"

case "$MODE" in
    benchmark)
        if [ "$PAR" = "true" ]; then
            echo "Running PARALLEL benchmark ($THREADS threads)..."
            "$EXE" --benchmark-par "$THREADS" $CMD
        else
            echo "Running SEQUENTIAL benchmark..."
            "$EXE" --benchmark $CMD
        fi
        ;;
    image)
        if [ "$PAR" = "true" ]; then
            echo "Showing PARALLEL image ($THREADS threads)..."
            "$EXE" --image-par "$THREADS"
        else
            echo "Showing SEQUENTIAL image..."
            "$EXE" --image
        fi
        ;;
    all)
        if [ "$PAR" = "true" ]; then
            echo "Running PARALLEL benchmark + image ($THREADS threads)..."
            "$EXE" --benchmark-par "$THREADS" $CMD
            "$EXE" --image-par "$THREADS"
        else
            echo "Running SEQUENTIAL benchmark + image..."
            "$EXE" $CMD
        fi
        ;;
esac
