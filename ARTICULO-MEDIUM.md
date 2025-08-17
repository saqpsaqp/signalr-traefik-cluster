# Construyendo un Sistema de Chat Escalable con SignalR y Traefik: Mi Experiencia Creando un Cluster Dinámico

*Cómo desarrollé una arquitectura de microservicios en tiempo real que se escala automáticamente usando .NET 9, Docker y Redis*

---

Hola, soy **Saúl A. Quintero P.**, Ingeniero en Informática, Cloud Engineer y ex-profesor universitario. En este artículo quiero compartir contigo mi experiencia desarrollando un sistema de chat en tiempo real altamente escalable usando SignalR, Traefik y Docker. Te contaré los desafíos que enfrenté, las decisiones técnicas que tomé y cómo logré crear una arquitectura que se escala dinámicamente.

## 🎯 El Problema que Quería Resolver

Como ingeniero cloud, siempre me ha fascinado el desafío de crear aplicaciones que puedan crecer bajo demanda. En mis años como profesor universitario, a menudo veía estudiantes luchando con conceptos de escalabilidad horizontal y comunicación en tiempo real. Decidí crear un proyecto que demostrara estos conceptos de manera práctica.

El reto era construir un sistema de chat que pudiera:
- **Escalar horizontalmente** agregando más nodos según la demanda
- **Sincronizar mensajes** entre todas las instancias en tiempo real
- **Balancear la carga** automáticamente sin configuración manual
- **Mantener conexiones WebSocket** estables durante el escalado

## 🏗️ La Arquitectura que Elegí

Después de evaluar diferentes opciones (nginx, HAProxy), me decidí por **Traefik** como load balancer por su capacidad de descubrimiento automático de servicios. La arquitectura final quedó así:

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

### Componentes Clave:

1. **SignalR Hubs** (.NET 9): Manejo de conexiones WebSocket y mensajería
2. **Redis Backplane**: Sincronización de mensajes entre nodos
3. **Traefik**: Load balancer con descubrimiento automático
4. **Docker Compose**: Orquestación y escalado dinámico

## 💻 Implementando el Hub de SignalR

Lo primero que desarrollé fue el hub de SignalR. Quería que cada mensaje incluyera información del nodo para poder verificar que el cluster funcionara correctamente:

```csharp
/// <summary>
/// SignalR Hub for real-time chat functionality with multi-node support
/// </summary>
public class ChatHub : Hub
{
    private readonly string _nodeId = Environment.MachineName;

    /// <summary>
    /// Sends a message to all connected clients across all nodes
    /// </summary>
    public async Task SendMessage(string user, string message)
    {
        // Broadcast to all clients with node identification for debugging
        await Clients.All.SendAsync("ReceiveMessage", user, message, Context.ConnectionId, _nodeId);
    }

    public async Task GetNodeInfo()
    {
        await Clients.Caller.SendAsync("NodeInfo", _nodeId, Context.ConnectionId);
    }
    
    // ... más métodos para grupos y gestión de conexiones
}
```

**Lo interesante aquí** es que cada mensaje incluye el `nodeId`. Esto me permitió verificar visualmente que los mensajes se sincronizaran correctamente entre diferentes nodos del cluster.

## 🔄 El Desafío del Registro Automático de Nodos

Uno de los mayores retos fue implementar un sistema donde los nodos se registraran automáticamente en Redis para que Traefik pudiera descubrirlos. Creé un servicio en background que funciona como un "heartbeat":

```csharp
/// <summary>
/// Background service that automatically registers this node in Redis for service discovery
/// Maintains a heartbeat and provides automatic cleanup when nodes go offline
/// </summary>
public class NodeRegistrationService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        var key = $"signalr:nodes:{_nodeId}";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Register/update this node in Redis with current timestamp
                await db.HashSetAsync(key, new HashEntry[]
                {
                    new("nodeId", _nodeId),
                    new("name", $"Node {_nodeId}"),
                    new("lastSeen", DateTime.UtcNow.ToString("O")),
                    new("status", "healthy")
                });

                // Set TTL for automatic cleanup if node becomes unresponsive
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(45));
                
                // Heartbeat interval - update registration every 15 seconds
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering node {NodeId}", _nodeId);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

**El truco aquí** es el TTL (Time To Live) de 45 segundos. Si un nodo falla, Redis automáticamente elimina su registro, manteniendo limpia la lista de nodos activos.

## ⚖️ Configuración de Traefik: El Corazón del Sistema

La configuración de Traefik fue crucial. Usé labels en Docker Compose para que Traefik descubriera automáticamente los contenedores:

```yaml
signalr:
  build: .
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ConnectionStrings__Redis=redis:6379
    - ASPNETCORE_URLS=http://+:8080
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.signalr.rule=Host(`localhost`)"
    - "traefik.http.services.signalr.loadbalancer.server.port=8080"
    # Enable sticky sessions for WebSocket connections
    - "traefik.http.services.signalr.loadbalancer.sticky.cookie=true"
    - "traefik.http.services.signalr.loadbalancer.sticky.cookie.name=signalr-server"
    # Health check
    - "traefik.http.services.signalr.loadbalancer.healthcheck.path=/health"
    - "traefik.http.services.signalr.loadbalancer.healthcheck.interval=30s"
