const signalR = require("@microsoft/signalr");

async function testCluster() {
    const connections = [];
    const numConnections = 5;
    
    console.log(`Creating ${numConnections} connections to test cluster...`);
    
    for (let i = 0; i < numConnections; i++) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(process.env.SIGNALR_URL || "http://localhost:8888/chathub")
            .build();
            
        connection.on("ReceiveMessage", function (user, message, connectionId) {
            console.log(`Connection ${i}: [${user}] ${message} (from ${connectionId})`);
        });
        
        try {
            await connection.start();
            connections.push(connection);
            console.log(`Connection ${i} established with ID: ${connection.connectionId}`);
        } catch (err) {
            console.error(`Failed to connect ${i}:`, err);
        }
    }
    
    // Send messages from each connection
    console.log("\nSending test messages...");
    for (let i = 0; i < connections.length; i++) {
        const connection = connections[i];
        try {
            await connection.invoke("SendMessage", `User${i}`, `Hello from connection ${i}!`);
        } catch (err) {
            console.error(`Failed to send message from connection ${i}:`, err);
        }
    }
    
    // Test group functionality
    console.log("\nTesting group functionality...");
    for (let i = 0; i < Math.min(3, connections.length); i++) {
        try {
            await connections[i].invoke("JoinGroup", "TestGroup");
        } catch (err) {
            console.error(`Failed to join group for connection ${i}:`, err);
        }
    }
    
    // Send group message
    setTimeout(async () => {
        if (connections.length > 0) {
            try {
                await connections[0].invoke("SendToGroup", "TestGroup", "GroupUser", "Group message test!");
            } catch (err) {
                console.error("Failed to send group message:", err);
            }
        }
    }, 2000);
    
    // Cleanup after 10 seconds
    setTimeout(async () => {
        console.log("\nClosing connections...");
        for (const connection of connections) {
            try {
                await connection.stop();
            } catch (err) {
                console.error("Error closing connection:", err);
            }
        }
        process.exit(0);
    }, 10000);
}

testCluster().catch(console.error);