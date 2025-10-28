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
    
    [Header("Prefabs")]
    public GameObject bolaPrefab;
    public GameObject playerPrefab;
    
    private readonly Dictionary<int, GameObject> players = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Vector3> targetPositions = new Dictionary<int, Vector>();
    private GameObject bola;
    
    private UdpClient udpClient;
    private Thread receiveThread;
    private IPEndPoint serverEP;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private bool running = false;

    [Header("Configurações de Movimento")]
    public float moveSpeed = 20.0f;
    public float interpolationSpeed = 15.0f;
    public float yClamp = 3.0;

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(serveIP), serverPort);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();

        SendUdpMessage("HELLO");
    }
    void FixedUpdate()
    {
        // processa mensagens vindas da thread de rede
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
                kpv.Value.transform.position = Vector3.Lerp(kpv.Value.transform.position, target, time.DeltaTime * interpolationSpeed);
            }
        }
    }
    void HandleLocalMovement()
    {
        float v = Input.GetAxis("Vertical");
        GameObject me = players[myId];
        
        me.transform.Translate(Vector3.up * v * moveSpeed * Time.deltaTime);
        Vector3 pos = me.transform.position;
        pos.y = Mathf.Clamp(pos.y, -yClamp, yClamp);
        
    }

    void SendLocalPosition()
    {
        Vector3 pos = players[myId].transform.position;
        string msg = $"POS:{myId};{pos.x.ToString("F2", CultureInfo.InvariantCulture)};{pos.y.ToString("F2", CultureInfo.InvariantCulture)}";
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

                // joga mensagem na fila
                messageQueue.Enqueue(msg);
            }
            catch (Exception e)
            {
                break;
            }
        }
    }

    void ProcessMessage(string msg)
    {
        if (msg.StartsWith("ASSIGN:"))
        {
            myId = int.Parse(msg.Substring(7));
            Debug.Log($"[Cliente] Meu ID = {myId}");
            SpawnPlayers();

            if (myId == 1)
            {
                localCube = GameObject.Find("Player 1");
                remoteCube = GameObject.Find("Player 2");

                localCube.transform.position = new Vector3(-8f, 0f, 0f); // Esquerda
                remoteCube.transform.position = new Vector3(8f, 0f, 0f); // Direita

                // Inicializa remotePos corretamente
                remotePos = remoteCube.transform.position;
            }
            else if (myId == 2)
            {
                localCube = GameObject.Find("Player 2");
                remoteCube = GameObject.Find("Player 1");

                localCube.transform.position = new Vector3(8f, 0f, 0f); // Direita
                remoteCube.transform.position = new Vector3(-8f, 0f, 0f); // Esquerda

                // Inicializa remotePos corretamente
                remotePos = remoteCube.transform.position;
            }

            // Reset da bola
            if (bola != null)
            {
                bola.transform.position = Vector3.zero;
                var rb = bola.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
            }
        }
        else if (msg.StartsWith("POS:"))
        {
            string[] parts = msg.Substring(4).Split(';');
            if (parts.Length == 3)
            {
                int id = int.Parse(parts[0]);
                if (id != myId)
                {
                    float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    remotePos = new Vector3(x, y, 0);
                }
            }
        }
        else if (msg.StartsWith("BALL:"))
        {
            // Só atualiza se não for o host da bola (ID 2)
            if (myId != 2)
            {
                string[] parts = msg.Substring(5).Split(';');
                if (parts.Length == 2)
                {
                    float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[1], CultureInfo.InvariantCulture);

                    if (bola != null)
                        bola.transform.position = new Vector3(x, y, 0);
                }
            }
        }
        else if (msg.StartsWith("SCORE:"))
        {
            string[] parts = msg.Substring(6).Split(';');
            if (parts.Length == 2)
            {
                int scoreA = int.Parse(parts[0]);
                int scoreB = int.Parse(parts[1]);

                if (bola != null)
                {
                    Bola bolaScript = bola.GetComponent<Bola>();
                    bolaScript.PontoA = scoreA;
                    bolaScript.PontoB = scoreB;
                    bolaScript.textoPontoA.text = "Pontos: " + scoreA;
                    bolaScript.textoPontoB.text = "Pontos: " + scoreB;
                }
            }
        }
    }
    void SpawnPlayers()
    {
        
    }
    public void SendUdpMessage(string msg)
    {
        client.Send(Encoding.UTF8.GetBytes(msg), msg.Length);
    }
    void OnApplicationQuit()
    {
        receiveThread.Abort();
        client.Close();
    }
}
