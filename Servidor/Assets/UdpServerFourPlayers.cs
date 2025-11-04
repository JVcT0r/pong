using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Concurrent;

public class UdpClientFourPlayers : MonoBehaviour
{
    public int myId = -1; // ID atribuído pelo servidor
    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;

    public int Velocidade = 20;
    public GameObject localCube; // Player controlado localmente
    public GameObject bola;      // Referência à bola na cena

    // Controle de posições dos outros jogadores
    private ConcurrentDictionary<int, Vector3> remotePositions = new ConcurrentDictionary<int, Vector3>();
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    void Start()
    {
        client = new UdpClient();

        // ⚠️ Troque o IP abaixo pelo IP do computador onde o servidor está rodando
        serverEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();

        // Envia mensagem inicial de conexão
        client.Send(Encoding.UTF8.GetBytes("HELLO"), 5);

        // Reseta a bola no início
        if (bola != null)
        {
            bola.transform.position = Vector3.zero;
            var rb = bola.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }
    }

    void Update()
    {
        // Processa mensagens vindas da thread de rede
        while (messageQueue.TryDequeue(out string msg))
        {
            ProcessMessage(msg);
        }

        if (myId == -1 || localCube == null) return;

        // Movimento vertical
        float v = Input.GetAxis("Vertical");
        localCube.transform.Translate(new Vector3(0, v, 0) * Time.deltaTime * Velocidade);

        // Limite no eixo Y
        Vector3 pos = localCube.transform.position;
        pos.y = Mathf.Clamp(pos.y, -3.5f, 3.5f);
        localCube.transform.position = pos;

        // Envia posição do jogador local
        string msgPos = "POS:" + myId + ";" +
                        localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                        localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
        SendUdpMessage(msgPos);

        // Atualiza visualmente os outros jogadores
        foreach (var kvp in remotePositions)
        {
            if (kvp.Key == myId) continue;
            GameObject other = GameObject.Find("Player " + kvp.Key);
            if (other != null)
                other.transform.position = Vector3.Lerp(other.transform.position, kvp.Value, Time.deltaTime * 10f);
        }
    }

    // Thread que escuta mensagens do servidor
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

    // Processa mensagens recebidas do servidor
    void ProcessMessage(string msg)
    {
        if (msg.StartsWith("ASSIGN:"))
        {
            myId = int.Parse(msg.Substring(7));
            Debug.Log("[Cliente] Meu ID = " + myId);

            localCube = GameObject.Find("Player " + myId);
            bola = GameObject.Find("Bola");

            // Posições fixas (2x2 times — baseadas nas imagens)
            if (myId == 1) localCube.transform.position = new Vector3(-8f, 0f, 0f); // Defesa esquerda
            if (myId == 4) localCube.transform.position = new Vector3(-5f, 0f, 0f); // Ataque esquerda
            if (myId == 3) localCube.transform.position = new Vector3(5f, 0f, 0f);  // Ataque direita
            if (myId == 2) localCube.transform.position = new Vector3(8f, 0f, 0f);  // Defesa direita

            // Reset da bola apenas pelo Player 1 (host)
            if (bola != null && myId == 1)
            {
                bola.transform.position = Vector3.zero;
                var rb = bola.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
            }
        }

        // Atualização de posição
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

        // Atualização da bola
        else if (msg.StartsWith("BALL:"))
        {
            // Apenas os clientes (não o host) atualizam a posição
            if (myId != 1)
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

        // Atualização da pontuação (2 times)
        else if (msg.StartsWith("SCORE:"))
        {
            string[] parts = msg.Substring(6).Split(';');
            if (parts.Length == 2 && bola != null)
            {
                var bolaScript = bola.GetComponent<Bola>();
                bolaScript.pontosEsquerda = int.Parse(parts[0]);
                bolaScript.pontosDireita  = int.Parse(parts[1]);
                bolaScript.AtualizarPontuacao();
            }
        }
    }

    // Envia mensagens para o servidor
    public void SendUdpMessage(string msg)
    {
        client.Send(Encoding.UTF8.GetBytes(msg), msg.Length);
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        client?.Close();
    }
}
