# Deployment Guide

## Overview

Neighborly offers flexible deployment options to suit different application architectures and requirements. This guide covers deployment strategies, configuration options, and best practices for production environments.

## Deployment Options

### 1. Embedded Library (NuGet Package)

The simplest deployment option for applications that need direct database access.

#### Installation
```powershell
PM> NuGet\Install-Package Neighborly
```

#### Use Cases
- Desktop applications
- Mobile applications
- Server applications with direct database access
- Microservices with embedded storage
- Applications requiring maximum performance

#### Advantages
- Lowest latency (no network overhead)
- Simplified deployment (single binary)
- No additional infrastructure required
- Maximum throughput
- Direct access to all features

#### Considerations
- Database file must be accessible to the application
- Suitable for single-application scenarios
- Requires .NET runtime on target machine

#### Example Configuration
```csharp
// Basic embedded usage
var db = new VectorDatabase("MyApplication");
await db.LoadAsync("vectors.db", createOnNew: true);

// Add vectors and search
await db.AddAsync(vector);
var results = await db.SearchAsync(query, k: 10);
```

### 2. Docker Container (gRPC/REST API)

Containerized deployment for client-server architectures and microservices.

#### Docker Hub
```bash
docker pull nick206/neighborly:latest
```

#### Basic Docker Run
```bash
docker run -p 8080:8080 \
  -e PROTO_GRPC=true \
  -e PROTO_REST=true \
  nick206/neighborly:latest
```

#### Docker Compose
```yaml
version: '3.8'
services:
  neighborly:
    image: nick206/neighborly:latest
    ports:
      - "8080:8080"
    environment:
      - PROTO_GRPC=true
      - PROTO_REST=true
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - ./data:/app/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

#### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: neighborly-deployment
spec:
  replicas: 3
  selector:
    matchLabels:
      app: neighborly
  template:
    metadata:
      labels:
        app: neighborly
    spec:
      containers:
      - name: neighborly
        image: nick206/neighborly:latest
        ports:
        - containerPort: 8080
        env:
        - name: PROTO_GRPC
          value: "true"
        - name: PROTO_REST
          value: "true"
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        volumeMounts:
        - name: data-volume
          mountPath: /app/data
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
      volumes:
      - name: data-volume
        persistentVolumeClaim:
          claimName: neighborly-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: neighborly-service
spec:
  selector:
    app: neighborly
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

#### Use Cases
- Multi-client applications
- Microservices architectures
- Web applications
- Cross-platform client support
- Scalable deployments

#### Advantages
- Language-agnostic clients
- Centralized data management
- Horizontal scaling
- Load balancing support
- Infrastructure standardization

## Configuration Options

### Environment Variables

#### Core Settings
```bash
# Protocol support
PROTO_GRPC=true              # Enable gRPC endpoint
PROTO_REST=true              # Enable REST endpoint

# ASP.NET Core settings
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_LOGGING__LOGLEVEL__DEFAULT=Information

# Database settings
DB_PATH=/app/data/vectors.db  # Database file path
DB_AUTO_SAVE=true            # Enable automatic saving
DB_SAVE_INTERVAL=300         # Auto-save interval (seconds)
```

#### Performance Tuning
```bash
# Memory settings
DOTNET_GCHeapHardLimit=2gb   # Limit GC heap size
DOTNET_GCRetainVM=1          # Keep virtual memory

# Threading
DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=6
DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1

# Logging
LOGGING__LOGLEVEL__NEIGHBORLY=Debug
OTEL_TRACES_EXPORTER=otlp    # OpenTelemetry export
```

### Configuration Files

#### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Neighborly": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Neighborly": {
    "DatabasePath": "/app/data/vectors.db",
    "AutoSave": true,
    "SaveIntervalSeconds": 300,
    "BackgroundIndexing": true,
    "IndexRebuildDelaySeconds": 5,
    "MaxVectorDimensions": 4096,
    "DefaultSearchAlgorithm": "Auto"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:8080"
      }
    },
    "Limits": {
      "MaxConcurrentConnections": 1000,
      "MaxRequestBodySize": 52428800
    }
  }
}
```

#### Production Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Neighborly": "Information"
    }
  },
  "Neighborly": {
    "AutoSave": true,
    "SaveIntervalSeconds": 600,
    "BackgroundIndexing": true,
    "CompressOnSave": true,
    "EnableTelemetry": true
  },
  "HealthChecks": {
    "UI": {
      "ApiPath": "/health",
      "UIPath": "/healthchecks-ui"
    }
  }
}
```

## Platform-Specific Deployments

### Windows Server

#### Service Installation
```bash
# Install as Windows Service
sc create NeighborlyService binpath="C:\Apps\Neighborly\Neighborly.exe"
sc config NeighborlyService start=auto
sc start NeighborlyService
```

#### IIS Integration
```xml
<!-- web.config for IIS -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" 
                arguments=".\Neighborly.dll" 
                stdoutLogEnabled="false" 
                stdoutLogFile=".\logs\stdout" />
  </system.webServer>
