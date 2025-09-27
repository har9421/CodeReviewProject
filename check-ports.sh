#!/bin/bash

# Check what's running on common ports
echo "🔍 Checking what's running on common ports..."
echo "=============================================="

echo ""
echo "Port 5000 (commonly used by AirPlay/AirTunes):"
lsof -i:5000 2>/dev/null || echo "  ✅ Port 5000 is free"

echo ""
echo "Port 5001 (commonly used by AirPlay/AirTunes):"
lsof -i:5001 2>/dev/null || echo "  ✅ Port 5001 is free"

echo ""
echo "Port 5002 (our new bot port):"
lsof -i:5002 2>/dev/null || echo "  ✅ Port 5002 is free"

echo ""
echo "Port 5003 (our new HTTPS port):"
lsof -i:5003 2>/dev/null || echo "  ✅ Port 5003 is free"

echo ""
echo "💡 If you see AirTunes or ControlCenter processes on ports 5000/5001,"
echo "   that's why you were getting 403 Forbidden errors!"
echo "   Our bot now uses ports 5002/5003 to avoid conflicts."