```

**Las sticky sessions fueron esenciales** para mantener las conexiones WebSocket en el mismo nodo durante toda la sesión del usuario.

## 🚀 API de Descubrimiento de Nodos

Para que el cliente web pudiera mostrar todos los nodos disponibles, implementé un endpoint que consulta Redis:

```csharp
app.MapGet("/nodes", async (IConnectionMultiplexer redis) => 
{
    var db = redis.GetDatabase();
    var server = redis.GetServer(redis.GetEndPoints().First());
    
    // Get active nodes from Redis service registry
    var nodeKeys = server.Keys(pattern: "signalr:nodes:*").ToList();
    var activeNodes = new List<object>();
    
    foreach (var key in nodeKeys)
    {
        var nodeData = await db.HashGetAllAsync(key);
        if (nodeData.Any())
        {
            var nodeInfo = nodeData.ToDictionary(x => x.Name, x => x.Value);
            var lastSeen = DateTime.Parse(nodeInfo["lastSeen"]);
            
            // Only include nodes seen in the last 30 seconds
            if (DateTime.UtcNow - lastSeen < TimeSpan.FromSeconds(30))
            {
                activeNodes.Add(new 
                { 
                    id = nodeInfo["nodeId"].ToString(),
                    name = nodeInfo["name"].ToString(),
                    description = nodeInfo["description"].ToString(),
                    lastSeen = lastSeen
                });
            }
        }
    }
    
    return Results.Ok(new { 
        available_nodes = activeNodes,
        current_node = Environment.MachineName,
        active_count = activeNodes.Count,
        timestamp = DateTime.UtcNow
    });
});
```

## 🎮 Cliente Web Dinámico

Desarrollé un cliente web que se actualiza automáticamente cada 10 segundos, mostrando todos los nodos disponibles:

```javascript
// Auto-refresh nodes every 10 seconds
async function loadNodes() {
    try {
        const response = await fetch('/nodes');
        const data = await response.json();
        
        const select = document.getElementById('nodeSelect');
        select.innerHTML = '';
        
        data.available_nodes.forEach(node => {
            const option = document.createElement('option');
            option.value = node.url;
            option.textContent = `${node.name} (${node.id})`;
            select.appendChild(option);
        });
        
        document.getElementById('nodeCount').textContent = `Nodes: ${data.active_count}`;
    } catch (error) {
        console.error('Error loading nodes:', error);
    }
}

// Auto-refresh every 10 seconds
setInterval(loadNodes, 10000);
```

## 🔧 Escalado Dinámico en Acción

Una de las características más emocionantes del sistema es lo fácil que es escalar:

```bash
# Iniciar con 3 nodos
docker-compose up --build --scale signalr=3

# Escalar a 7 nodos dinámicamente
docker-compose up --scale signalr=7 -d

# Reducir a 2 nodos
docker-compose up --scale signalr=2 -d
```

**La magia sucede aquí**: Traefik detecta automáticamente los nuevos contenedores, Redis mantiene el registro actualizado, y el cliente web muestra los cambios en tiempo real.

## 📊 Monitoreo y Debugging

Implementé varias herramientas de monitoreo:

1. **Traefik Dashboard** (puerto 8080): Muestra servicios activos, salud y métricas
2. **Redis Commander** (/redis): Permite ver los nodos registrados
3. **Health Check API** (/health): Verifica el estado de cada nodo
4. **Nodes API** (/nodes): Lista todos los nodos activos

## 🎯 Resultados y Aprendizajes

Después de completar este proyecto, logré:

- ✅ **Escalado horizontal sin tiempo de inactividad**
- ✅ **Sincronización perfecta de mensajes entre nodos**
- ✅ **Descubrimiento automático de servicios**
- ✅ **Monitoreo en tiempo real del cluster**
- ✅ **Manejo automático de fallos de nodos**

### Lecciones Aprendidas:

1. **Las sticky sessions son cruciales** para WebSockets en entornos balanceados
2. **El TTL en Redis** es perfecto para cleanup automático de nodos muertos
3. **Traefik simplifica enormemente** la configuración comparado con nginx/HAProxy
4. **Docker Compose con --scale** es sorprendentemente poderoso
5. **La observabilidad** es esencial en sistemas distribuidos

## 🚀 ¿Qué Sigue?

Este proyecto me demostró el poder de combinar tecnologías modernas para crear sistemas resilientes. Algunas mejoras futuras que considero:

- Implementar autenticación y autorización
- Agregar métricas con Prometheus
- Usar HTTPS con certificados SSL
- Implementar rate limiting
- Añadir persistencia de mensajes

## 💡 Reflexiones Finales

Como ingeniero cloud y ex-profesor, creo firmemente que la mejor manera de aprender arquitecturas complejas es construyéndolas. Este proyecto no solo me ayudó a profundizar en tecnologías como SignalR y Traefik, sino que también me recordó la importancia de documentar bien y pensar en la experiencia del desarrollador.

Si estás comenzando en el mundo de los microservicios o la comunicación en tiempo real, te animo a que experimentes con proyectos similares. La combinación de .NET, Docker y herramientas modernas de orquestación abre un mundo de posibilidades.

**¿Te animas a construir tu propio cluster escalable?** El código completo está disponible en mi repositorio. ¡Me encantaría conocer tus experiencias y mejoras!

---

*Sígueme para más artículos sobre arquitecturas cloud, .NET y DevOps. Como siempre, estoy abierto a preguntas y discusiones técnicas.*

**Saúl A. Quintero P.**  
*Ingeniero en Informática | Cloud Engineer | Ex-Profesor Universitario*

---

## 🔗 Enlaces Útiles

- [Documentación de SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [Traefik Documentation](https://traefik.io/)
- [Docker Compose Reference](https://docs.docker.com/compose/)
- [Redis Official Documentation](https://redis.io/)

#SignalR #DotNet #Docker #Microservices #CloudEngineering #RealTime #WebSockets #DevOps #Traefik #Redis