</configuration>
```

### Linux (systemd)

#### Service File
```ini
# /etc/systemd/system/neighborly.service
[Unit]
Description=Neighborly Vector Database
After=network.target

[Service]
Type=notify
ExecStart=/opt/neighborly/Neighborly
Restart=always
RestartSec=5
User=neighborly
Group=neighborly
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
WorkingDirectory=/opt/neighborly

[Install]
WantedBy=multi-user.target
```

#### Installation Commands
```bash
# Create user and directories
sudo useradd -r -s /bin/false neighborly
sudo mkdir -p /opt/neighborly/data
sudo chown -R neighborly:neighborly /opt/neighborly

# Install service
sudo systemctl daemon-reload
sudo systemctl enable neighborly
sudo systemctl start neighborly
```

### macOS

#### LaunchDaemon
```xml
<!-- /Library/LaunchDaemons/com.neighborly.service.plist -->
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.neighborly.service</string>
    <key>ProgramArguments</key>
    <array>
        <string>/Applications/Neighborly/Neighborly</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>WorkingDirectory</key>
    <string>/Applications/Neighborly</string>
</dict>
</plist>
```

### Mobile Platforms

#### Android Considerations
- Disable background indexing to preserve battery
- Use manual index rebuilds during app lifecycle events
- Consider local storage limitations
- Implement proper app suspension handling

#### iOS Considerations
- Background processing restrictions apply
- Use app lifecycle events for maintenance
- Consider iCloud backup implications
- Handle app sandboxing requirements

## Production Best Practices

### Performance Optimization

#### Memory Management
```csharp
// Configure for production workloads
var config = new VectorDatabaseConfig
{
    MaxMemoryUsageMB = 1024,
    EnableCompression = true,
    BackgroundIndexing = true,
    IndexRebuildDelaySeconds = 30  // Longer delay for production
};
```

#### Connection Pooling
```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 1000,
      "MaxConcurrentUpgradedConnections": 100,
      "MaxRequestBodySize": 52428800,
      "RequestHeadersTimeout": "00:00:30"
    }
  }
}
```

### Security

#### HTTPS Configuration
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://+:443",
        "Certificate": {
          "Path": "/etc/ssl/certs/neighborly.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

#### Authentication and Authorization
```csharp
// API key authentication middleware
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// Rate limiting
app.UseRateLimiter();

// CORS configuration
app.UseCors(policy => policy
    .WithOrigins("https://yourdomain.com")
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .WithHeaders("Authorization", "Content-Type"));
```

### Monitoring and Observability

#### Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<VectorDatabaseHealthCheck>("database")
    .AddCheck<MemoryHealthCheck>("memory")
    .AddCheck<DiskSpaceHealthCheck>("disk");
```

#### Metrics Collection
```csharp
// Custom metrics
services.AddSingleton<IMetrics, NeighborlyMetrics>();

// OpenTelemetry
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("Neighborly")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("Neighborly")
        .AddOtlpExporter());
```

#### Logging Configuration
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": "Information",
    "WriteTo": [
      {"Name": "Console"},
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/neighborly/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

### Backup and Recovery

#### Database Backup
```csharp
// Automated backup strategy
public class BackupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var backupPath = $"/backups/vectors-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
                await _database.SaveAsync(backupPath);
                
                // Clean old backups
                CleanOldBackups("/backups", retentionDays: 7);
                
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
```

#### Disaster Recovery
```bash
#!/bin/bash
# Disaster recovery script

# Stop service
sudo systemctl stop neighborly

# Restore from backup
cp /backups/latest/vectors.db /opt/neighborly/data/

# Verify integrity
dotnet /opt/neighborly/Neighborly.dll --verify-database

# Start service
sudo systemctl start neighborly

# Verify service health
curl -f http://localhost:8080/health || exit 1
```

### Scaling Strategies

#### Horizontal Scaling
- Deploy multiple read-only replicas
- Use load balancer for read distribution
- Implement cache layer for hot data
- Consider data partitioning strategies

#### Vertical Scaling
- Increase memory for larger datasets
- Use faster storage (NVMe SSDs)
- Optimize CPU for search algorithms
- Monitor resource utilization

#### Database Sharding
```csharp
// Simple hash-based sharding
public class ShardedVectorDatabase
{
    private readonly VectorDatabase[] _shards;
    
    public VectorDatabase GetShard(Guid vectorId)
    {
        var hash = vectorId.GetHashCode();
        var index = Math.Abs(hash) % _shards.Length;
        return _shards[index];
    }
}
```

This deployment guide provides comprehensive coverage of production deployment scenarios, configuration options, and best practices for running Neighborly in various environments.