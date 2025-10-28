# MQTT Server Migration Script
# Migrates from Host1 (port 1883) to Host2 (port 1884)
# This script uses a "hot-swap" strategy:
# 1. State is copied from RUNNING Host1.
# 2. Host2 is started with that state.
# 3. Host1 is stopped AFTER Host2 is running.

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  MQTT Server Migration" -ForegroundColor Cyan
Write-Host "  Hot-Swap Strategy" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$HOST1_CONTAINER = "mqtt-server-host1"
$HOST2_CONTAINER = "mqtt-server-host2"
$STATE_FILE = "state.json"
$TEMP_DIR = "$PSScriptRoot\temp-migration"

# Create temp directory
New-Item -ItemType Directory -Force -Path $TEMP_DIR | Out-Null

Write-Host "[Step 1/7] Starting Host1 (Source)..." -ForegroundColor Yellow
docker-compose -f docker-compose.host1.yml up -d --build

Write-Host "[Step 2/7] Waiting for server to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

Write-Host "`n" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  HOST1 IS READY!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Connect your client to: localhost:1883" -ForegroundColor White
Write-Host "  Let it run and send some messages..." -ForegroundColor White
Write-Host "`n  Press ENTER when ready to migrate" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Green

Read-Host

Write-Host "[Step 3/7] Extracting state from RUNNING Host1..." -ForegroundColor Yellow
$statePath = "$TEMP_DIR\$STATE_FILE"

# Try to copy state file from the running container
docker cp "${HOST1_CONTAINER}:/app/data/${STATE_FILE}" $statePath 2>$null

if (Test-Path $statePath) {
    Write-Host "  [OK] State file extracted successfully" -ForegroundColor Green
    Write-Host "  State file size: $((Get-Item $statePath).Length) bytes" -ForegroundColor Gray
    
    # Show state content
    Write-Host "`n  State content preview:" -ForegroundColor Cyan
    Get-Content $statePath | ConvertFrom-Json | ConvertTo-Json -Depth 3 | Write-Host
} else {
    Write-Host "  [WARNING] No state file found - this might be the first run" -ForegroundColor Yellow
}

Write-Host "`n[Step 4/7] Building Host2 (Target)..." -ForegroundColor Yellow
docker-compose -f docker-compose.host2.yml up --no-start

Write-Host "[Step 5/7] Transferring state to Host2..." -ForegroundColor Yellow
if (Test-Path $statePath) {
    docker cp $statePath "${HOST2_CONTAINER}:/app/${STATE_FILE}"
    Write-Host "  [OK] State transferred to Host2" -ForegroundColor Green
}else{
    Write-Host "[WARNING] No state file to transfer" -ForegroundColor Yellow
}

Write-Host "[Step 6/7] Starting Host2 (will load the transferred state)..." -ForegroundColor Yellow
Write-Host "  (Host1 is still running at this point)" -ForegroundColor Gray
docker start $HOST2_CONTAINER
Start-Sleep -Seconds 3 # Give Host2 time to initialize

Write-Host "[Step 7/7] Stopping Host1 (migration complete)..." -ForegroundColor Yellow
docker stop $HOST1_CONTAINER 2>$null

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  MIGRATION COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Host1: STOPPED" -ForegroundColor Red
Write-Host "  Host2: RUNNING on localhost:1884" -ForegroundColor Green
Write-Host "`n  Reconnect your client to: localhost:1884" -ForegroundColor Yellow
Write-Host "  State should be preserved!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Green

# Show running containers
Write-Host "Container Status:" -ForegroundColor Cyan
docker ps -a --filter "name=mqtt-server" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

Write-Host "`n[INFO] To view Host2 logs:" -ForegroundColor Cyan
Write-Host "  docker logs -f $HOST2_CONTAINER" -ForegroundColor White

Write-Host "`n[INFO] To cleanup everything:" -ForegroundColor Cyan
Write-Host "  docker-compose -f docker-compose.host1.yml down -v" -ForegroundColor White
Write-Host "  docker-compose -f docker-compose.host2.yml down -v" -ForegroundColor White
Write-Host "  Remove-Item -Recurse -Force $TEMP_DIR" -ForegroundColor White
