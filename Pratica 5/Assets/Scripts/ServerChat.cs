using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

public class TcpServerUnity : MonoBehaviour
{
    TcpListener server;
    Thread serverThread;
    public TMP_Text text;
    private string msg;

    void Start()
    {
        serverThread = new Thread(new ThreadStart(StartServer));
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    private void Update()
    {
        text.text = msg;
    }

    void StartServer()
    {
        server = new TcpListener(IPAddress.Any, 8080);
        server.Start();
        Debug.Log("Servidor ouvindo na porta 8080...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int len = stream.Read(buffer, 0, buffer.Length);
            msg = Encoding.UTF8.GetString(buffer, 0, len);
            Debug.Log($"[Servidor] Mensagem recebida: {msg}");
            text.text = msg;

            stream.Close();
            client.Close();
        }
    }

    void OnApplicationQuit()
    {
        server?.Stop();
        serverThread?.Abort();
    }
}
