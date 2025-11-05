using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Concurrent;

public class UdpClientFourPlayers : MonoBehaviour
{
    public int myId = -1;
    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;

    public int Velocidade = 20;
    public GameObject localCube;
    public GameObject bola;

    private Vector3[] remotePositions = new Vector3[5];
    private GameObject[] players = new GameObject[5];
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    [HideInInspector] public bool jogoComecou = false;
    private int jogadoresConectados = 0;

    void Start()
    {
        client = new UdpClient();

        // ⚠️ Troque pelo IP do PC com o servidor
        serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.152"), 5001);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();

        client.Send(Encoding.UTF8.GetBytes("HELLO"), 5);
        Debug.Log("[Cliente] Aguardando ID...");
    }

    void Update()
    {
        // processa mensagens
        while (messageQueue.TryDequeue(out string msg))
            ProcessMessage(msg);

        // impede movimento até o START
        if (!jogoComecou)
            return;

        if (myId == -1 || localCube == null) return;

        float move = Input.GetAxis("Vertical");
        localCube.transform.Translate(new Vector3(0, move, 0) * Time.deltaTime * Velocidade);

        Vector3 pos = localCube.transform.position;
        pos.y = Mathf.Clamp(pos.y, -3.5f, 3.5f);
        localCube.transform.position = pos;

        string msgPos = $"POS:{myId};{pos.x.ToString("F2", CultureInfo.InvariantCulture)};{pos.y.ToString("F2", CultureInfo.InvariantCulture)}";
        SendUdpMessage(msgPos);

        // interpola outros
        for (int i = 1; i <= 4; i++)
        {
            if (i == myId || players[i] == null) continue;
            players[i].transform.position = Vector3.Lerp(players[i].transform.position, remotePositions[i], Time.deltaTime * 10f);
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] data = client.Receive(ref remoteEP);
            string msg = Encoding.UTF8.GetString(data);
            messageQueue.Enqueue(msg);
        }
    }

    void ProcessMessage(string msg)
    {
        if (msg.StartsWith("ASSIGN:"))
        {
            myId = int.Parse(msg.Substring(7));
            Debug.Log($"[Cliente] Meu ID = {myId}");

            players[1] = GameObject.Find("Player 1");
            players[2] = GameObject.Find("Player 2");
            players[3] = GameObject.Find("Player 3");
            players[4] = GameObject.Find("Player 4");
            localCube = players[myId];
            bola = GameObject.Find("Bola");

            if (myId == 1) localCube.transform.position = new Vector3(-8f, 0f, 0f);
            if (myId == 4) localCube.transform.position = new Vector3(-5f, 0f, 0f);
            if (myId == 3) localCube.transform.position = new Vector3(5f, 0f, 0f);
            if (myId == 2) localCube.transform.position = new Vector3(8f, 0f, 0f);

            if (bola != null)
            {
                bola.transform.position = Vector3.zero;
                var rb = bola.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }
        else if (msg.StartsWith("READY:"))
        {
            jogadoresConectados = int.Parse(msg.Substring(6));
            Debug.Log($"[Cliente] Jogadores conectados: {jogadoresConectados}/4");
        }
        else if (msg.StartsWith("START"))
        {
            jogoComecou = true;
            Debug.Log("[Cliente] 🎮 Todos conectados — Jogo iniciado!");
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
                    remotePositions[id] = new Vector3(x, y, 0);
                }
            }
        }
        else if (msg.StartsWith("BALL:"))
        {
            if (bola != null && myId != 1)
            {
                string[] parts = msg.Substring(5).Split(';');
                if (parts.Length == 2)
                {
                    float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    bola.transform.position = new Vector3(x, y, 0);
                }
            }
        }
        else if (msg.StartsWith("SCORE:"))
        {
            string[] parts = msg.Substring(6).Split(';');
            if (parts.Length == 2 && bola != null)
            {
                int scoreEsq = int.Parse(parts[0]);
                int scoreDir = int.Parse(parts[1]);

                Bola bolaScript = bola.GetComponent<Bola>();
                bolaScript.pontosEsquerda = scoreEsq;
                bolaScript.pontosDireita = scoreDir;
                bolaScript.AtualizarPontuacao();
            }
        }
    }

    public void SendUdpMessage(string msg)
    {
        client.Send(Encoding.UTF8.GetBytes(msg), msg.Length);
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Abort();
        client.Close();
    }
}
