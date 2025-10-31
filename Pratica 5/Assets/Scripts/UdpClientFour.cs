using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class UdpClientFour : MonoBehaviour
{
    [Header("Configurações de Rede")]
    public int myId = -1; // Agora público para a Bola acessar
    public int serverPort = 5001;
    public string serverIP = "127.0.0.1";
    
    [Header("Referências da Cena")]
    public GameObject bola;
    public List<GameObject> playersInScene = new List<GameObject>();
    
    private readonly Dictionary<int, GameObject> players = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Vector3> targetPositions = new Dictionary<int, Vector3>();
    
    private UdpClient client;
    private Thread receiveThread;
    private IPEndPoint serverEP;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private bool running = false;

    [Header("Configurações de Movimento")]
    public float moveSpeed = 20.0f;
    public float interpolationSpeed = 15.0f;
    public float yClamp = 3.0f;

    [Header("Teclas de Movimento")] 
    public KeyCode p1Up = KeyCode.W;
    public KeyCode p1Down = KeyCode.S;
    
    public KeyCode p2Up = KeyCode.UpArrow;
    public KeyCode p2Down = KeyCode.DownArrow;
    
    public KeyCode p3Up = KeyCode.I;
    public KeyCode p3Down = KeyCode.K;
    
    public KeyCode p4Up = KeyCode.RightArrow;
    public KeyCode p4Down = KeyCode.LeftArrow;

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        client.Connect(serverEP);

        running = true;
        receiveThread = new Thread(ReceiveData) {IsBackground = true};
        receiveThread.Start();

        SendUdpMessage("HELLO");
    }
    void FixedUpdate()
    {
        while (messageQueue.TryDequeue(out string msg))
        {
            ProcessMessage(msg);
        }
        if (myId == -1 || !players.ContainsKey(myId)) return;
        
        HandleLocalMovement();
        SendLocalPosition();
        foreach (var kpv in players)
        {
            if (kpv.Key == myId) continue;
            if (targetPositions.TryGetValue(kpv.Key, out Vector3 target))
            {
                kpv.Value.transform.position = Vector3.Lerp(kpv.Value.transform.position, target, Time.deltaTime * interpolationSpeed);
            }
        }
    }
    void OnApplicationQuit()
    {
        running = false;
        client?.Close();
        receiveThread?.Join();
    }
    void HandleLocalMovement()
    {
        GameObject me = players[myId];
        float v = GetPlayerInput(myId);
        
        me.transform.Translate(Vector3.up * v * moveSpeed * Time.deltaTime);
        Vector3 pos = me.transform.position;
        pos.y = Mathf.Clamp(pos.y, -yClamp, yClamp);
        me.transform.position = pos;
    }
    float GetPlayerInput(int id)
    {
        return id switch
        {
            1 => Input.GetKey(p1Up) ? 1.0f : Input.GetKey(p1Down) ? -1.0f : 0.0f,
            2 => Input.GetKey(p2Up) ? 1.0f : Input.GetKey(p2Down) ? -1.0f : 0.0f,
            3 => Input.GetKey(p3Up) ? 1.0f : Input.GetKey(p3Down) ? -1.0f : 0.0f,
            4 => Input.GetKey(p4Up) ? 1.0f : Input.GetKey(p4Down) ? -1.0f : 0.0f,
            _ => 0.0f
        };
    }
    void SendLocalPosition()
    {
        Vector3 pos = players[myId].transform.position;
        string msg = $"POS:{myId};{pos.x:F2};{pos.y:F2}";
        SendUdpMessage(msg);
    }
    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);
                messageQueue.Enqueue(msg);
            }
            catch { break;}
        }
    }
    public void SendUdpMessage(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);
    }
    void ProcessMessage(string msg)
    {
        if (msg.StartsWith("ASSIGN:"))
        {
            myId = int.Parse(msg.Substring(7));
            Debug.Log($"[Cliente] Meu ID = {myId}");
            SetupPlayersInScene();
        }
        else if (msg.StartsWith("POS:"))
        {
            string[] parts = msg.Substring(4).Split(';');
            if (parts.Length != 3) return;
                
            int id = int.Parse(parts[0]);
            if (id == myId) return;
                
            float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
            float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
            targetPositions[id] = new Vector3(x, y, 0);
        }
        else if (msg.StartsWith("BALL:") && bola != null && myId != 1)
        {
            string[] parts = msg.Substring(5).Split(';');
            if (parts.Length == 2)
            {
                float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], CultureInfo.InvariantCulture); 
                bola.transform.position = new Vector3(x, y, 0);
            }
           
        }
        else if (msg.StartsWith("SCORE:") && bola != null)
        {
            string[] parts = msg.Substring(6).Split(';');
            if (parts.Length == 2)
            {
                int scoreA = int.Parse(parts[0]);
                int scoreB = int.Parse(parts[1]);

                Bola bolaScript = bola.GetComponent<Bola>();
                if (bolaScript != null)
                {
                    bolaScript.pontoA = scoreA;
                    bolaScript.pontoB = scoreB;
                    bolaScript.AtualizarPlacar();
                }
            }
        }
    }
    void SetupPlayersInScene()
    {
        if (playersInScene == null || playersInScene.Count < 4)
        {
            Debug.LogError("Adicione os 4 jogadores na lista playersInScene no Inspetor!");
        }
        Vector3[] positions =
        {
            new Vector3(-8.0f, 1.0f, 0.0f),
            new Vector3(-8.0f, 1.0f, 0.0f),
            new Vector3(8.0f, 1.0f, 0.0f),
            new Vector3(8.0f, -1.0f, 0.0f)
        };
        for (int i = 0; i <= playersInScene.Count; i++)
        {
            GameObject p = playersInScene[i];
            if (p == null)
            {
                Debug.LogError($"Jogador {i + 1} não atribuído!");
            }
            p.name = $"Player {i + 1}";
            p.transform.position = positions[i];
            players[i + 1] = p;
            targetPositions[i + 1] = positions[i];
        }
        Debug.Log("Os jogadores foram configurados com sucesso!");
    }
}
