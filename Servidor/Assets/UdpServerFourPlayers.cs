using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServerFourPlayers : MonoBehaviour
{
    UdpClient server;
    IPEndPoint anyEP;
    Thread receiveThread;
    Dictionary<string, int> clientIds = new Dictionary<string, int>();
    int nextId = 1;

    bool jogoIniciado = false;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);

        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();

        Debug.Log("Servidor iniciado na porta 5001");
    }

    void ReceiveData()
    {
        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = anyEP.Address + ":" + anyEP.Port;

                if (!clientIds.ContainsKey(key))
                {
                    if (nextId <= 4)
                    {
                        clientIds[key] = nextId++;
                        string assignMsg = "ASSIGN:" + clientIds[key];
                        server.Send(Encoding.UTF8.GetBytes(assignMsg), assignMsg.Length, anyEP);
                        Debug.Log($"Novo cliente conectado: {key} => ID {clientIds[key]}");

                        // Informa a todos quantos já entraram
                        Broadcast($"READY:{clientIds.Count}");

                        // Quando todos entram, envia START
                        if (clientIds.Count == 4 && !jogoIniciado)
                        {
                            jogoIniciado = true;
                            Broadcast("START");
                            Debug.Log("🎮 Todos conectados! Iniciando o jogo!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Conexão extra ignorada: {key}");
                        continue;
                    }
                }

                // retransmissão de mensagens comuns
                if (msg.StartsWith("POS:") || msg.StartsWith("BALL:") || msg.StartsWith("SCORE:"))
                {
                    Broadcast(msg);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Erro no servidor: " + ex.Message);
            }
        }
    }

    void Broadcast(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        foreach (var kvp in clientIds)
        {
            try
            {
                string[] parts = kvp.Key.Split(':');
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                server.Send(data, data.Length, ep);
            }
            catch { }
        }
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        server?.Close();
    }
}
