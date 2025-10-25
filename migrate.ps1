# MQTT Server Migration Script
# Migrates from Host1 (port 1883) to Host2 (port 1884)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  MQTT Server Migration" -ForegroundColor Cyan
Write-Host "  Post-Copy Strategy" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$HOST1_CONTAINER = "mqtt-server-host1"
$HOST2_CONTAINER = "mqtt-server-host2"
$STATE_FILE = "state.json"
$TEMP_DIR = "$PSScriptRoot\temp-migration"

# Create temp directory
New-Item -ItemType Directory -Force -Path $TEMP_DIR | Out-Null

Write-Host "[Step 1/8] Starting Host1 (Source)..." -ForegroundColor Yellow
docker-compose -f docker-compose.host1.yml up -d --build

Write-Host "[Step 2/8] Waiting for server to initialize..." -ForegroundColor Yellow
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

Write-Host "[Step 3/8] FREEZING Host1 (pausing container)..." -ForegroundColor Yellow
docker pause $HOST1_CONTAINER

Write-Host "[Step 4/8] Extracting state from Host1..." -ForegroundColor Yellow
$statePath = "$TEMP_DIR\$STATE_FILE"

# Try to copy state file
docker cp "${HOST1_CONTAINER}:/app/${STATE_FILE}" $statePath 2>$null

if (Test-Path $statePath) {
    Write-Host "  [OK] State file extracted successfully" -ForegroundColor Green
    Write-Host "  State file size: $((Get-Item $statePath).Length) bytes" -ForegroundColor Gray
    
    # Show state content
    Write-Host "`n  State content preview:" -ForegroundColor Cyan
    Get-Content $statePath | ConvertFrom-Json | ConvertTo-Json -Depth 3 | Write-Host
} else {
    Write-Host "  [WARNING] No state file found - this might be the first run" -ForegroundColor Yellow
}

Write-Host "`n[Step 5/8] Building Host2 (Target)..." -ForegroundColor Yellow
docker-compose -f docker-compose.host2.yml up -d --build

Write-Host "[Step 6/8] Waiting for Host2 to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

Write-Host "[Step 7/8] Transferring state to Host2..." -ForegroundColor Yellow
if (Test-Path $statePath) {
    docker cp $statePath "${HOST2_CONTAINER}:/app/${STATE_FILE}"
    Write-Host "  [OK] State transferred to Host2" -ForegroundColor Green
    
    # Restart to ensure state is loaded
    docker restart $HOST2_CONTAINER | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "[Step 8/8] Stopping Host1..." -ForegroundColor Yellow
docker stop $HOST1_CONTAINER | Out-Null

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
Write-Host "   docker logs -f $HOST2_CONTAINER" -ForegroundColor White

Write-Host "`n[INFO] To cleanup everything:" -ForegroundColor Cyan
Write-Host "   docker-compose -f docker-compose.host1.yml down -v" -ForegroundColor White
Write-Host "   docker-compose -f docker-compose.host2.yml down -v" -ForegroundColor White
Write-Host "   Remove-Item -Recurse -Force $TEMP_DIR" -ForegroundColor White