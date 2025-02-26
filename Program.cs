using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    private const string WebSocketUrl = "ws://localhost:8082";
    private static ClientWebSocket webSocket = new ClientWebSocket();

    public static async Task Main(string[] args)
    {
        try
        {
            await webSocket.ConnectAsync(new Uri(WebSocketUrl), CancellationToken.None);
            Console.WriteLine("Connected to WebSocket server.");
            await ReceiveMessages();
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
        }
        finally
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            webSocket.Dispose();
            Console.WriteLine("Connection closed.");
        }
    }

    private static async Task ReceiveMessages()
    {
        var buffer = new byte[65536];

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("WebSocket connection closed.");
                break;
            }

            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received: {message}");


            try
            {
                JObject jsonMessage = JObject.Parse(message);
                string transactionId = jsonMessage["id"]?.ToString();
                string stage = jsonMessage["stage"]?.ToString();

                Console.WriteLine($"Stage: {stage}");

                if (!string.IsNullOrEmpty(transactionId))
                {
                    jsonMessage["send_request"] = true;
                    jsonMessage["send_response"] = true;
                    JObject response = jsonMessage;
                    
                    if (stage == "response")
                    {
                        string responseBody = response["response"]["body"].ToString();
                        byte [] decodedBytes = Convert.FromBase64String(responseBody);
                        string decodedJson = Encoding.UTF8.GetString(decodedBytes); 
                        dynamic jsonObject = JsonConvert.DeserializeObject(decodedJson);
                        if (jsonObject?.ContainsKey("pause_collection"))
                        {
                            jsonObject["pause_collection"] = true;
                        }
                        if (jsonObject?.ContainsKey("auto_update_mode"))
                        {
                            jsonObject["auto_update_mode"] = "MOCK";
                        }

                        string modifiedJson = JsonConvert.SerializeObject(jsonObject);
                        byte [] modifiedBytes = Encoding.UTF8.GetBytes(modifiedJson);
                        string modifiedResponseBody = Convert.ToBase64String(modifiedBytes);
                        response["response"]["body"] = modifiedResponseBody;

                    }
                    await SendMessage(response.ToString());
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }
    }

    private static async Task SendMessage(string message)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine($"Sent: {message}");
        }
    }
}