#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project "$repo_dir/src/backend/AgentRPA.Api/AgentRPA.Api.csproj" -- --seed-only
