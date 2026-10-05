using NativeWebSocket;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.GraphicsBuffer;

public class WebSocketMessageEventArgs : EventArgs
{
    public string source { get; private set; }
    public string payload { get; private set; }

    public WebSocketMessageEventArgs(string source, string payload)
    {
        this.source = source;
        this.payload = payload;
    }
}

[Serializable]
public class WebSocketMessageEvent : UnityEvent<WebSocketMessageEventArgs> { }

public class WebsocketClient : MonoBehaviour
{
    //По советам с редитта синглтонимся
    public static WebsocketClient Instance { get; private set; }

    public WebSocketMessageEvent OnMessageReceived = new WebSocketMessageEvent();

    private WebSocket _websocket;

    [SerializeField] string _url = "wss://echo.websocket.org";
    [SerializeField] bool _connectOnStart = true; //попытаться подключиться при старте проекта, на случай если по названию не понятно

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // на всякий случай если мы тупые и на сцене будет >1 объект с этим скриптом
            return;
        }
    }

    async void Start()
    {
        if (_connectOnStart) 
        {
            await ConnectToServer();
        }
    }

    public async System.Threading.Tasks.Task ConnectToServer() //такие ужасы с асинками нужны, как оказывается для того чтобы не умирала вся сцена при подключении
    {
        _websocket = new WebSocket(_url);

        _websocket.OnOpen += () => print($"WebSocket connected to:\n {_url}");
        _websocket.OnError += (errorText) => Debug.LogError($"WebSocket error:\n {errorText}");
        _websocket.OnClose += (reasonForStopping) => print($"WebSocket connection closed:\n {reasonForStopping}"); //причина остановки

        _websocket.OnMessage += (bytes) =>
        {
            string rawMessage = System.Text.Encoding.UTF8.GetString(bytes); //гиперспецифичная функция шарпов наконец то применена, можно и на покой
            print(rawMessage);

            OnMessageReceived.Invoke(new WebSocketMessageEventArgs(_url, rawMessage)); //не совсем понял для чего тебе source, поменяй _url на что то нужное если это не то
        };

        await _websocket.Connect();
    }

    public async void SendMessage(string destination, string payload)
    {
        if (_websocket != null && _websocket.State == WebSocketState.Open)
        {
            string messageToSend = $"{{\"destination\": {destination}, \"payload\": {payload}}}";

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(messageToSend);
            await _websocket.Send(bytes);
        }
        else
        {
            Debug.LogError("WebSocket error: \nwebsocket isn't open or doesn't exist");
        }
    }

    private void Update()
    {
        if (_websocket != null)
        {
            _websocket.DispatchMessageQueue();
        }
    }

    private async void OnApplicationQuit()
    {
        if (_websocket != null)
        {
            await _websocket.Close();
        }
    }
}