const signalR = require("@microsoft/signalr");

const connection = new signalR.HubConnectionBuilder()
    .withUrl(process.env.SIGNALR_URL || "http://localhost:8888/chathub")
    .build();

connection.start().then(function () {
    console.log("Connected to SignalR Hub!");
    
    // Send test messages
    setInterval(() => {
        const message = `Test message from ${connection.connectionId} at ${new Date().toISOString()}`;
        connection.invoke("SendMessage", "TestUser", message);
    }, 5000);
    
}).catch(function (err) {
    return console.error(err.toString());
});

connection.on("ReceiveMessage", function (user, message, connectionId) {
    console.log(`[${new Date().toISOString()}] ${user}: ${message} (from ${connectionId})`);
});

connection.onclose(function () {
    console.log("Connection closed.");
});

process.on('SIGINT', function() {
    console.log('\nShutting down gracefully...');
    connection.stop().then(() => {
        process.exit(0);
    });
});