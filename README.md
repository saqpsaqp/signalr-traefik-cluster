# SignalR Cluster POC with Traefik Load Balancer

A proof-of-concept implementation of a scalable SignalR real-time chat system using:
- **.NET 9.0 SignalR Hubs** for real-time communication
- **Redis backplane** for message synchronization across nodes
- **Traefik** as load balancer with automatic service discovery
- **Docker Compose** for container orchestration with dynamic scaling

## 🏗️ Architecture

```
┌──────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Web Client     │────│   Traefik LB     │────│  SignalR Node 1 │
│  (WebSocket)     │    │  (Port 8888)     │    │    (Dynamic)    │
└──────────────────┘    │                  │    ├─────────────────┤
                        │  Auto Discovery  │────│  SignalR Node 2 │
┌──────────────────┐    │  Sticky Sessions │    │    (Dynamic)    │
│ Traefik Dashboard│────│  Health Checks   │    ├─────────────────┤
│  (Port 8080)     │    │                  │────│  SignalR Node N │
└──────────────────┘    └──────────────────┘    │    (Scalable)   │
                                                └─────────────────┘
┌──────────────────┐    ┌──────────────────┐
│ Redis Commander  │────│   Redis Server   │
│  (/redis path)   │    │   (Port 6379)    │
└──────────────────┘    └──────────────────┘
```

## ✨ Features

- **🔄 Dynamic Scaling**: Add/remove SignalR nodes on demand
- **🎯 Auto Service Discovery**: Traefik automatically detects new containers
- **🔗 Sticky Sessions**: WebSocket connections stay on same node
- **❤️ Health Monitoring**: Automatic health checks and failover
- **📊 Real-time Dashboards**: Traefik dashboard + Redis Commander
- **🌐 CORS Support**: Configured for cross-origin requests
- **⚡ Redis Backplane**: Message synchronization across all nodes
- **📱 Multi-client Support**: Web client with dynamic node selection

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- Node.js (for test client)

### Start the Cluster
```bash
# Clone the repository
git clone https://github.com/saqpsaqp/signalr-traefik-cluster.git
cd signalr-traefik-cluster

# Start with 3 SignalR nodes
docker-compose up --build --scale signalr=3

# Or start with custom number of nodes
docker-compose up --build --scale signalr=5
```

### Access the Services
- **SignalR Chat**: http://localhost:8888/chathub
- **Web Client**: Open `test-client/web-client-dynamic.html` in browser
- **Traefik Dashboard**: http://localhost:8080
- **Redis Commander**: http://localhost:8888/redis

### Scale Dynamically
```bash
# Scale up to 7 nodes
docker-compose up --scale signalr=7 -d

# Scale down to 2 nodes
docker-compose up --scale signalr=2 -d

# The web client will automatically detect new nodes
```

## 🧪 Testing

### Web Client Testing
1. Open `test-client/web-client-dynamic.html` in multiple browser tabs
2. Select different nodes from the dropdown (auto-refreshes every 10s)
3. Send messages and see them synchronized across all clients
4. Scale the cluster and watch nodes appear/disappear in real-time

### API Testing
```bash
# Check cluster health
curl http://localhost:8888/health

# Get active nodes
curl http://localhost:8888/nodes

# Test load balancing
for i in {1..10}; do curl http://localhost:8888/health; done
```

### Node.js Test Client
```bash
cd test-client
npm install
node test.js
```

## 🛠️ Development

### Project Structure
```
signalr-poc-cluster/
├── docker-compose.yml           # Main orchestration file
├── Dockerfile                   # SignalR app container
├── Program.cs                   # .NET application entry point
├── Hubs/
│   └── ChatHub.cs               # SignalR hub implementation
├── Services/
│   └── NodeRegistrationService.cs  # Redis node registration
├── test-client/
│   ├── web-client-dynamic.html  # Dynamic web client
│   ├── web-client.html          # Static web client
│   ├── test.js                  # Node.js test client
│   └── index.js                 # Alternative test client
└── README.md                    # This file
```

### Key Components

#### SignalR Hub (`Hubs/ChatHub.cs`)
- **SendMessage**: Broadcast to all clients
- **JoinGroup/LeaveGroup**: Room-based messaging
- **SendToGroup**: Send to specific room
- **GetNodeInfo**: Returns current node information

#### Node Registration (`Services/NodeRegistrationService.cs`)
- Registers nodes in Redis with heartbeat
- Auto-cleanup of dead nodes
- Provides node discovery for load balancer

#### Traefik Configuration
- **Automatic Discovery**: Detects containers via Docker API
- **Sticky Sessions**: Maintains WebSocket connections
- **Health Checks**: Monitors `/health` endpoint
- **Load Balancing**: Round-robin with session affinity

## 📋 Configuration

### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Set to `Production`
- `ConnectionStrings__Redis`: Redis connection string
- `ASPNETCORE_URLS`: HTTP binding URL

### Traefik Labels
The SignalR service uses these Docker labels for Traefik configuration:
- `traefik.enable=true`: Enable Traefik routing
- `traefik.http.routers.signalr.rule=Host(\`localhost\`)`: Route rule
- `traefik.http.services.signalr.loadbalancer.sticky.cookie=true`: Sticky sessions

### Redis Configuration
- **Persistence**: Enabled with appendonly mode
- **Service Discovery**: Nodes register themselves automatically
- **Health Monitoring**: 30-second heartbeat with auto-cleanup

## 🔧 Monitoring & Debugging

### Traefik Dashboard
Visit http://localhost:8080 to see:
- Active services and their health status
- Request routing and load balancing
- Real-time traffic metrics

### Redis Commander
Visit http://localhost:8888/redis to:
- Monitor registered nodes
- View Redis keys and data
- Debug service discovery issues

### Docker Logs
```bash
# View all service logs
docker-compose logs -f

# View specific service logs
docker-compose logs -f signalr
docker-compose logs -f traefik
docker-compose logs -f redis
```

## 🚀 Production Considerations

### Security
- Remove `traefik.api.insecure=true` for production
- Use HTTPS with SSL certificates
- Implement authentication and authorization
- Secure Redis with password and TLS

### Performance
- Use Redis Cluster for high availability
- Configure connection pooling
- Set appropriate resource limits
- Use external load balancer for multi-host setups

### Monitoring
- Add metrics collection (Prometheus)
- Set up alerting for node failures
- Monitor WebSocket connection counts
- Track message throughput and latency

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test with different scale scenarios
5. Submit a pull request

## 📄 License

This project is open source and available under the MIT License.

## 🔗 Related Technologies

- [ASP.NET Core SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [Traefik](https://traefik.io/)
- [Redis](https://redis.io/)
- [Docker Compose](https://docs.docker.com/compose/)

---

**Built with ❤️ for real-time web applications**