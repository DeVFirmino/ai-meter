#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)

cd "$REPOSITORY_ROOT"

dotnet restore llm-observability-azure.sln --nologo
dotnet build llm-observability-azure.sln --no-restore --nologo --verbosity minimal
dotnet test llm-observability-azure.sln --no-build --nologo --verbosity minimal
dotnet format llm-observability-azure.sln --verify-no-changes --no-restore --verbosity minimal
dotnet package list --project llm-observability-azure.sln --vulnerable --include-transitive --no-restore
